import { fail, redirect, isRedirect } from '@sveltejs/kit';
import { apiRequest } from '$lib/api/client';
import { fetchCustomers, fetchIntake, sessionToken } from '../records.server';
import { customerInput, type CustomerRecord } from '../records';
export const load = async (event) => {
 const token = sessionToken(event);
 try {
  const customers = await fetchCustomers(event.fetch, token);
  let draft: Partial<CustomerRecord> | null = null;
  const fromRequest = event.url.searchParams.get('fromRequest');
  if (fromRequest) {
   const request = (await fetchIntake(event.fetch, token)).find(record => record.id === fromRequest);
   if (request) draft = { firstName: request.contactName || request.customerName, lastName: '', companyName: request.companyName, email: request.email, phone: request.phone, address: request.serviceAddress };
  }
  return { customers, draft, loadError: '' };
 } catch { return { customers: [], draft: null, loadError: 'Customer records could not be loaded. Check your connection and try again.' }; }
};
export const actions = {
 save: async (event) => {
  const token = sessionToken(event);
  const form = await event.request.formData();
  try {
   const input = customerInput(form);
   const id = String(form.get('id') ?? '');
   let existing: CustomerRecord | undefined;
   if (id) {
    existing = (await fetchCustomers(event.fetch, token)).find(customer => customer.id === id);
    if (!existing) return fail(404, { error: 'Customer is not available in this workspace.' });
    if ((existing.dateUpdated ?? '') !== String(form.get('dateUpdated') ?? '')) return fail(409, { error: 'This customer changed. Reload before saving.' });
   }
   const customer = await apiRequest<CustomerRecord>('/api/Customers', { method: id ? 'PUT' : 'POST', body: JSON.stringify({ ...existing, ...input, ...(id ? { id } : {}) }) }, event.fetch, token);
   throw redirect(303, `/carlzipf/admin/customers?customer=${encodeURIComponent(customer.id)}&saved=1`);
  } catch (cause) { if (isRedirect(cause)) throw cause; return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save this customer.' }); }
 }
};
