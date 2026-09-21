import { error, fail } from '@sveltejs/kit';
import { authTokenCookie, getAuthApiBaseUrl } from './auth-session';
import type { RequestEvent } from '@sveltejs/kit';

export type Person = { id: string; firstName: string; lastName: string; contactEmail?: string; contactPhone?: string;
 profileTypes: string[]; companyName?: string; title?: string; team?: string; customerId?: string;
 modulePermissions: string[] | null; effectivePermissions: string[]; role?: string; membershipId?: string;
 isOwner: boolean; isActive: boolean; version: string };
export const modules = ['dashboard','bob','calendar','jobs','requests','estimates','invoices','contacts','users','settings','billing'];
export async function peopleRequest<T>(event: Pick<RequestEvent, 'fetch' | 'cookies'>, path: string, init?: RequestInit): Promise<T> {
 const token = event.cookies.get(authTokenCookie);
 if (!token) throw error(401, 'Sign in to continue.');
 const response = await event.fetch(`${getAuthApiBaseUrl()}${path}`, { ...init, headers: {
  Accept: 'application/json', Authorization: `Bearer ${token}`, ...(init?.body ? {'Content-Type':'application/json'} : {})
 }});
 if (response.status === 204) return undefined as T;
 const payload = await response.json().catch(() => null);
 if (!response.ok) throw error(response.status, payload?.error || payload?.errors?.map((e: {message?: string}) => e.message).join(', ') || payload?.message || 'Could not save the user.');
 return payload && 'data' in payload ? payload.data : payload;
}
export const loadPeople = async (event: RequestEvent) => ({...(await peopleRequest<{canDeleteUsers:boolean}>(event, '/api/people/capabilities')), people: await peopleRequest<Person[]>(event, '/api/people'), customerLinks: await peopleRequest<{id:string;name:string;companyName?:string}[]>(event, '/api/people/customers')});
const text = (d: FormData, key: string) => String(d.get(key) ?? '').trim();
async function action(event: RequestEvent, operation: (d: FormData) => Promise<Record<string, unknown>>) {
 try { return await operation(await event.request.formData()); }
 catch (cause) { const c = cause as {status?: number; body?: {message?: string}; message?: string};
  return fail(c.status && c.status >= 400 && c.status <= 599 ? c.status : 400, {error: c.body?.message || c.message || 'Could not complete this change.'}); }
}
export const peopleActions = {
 deletePerson:(event: RequestEvent) => action(event,async d => {
  await peopleRequest(event,`/api/people/${encodeURIComponent(text(d,'id'))}/delete?version=${encodeURIComponent(text(d,'version'))}`,{method:'POST'});
  return {message:'User deleted from this company. App access was removed; business history is retained.'};
 }),
 updatePersonRole:(event: RequestEvent) => action(event,async d => {
  await peopleRequest(event,`/api/people/${encodeURIComponent(text(d,'id'))}/role`,{method:'POST',body:JSON.stringify({role:text(d,'role')})});
  return {message:'User access role updated.'};
 }),
 savePerson: (event: RequestEvent) => action(event, async d => {
  const id = text(d,'id');
  const permissions = text(d,'permissionMode') === 'default' ? null : modules.flatMap(m => {
   const level = text(d,`module:${m}`); return level === 'write' ? [`${m}.read`,`${m}.write`] : level === 'read' ? [`${m}.read`] : [];
  });
  const person = await peopleRequest<Person>(event, `/api/people${id ? '/'+encodeURIComponent(id) : ''}`, {method:id ? 'PUT':'POST', body:JSON.stringify({
   firstName:text(d,'firstName'),lastName:text(d,'lastName'),contactEmail:text(d,'contactEmail') || null,contactPhone:text(d,'contactPhone') || null,
   loginIdentifier:text(d,'loginIdentifier') || null,profileTypes:d.getAll('profileTypes').map(String),companyName:text(d,'companyName'),title:text(d,'title'),team:text(d,'team'),
   customerId:text(d,'customerId') || null,modulePermissions:permissions,expectedVersion:text(d,'version')
  })});
  return {message:'User saved.',savedId:person.id};
 }),
 archivePerson:(event: RequestEvent) => action(event,async d => {
  await peopleRequest(event,`/api/people/${encodeURIComponent(text(d,'id'))}?version=${encodeURIComponent(text(d,'version'))}`,{method:'DELETE'});
  return {message:'User archived and company access removed.'};
 }),
 restorePerson:(event: RequestEvent) => action(event,async d => {
  await peopleRequest(event,`/api/people/${encodeURIComponent(text(d,'id'))}/restore?version=${encodeURIComponent(text(d,'version'))}`,{method:'POST'});
  return {message:'User restored. Sign-in access requires a new invitation.'};
 }),
 invitePerson:(event: RequestEvent) => action(event,async d => {
  const invite = await peopleRequest<{id:string;inviteToken?:string}>(event,`/api/people/${encodeURIComponent(text(d,'id'))}/invite`,{method:'POST',body:JSON.stringify({role:text(d,'role')})});
  return {message:'Invitation created.',inviteUrl:invite.inviteToken ? new URL(`/auth/invite/${invite.id}?token=${encodeURIComponent(invite.inviteToken)}`,event.url.origin).toString() : undefined};
 })
};
