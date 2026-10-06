import { fail } from '@sveltejs/kit';
import { apiRequest } from '$lib/api/client';
import { fetchIntake, sessionToken } from '../records.server';
import { triageStatuses } from '../records';
export const load = async (event) => {
 const token = sessionToken(event);
 try { return { requests: await fetchIntake(event.fetch, token), loadError: '', selectedId: event.url.searchParams.get('request') ?? '' }; }
 catch { return { requests: [], loadError: 'Leads could not be loaded. Check your connection and try again.', selectedId: '' }; }
};
export const actions = {
 update: async (event) => {
  const token = sessionToken(event);
  const form = await event.request.formData();
  try {
   const id = String(form.get('id') ?? '');
   const current = (await fetchIntake(event.fetch, token)).find(record => record.id === id);
   if (!current) return fail(404, { error: 'This lead is not available in your workspace.' });
   if (current.updatedAtUtc !== form.get('updatedAtUtc')) return fail(409, { error: 'This lead changed. Reload before saving.' });
   const status = String(form.get('status') ?? '');
   const nextAction = String(form.get('nextAction') ?? '').trim();
   if (!triageStatuses(current.status).includes(status) || !nextAction || nextAction.length > 1000) return fail(400, { error: 'Choose a valid status and enter a next action (up to 1,000 characters).' });
   await apiRequest(`/api/quote-requests/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify({ ...current, status, nextAction, assignedTo: current.assignedTo || 'Office intake' }) }, event.fetch, token);
   return { message: 'Lead follow-up saved.', id };
  } catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save this lead.' }); }
 }
};
