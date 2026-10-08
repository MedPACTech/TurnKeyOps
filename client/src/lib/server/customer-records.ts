import { fail, redirect, isRedirect } from '@sveltejs/kit';
import { peopleRequest } from '$lib/server/people';
import type { Contact } from '$lib/server/contacts';
import { apiRequest } from '$lib/api/client';
import type { RequestEvent } from '@sveltejs/kit';
import { authTokenCookie } from './auth-session';
const sessionToken = (event: RequestEvent) => event.cookies.get(authTokenCookie) ?? null;
const fetchCustomers = (fetcher: typeof fetch, token: string | null) => apiRequest<CustomerRecord[]>('/api/Customers/search?query=', {}, fetcher, token);
const fetchIntake = (fetcher: typeof fetch, token: string | null) => apiRequest<any[]>('/api/quote-requests', {}, fetcher, token);
import { customerInput, type CustomerRecord } from '$lib/customer-records';
export const load = async (event: RequestEvent) => {
 const token = sessionToken(event);
 const canManagePortal = event.locals.bdrAdminSession?.role === 'owner' && (event.locals.modulePermissions ?? []).includes('settings.write');
 let portalContacts: Contact[] = [], portalContactsError = '';
 if (canManagePortal) {
  try { portalContacts = await peopleRequest<Contact[]>(event, '/api/contacts'); }
  catch { portalContactsError = 'Customer contacts could not be loaded. Reload to try again.'; }
 }
 try {
  const customers = await fetchCustomers(event.fetch, token);
  let draft: Partial<CustomerRecord> | null = null;
  const fromRequest = event.url.searchParams.get('fromRequest');
  if (fromRequest) {
   const request = (await fetchIntake(event.fetch, token)).find(record => record.id === fromRequest);
   if (request) draft = { firstName: request.contactName || request.customerName, lastName: '', companyName: request.companyName, email: request.email, phone: request.phone, address: request.serviceAddress };
  }
  return { customers, draft, loadError: '', canManagePortal, portalContacts, portalContactsError };
 } catch { return { canManagePortal, portalContacts, portalContactsError, customers: [], draft: null, loadError: 'Customer records could not be loaded. Check your connection and try again.' }; }
};
export const actions = {
 setupPrimaryContact: async (event: RequestEvent) => {
  if (event.locals.bdrAdminSession?.role !== 'owner' || !(event.locals.modulePermissions ?? []).includes('settings.write')) return fail(403, {error:'Only an owner can set up portal access.'});
  const form = await event.request.formData();
  try {
   const customer = (await fetchCustomers(event.fetch, sessionToken(event))).find(c=>c.id===String(form.get('customerId')));
   if (!customer || customer.customerType !== 'residential') return fail(400, {error:'Save this as a residential customer first.'});
   if ((customer.dateUpdated ?? '') !== String(form.get('dateUpdated') ?? '')) return fail(409, {error:'Customer details changed. Reload before setting up access.'});
   const existing = await peopleRequest<Contact[]>(event, '/api/contacts');
   if (!existing.some(p=>p.customerId===customer.id && p.profileTypes.includes('customer'))) {
    await peopleRequest(event, '/api/contacts', {method:'POST',body:JSON.stringify({firstName:String(form.get('firstName')??'').trim(),lastName:String(form.get('lastName')??'').trim(),contactEmail:String(form.get('email')??'').trim()||null,contactPhone:String(form.get('phone')??'').trim()||null,profileTypes:['customer'],customerId:customer.id,address:customer.address,city:customer.city,state:customer.state,postalCode:customer.zip})});
   }
   throw redirect(303, `${event.url.pathname}?customer=${customer.id}`);
  } catch(cause) {if(isRedirect(cause))throw cause;return fail(400,{error:cause instanceof Error?cause.message:'Could not set up the customer contact.'});}
 },
 save: async (event: RequestEvent) => {
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
   throw redirect(303, `${event.url.pathname}?customer=${encodeURIComponent(customer.id)}&saved=1`);
  } catch (cause) { if (isRedirect(cause)) throw cause; return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save this customer.' }); }
 }
};
