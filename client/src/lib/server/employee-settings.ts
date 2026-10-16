import { fail, type RequestEvent } from '@sveltejs/kit';
import { getTenantSettings, updateTenantSettings } from '$lib/api/tenant-settings';
import { authTokenCookie } from './auth-session';
import type { EmployeeAccessConfiguration } from './people';
export const load = async (event: RequestEvent) => {
 const document = await getTenantSettings<EmployeeAccessConfiguration>('employee-access', event.fetch, event.cookies.get(authTokenCookie));
 return { employeeAccess: document.values, version: document.version, canManage: event.locals.bdrAdminSession?.role === 'owner' };
};
export const actions = { save: async (event: RequestEvent) => {
 const form = await event.request.formData();
 try {
  const values = JSON.parse(String(form.get('configuration')));
  await updateTenantSettings('employee-access', values, String(form.get('version') || ''), event.fetch, event.cookies.get(authTokenCookie));
  return { message: 'Roles and teams saved. Assigned employees now inherit these permissions.' };
 } catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save roles and teams.' }); }
} };
