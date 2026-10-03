import { fail } from '@sveltejs/kit';
import { apiRequest } from '$lib/api/client';
import { loadLocksmithSettings } from '$lib/server/locksmith-settings';
import { listCurrentTenantUsers } from '$lib/server/user-administration';
import { requestJobType, scheduleConflicts, schedulingTechnicians, validVisitWindow } from '$lib/locksmith-scheduling';
import { fetchIntake, sessionToken } from '../records.server';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async (event) => {
	const token = sessionToken(event);
	event.setHeaders({ 'Cache-Control': 'private, no-store' });
	try {
		const [requests, users, { settings }] = await Promise.all([
			fetchIntake(event.fetch, token), listCurrentTenantUsers(event.fetch, token), loadLocksmithSettings(event.fetch, token)
		]);
		return {
			requests, technicians: schedulingTechnicians(users, settings),
			selectedId: event.url.searchParams.get('request') ?? '', loadError: ''
		};
	} catch (cause) {
		return { requests: [], technicians: [], selectedId: '', loadError: cause instanceof Error ? cause.message : 'Calendar could not be loaded.' };
	}
};

export const actions: Actions = {
	schedule: async (event) => {
		const token = sessionToken(event);
		const form = await event.request.formData();
		const id = String(form.get('requestId') ?? '');
		const version = String(form.get('updatedAtUtc') ?? '');
		const membershipId = String(form.get('membershipId') ?? '');
		const visitDate = String(form.get('visitDate') ?? '');
		const windowStart = String(form.get('windowStart') ?? '');
		const windowEnd = String(form.get('windowEnd') ?? '');
		const notes = String(form.get('notes') ?? '').trim();
		if (!validVisitWindow(visitDate, windowStart, windowEnd) || notes.length > 1000)
			return fail(400, { error: 'Choose a valid date and time window, with notes under 1,000 characters.' });
		try {
			const [requests, users, { settings }] = await Promise.all([
				fetchIntake(event.fetch, token), listCurrentTenantUsers(event.fetch, token), loadLocksmithSettings(event.fetch, token)
			]);
			const request = requests.find((entry) => entry.id === id);
			if (!request) return fail(404, { error: 'This request is not available in the Carl Zipf workspace.' });
			if (request.updatedAtUtc !== version) return fail(409, { error: 'The request changed. Reload before scheduling.' });
			if (!['qualified', 'contacted', 'inspection-scheduled'].includes(request.status))
				return fail(400, { error: 'Qualify the request before scheduling its assessment.' });
			const jobType = requestJobType(request);
			const tech = schedulingTechnicians(users, settings).find((user) => user.membershipId === membershipId && jobType && user.jobTypes.includes(jobType));
			if (!tech) return fail(400, { error: 'Choose an active technician enabled for this residential or commercial job type.' });
			if (scheduleConflicts(requests, id, tech.label, visitDate, windowStart, windowEnd))
				return fail(409, { error: 'That technician has an overlapping assessment. Choose another time or technician.' });
			await apiRequest(`/api/quote-requests/${encodeURIComponent(id)}`, {
				method: 'PUT', body: JSON.stringify({ ...request, status: 'inspection-scheduled', assignedTo: tech.label,
					nextAction: `Assessment scheduled for ${visitDate} ${windowStart}–${windowEnd}. Verify measurements and hardware on site.`,
					siteVisitSchedule: { visitDate, windowStart, windowEnd, siteContact: request.contactName || request.customerName,
						siteContactPhone: request.phone || '', assignedFieldResource: tech.label, notes } })
			}, event.fetch, token);
			return { message: `Assessment scheduled with ${tech.label} on ${visitDate}. No email or text was sent.` };
		} catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'The assessment could not be scheduled.' }); }
	}
};
