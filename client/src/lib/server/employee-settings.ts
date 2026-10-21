import { personRoleIds, personTeams } from '$lib/employee-roles';
import { fail, type RequestEvent } from '@sveltejs/kit';
import { getTenantSettings, updateTenantSettings } from '$lib/api/tenant-settings';
import { authTokenCookie } from './auth-session';
import { peopleRequest, type EmployeeAccessConfiguration, type Person } from './people';
export const load = async (event: RequestEvent) => {
 const document = await getTenantSettings<EmployeeAccessConfiguration>('employee-access', event.fetch, event.cookies.get(authTokenCookie));
 const employees = await peopleRequest<Person[]>(event, '/api/people');
 return { employees: employees.filter(person => person.isActive && person.profileTypes.includes('employee')), employeeAccess: document.values, version: document.version, canManage: event.locals.bdrAdminSession?.role === 'owner' };
};
export const actions = { saveTeam: async (event: RequestEvent) => {
 if (event.locals.bdrAdminSession?.role !== 'owner') return fail(403, { error: 'Only owners can configure teams.' });
 const form = await event.request.formData();
 try {
  const document = await getTenantSettings<EmployeeAccessConfiguration>('employee-access', event.fetch, event.cookies.get(authTokenCookie));
  const name = String(form.get('teamName') || '').trim();
  const previous = String(form.get('previousName') || '');
  if (!name || name.length > 100) return fail(400, { error: 'Enter a team name of at most 100 characters.' });
  if (previous && !document.values.teams.includes(previous)) return fail(400, { error: 'This team changed. Reload before saving.' });
  if (document.values.teams.some(team => team !== previous && team.toLowerCase() === name.toLowerCase())) return fail(400, { error: 'A team with this name already exists.' });
  const teamAliases = { ...document.values.teamAliases };
  if (previous && previous !== name) {
   for (const key of Object.keys(teamAliases)) if (teamAliases[key] === previous) teamAliases[key] = name;
   teamAliases[previous] = name;
  }
  if (!previous && teamAliases[name]) return fail(400, { error: 'This name belongs to a renamed team. Choose another name.' });
  // Reusing a previous name is safe: older names now resolve directly to this name.
  delete teamAliases[name];
  const teams = previous ? document.values.teams.map(team => team === previous ? name : team) : [...document.values.teams, name];
  await updateTenantSettings('employee-access', { ...document.values, teams, teamAliases }, String(form.get('version') || ''), event.fetch, event.cookies.get(authTokenCookie));
  return { message: 'Team saved.' };
 } catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save team.' }); }
}, assignTeam: async (event: RequestEvent) => {
 if (event.locals.bdrAdminSession?.role !== 'owner') return fail(403, { error: 'Only owners can assign teams.' });
 const form = await event.request.formData();
 try {
  const employees = await peopleRequest<Person[]>(event, '/api/people');
  const person = employees.find(person => person.id === String(form.get('id')) && person.isActive && person.profileTypes.includes('employee'));
  if (!person) return fail(400, { error: 'Choose an active employee.' });
  const team = String(form.get('team') || '');
  const configuration = await peopleRequest<EmployeeAccessConfiguration>(event, '/api/people/configuration');
  if (!team || !configuration.teams.includes(team)) return fail(400, { error: 'Choose a team configured in Settings.' });
  const removing = form.get('operation') === 'remove';
  if (removing && !personTeams(person).includes(team)) return fail(409, { error: 'This assignment changed. Reload before saving.' });
  const employeeRoleIds = personRoleIds(person);
  await peopleRequest(event, `/api/people/${encodeURIComponent(person.id)}`, { method: 'PUT', body: JSON.stringify({
   firstName: person.firstName, lastName: person.lastName, contactEmail: person.contactEmail, contactPhone: person.contactPhone,
   profileTypes: ['employee'], title: person.title, teams: removing ? personTeams(person).filter(name => name !== team) : [...new Set([...personTeams(person), team])], customerId: person.customerId,
   employeeRoleId: employeeRoleIds[0] || null, employeeRoleIds, modulePermissions: person.modulePermissions, expectedVersion: String(form.get('version') || '')
  }) });
  return { message: removing ? 'Employee removed from team.' : 'Employee assigned to team.' };
 } catch (cause) { const error = cause as {status?:number;body?:{message?:string};message?:string}; return fail(error.status || 400, {error:error.body?.message || error.message || 'Could not update team assignment.'}); }
}, assignRole: async (event: RequestEvent) => {
 if (event.locals.bdrAdminSession?.role !== 'owner') return fail(403, { error: 'Only owners can assign roles.' });
 const form = await event.request.formData();
 try {
  const employees = await peopleRequest<Person[]>(event, '/api/people');
  const person = employees.find(person => person.id === String(form.get('id')) && person.isActive && person.profileTypes.includes('employee'));
  if (!person) return fail(400, { error: 'Choose an active employee.' });
  const employeeRoleId = String(form.get('employeeRoleId') || '');
  if (!employeeRoleId) return fail(400, { error: 'Choose a role.' });
  const employeeRoleIds = form.get('operation') === 'remove' ? personRoleIds(person).filter(id => id !== employeeRoleId) : [...new Set([...personRoleIds(person), employeeRoleId])];
  await peopleRequest(event, `/api/people/${encodeURIComponent(person.id)}`, { method: 'PUT', body: JSON.stringify({
   firstName: person.firstName, lastName: person.lastName, contactEmail: person.contactEmail, contactPhone: person.contactPhone,
   profileTypes: ['employee'], title: person.title, teams: personTeams(person), customerId: person.customerId,
   employeeRoleId: employeeRoleIds[0] || null, employeeRoleIds, modulePermissions: person.isOwner || employeeRoleIds.length ? null : [], expectedVersion: String(form.get('version') || '')
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
