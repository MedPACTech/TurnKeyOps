import { fail } from '@sveltejs/kit';
import { apiRequest } from '$lib/api/client';
import { sessionToken } from '../records.server';
import type { FieldEstimate } from '$lib/locksmith-handoff';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async (event) => {
	const token = sessionToken(event);
	event.setHeaders({ 'Cache-Control': 'private, no-store', 'Referrer-Policy': 'no-referrer' });
	try {
		const packets = await apiRequest<FieldEstimate[]>('/api/quote-estimates', {}, event.fetch, token);
		return { estimates: packets.filter((packet) => packet.locksmithPricing).sort((a, b) => b.savedAtUtc.localeCompare(a.savedAtUtc)), selectedId: event.url.searchParams.get('request') ?? '', loadError: '' };
	} catch { return { estimates: [], selectedId: '', loadError: 'Estimates could not be loaded. Reconnect and retry.' }; }
};

const action = (kind: 'office-approval' | 'send') => async (event: Parameters<Actions[string]>[0]) => {
	const token = sessionToken(event);
	const form = await event.request.formData();
	const id = String(form.get('requestId') ?? '');
	const expectedVersion = String(form.get('version') ?? '');
	if (!/^[0-9a-f-]{36}$/i.test(id) || !expectedVersion) return fail(400, { error: 'Load a valid estimate revision before continuing.' });
	if (form.get('reviewed') !== 'yes') return fail(400, { error: 'Confirm that you reviewed the scope and server pricing.' });
	try {
		const current = await apiRequest<FieldEstimate>(`/api/quote-estimates/${id}`, {}, event.fetch, token);
		if (!current.locksmithPricing) return fail(400, { error: 'This estimate does not use the doors and locksmith pricing module.' });
		if (kind === 'send' && current.status === 'sent') return { message: 'This revision was already issued. Its existing customer review link is shown below; no message was sent.' };
		if (current.version !== expectedVersion) return fail(409, { error: 'The estimate changed. Review the latest saved revision before continuing.' });
		await apiRequest(`/api/quote-estimates/${id}/${kind}`, { method: 'POST', body: JSON.stringify({ expectedVersion }) }, event.fetch, token);
		return { message: kind === 'office-approval' ? 'Office pricing review recorded. The quote is ready for a customer review link.' : 'Customer review link issued. Copy or open it below; no email or text was sent.' };
	} catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'The quote action was not confirmed. Refresh its status before retrying.' }); }
};

export const actions: Actions = { approve: action('office-approval'), issue: action('send') };
