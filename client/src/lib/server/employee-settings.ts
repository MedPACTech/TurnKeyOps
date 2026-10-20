import { fail, type RequestEvent } from '@sveltejs/kit';
import { getTenantSettings, updateTenantSettings } from '$lib/api/tenant-settings';
import { authTokenCookie } from './auth-session';
import { peopleRequest, type EmployeeAccessConfiguration, type Person } from './people';
export const load = async (event: RequestEvent) => {
 const document = await getTenantSettings<EmployeeAccessConfiguration>('employee-access', event.fetch, event.cookies.get(authTokenCookie));
 const employees = await peopleRequest<Person[]>(event, '/api/people');
 return { employees: employees.filter(person => person.isActive && person.profileTypes.includes('employee')), employeeAccess: document.values, version: document.version, canManage: event.locals.bdrAdminSession?.role === 'owner' };
};
export const actions = { assignRole: async (event: RequestEvent) => {
 if (event.locals.bdrAdminSession?.role !== 'owner') return fail(403, { error: 'Only owners can assign roles.' });
 const form = await event.request.formData();
 try {
  const employees = await peopleRequest<Person[]>(event, '/api/people');
  const person = employees.find(person => person.id === String(form.get('id')) && person.isActive && person.profileTypes.includes('employee'));
  if (!person || person.isOwner) return fail(400, { error: 'Choose an active employee. Owners retain full access.' });
  const employeeRoleId = String(form.get('employeeRoleId') || '');
  if (!employeeRoleId) return fail(400, { error: 'Choose a role.' });
  await peopleRequest(event, `/api/people/${encodeURIComponent(person.id)}`, { method: 'PUT', body: JSON.stringify({
   firstName: person.firstName, lastName: person.lastName, contactEmail: person.contactEmail, contactPhone: person.contactPhone,
   profileTypes: ['employee'], title: person.title, team: person.team, customerId: person.customerId,
   employeeRoleId, modulePermissions: null, expectedVersion: String(form.get('version') || '')
  }) });
  return { message: 'Role assigned. Module access now follows this role.' };
 } catch (cause) { const error = cause as { status?: number; body?: { message?: string }; message?: string }; return fail(error.status || 400, { error: error.body?.message || error.message || 'Could not assign role.' }); }
}, save: async (event: RequestEvent) => {
 const form = await event.request.formData();
 try {
  const values = JSON.parse(String(form.get('configuration')));
  await updateTenantSettings('employee-access', values, String(form.get('version') || ''), event.fetch, event.cookies.get(authTokenCookie));
  return { message: 'Roles and teams saved. Assigned employees now inherit these permissions.' };
 } catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save roles and teams.' }); }
} };
