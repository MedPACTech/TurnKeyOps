import { fail } from '@sveltejs/kit';
import { thinkPinkTenant } from '$lib/config/tenants';
import { uploadQuoteRequestAttachments } from '$lib/server/quote-request-attachments';
import { submitQuoteRequest } from '$lib/server/quote-requests';
import type { Actions } from './$types';

const MAX_PHOTOS = 10;
const MAX_PHOTO_BYTES = 10 * 1024 * 1024;
const getValue = (data: FormData, key: string) => String(data.get(key) ?? '').trim();

export const load = () => ({ submissionId: crypto.randomUUID() });

export const actions: Actions = {
	quote: async ({ request, fetch }) => {
		const data = await request.formData();
		const requestedSubmissionId = getValue(data, 'submissionId');
		const submissionId = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(requestedSubmissionId)
			? requestedSubmissionId
			: crypto.randomUUID();
		const website = getValue(data, 'website');
		const values = {
			name: getValue(data, 'name'),
			phone: getValue(data, 'phone'),
			email: getValue(data, 'email'),
			address: getValue(data, 'address'),
			acreage: getValue(data, 'acreage'),
			service: getValue(data, 'service'),
			timeline: getValue(data, 'timeline')
		};

		if (website || !values.name || !values.phone || !values.address) {
			return fail(400, {
				success: false,
				error: website ? 'The request could not be accepted. Refresh the page and try again.' : 'Name, phone, and property address are required.',
				values,
				submissionId
			});
		}

		const photos = data.getAll('photos').filter((value): value is File => value instanceof File && value.size > 0);
		if (photos.length > MAX_PHOTOS) {
			return fail(400, {
				success: false,
				error: `Please attach ${MAX_PHOTOS} photos or fewer.`,
				values,
				submissionId
			});
		}

		if (photos.some((photo) => photo.size > MAX_PHOTO_BYTES)) {
			return fail(400, {
				success: false,
				error: 'Each photo must be under 10 MB.',
				values,
				submissionId
			});
		}

		let durableRequestCreated = false;
		try {
			const id = submissionId;
			await submitQuoteRequest(fetch, {
				id,
				tenantId: thinkPinkTenant.id,
				website,
				companyName: values.name,
				contactName: values.name,
				email: values.email,
				phone: values.phone,
				siteName: values.address,
				serviceAddress: values.address,
				serviceType: values.service || 'Land clearing assessment',
				propertyType: values.acreage || 'Acreage needs confirmation',
				requestedTimeline: values.timeline || 'Needs follow-up',
				priority: values.timeline.toLowerCase().includes('urgent') ? 'emergency' : 'standard',
				need: [
					values.service || 'Land clearing assessment',
					values.acreage ? `Approximate acreage: ${values.acreage}` : '',
					photos.length ? `${photos.length} property photo${photos.length === 1 ? '' : 's'} attached.` : ''
				]
					.filter(Boolean)
					.join('. '),
				attachments: [],
				assignedTo: 'Think Pink intake',
				nextAction: 'Call the property owner and schedule an on-site assessment.',
				routingNote: 'Submitted from the Think Pink public website.'
			});
			durableRequestCreated = true;
			await uploadQuoteRequestAttachments(fetch, thinkPinkTenant.id, id, photos);
		} catch (cause) {
			console.error('Think Pink quote request submission failed.', cause);
			const status = typeof cause === 'object' && cause !== null && 'status' in cause ? Number(cause.status) : 0;
			const timedOut = cause instanceof Error && (cause.name === 'TimeoutError' || cause.name === 'AbortError');
			return fail(502, {
				success: false,
				error: durableRequestCreated
					? `Your request was saved as ${submissionId.slice(0, 8).toUpperCase()}, but its photos were not confirmed. Retry with the same photos; your request will not be duplicated.`
					: status === 429
						? 'Too many requests were sent from this network. Wait one minute, then retry.'
						: timedOut
							? 'The request timed out and was not confirmed. Check your connection and retry; duplicate prevention is enabled.'
							: 'We could not confirm a durable request. Please retry; duplicate prevention is enabled.',
				values,
				submissionId,
				durableRequestCreated
			});
		}

		return { success: true, error: null, values: null, submissionId, reference: submissionId.slice(0, 8).toUpperCase() };
	}
};
