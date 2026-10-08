import { error, json, type RequestEvent } from '@sveltejs/kit';
import { peopleRequest } from './people';

type Grant = { id: string; userId: string; customerId: string; scope: string; recordId: string; expiresAtUtc: string; revoked: boolean };
type State = { version: string; configuration: { enabled: boolean }; grants: Grant[]; people: { userId: string; customerId: string }[] };
const read = (event: RequestEvent) => peopleRequest<State>(event, '/api/admin/portal');
const response = (value: unknown) => json(value, { headers: { 'Cache-Control': 'private, no-store' } });
function contact(state: State, id: string | undefined) {
 const person = state.people.find(p => p.userId === id);
 if (!person) error(404, 'Save an active Customer profile linked to a customer record first.');
 return person;
}
export async function GET(event: RequestEvent) {
 const state = await read(event), person = contact(state, event.params.id);
 const workspace = await peopleRequest<{work: {id:string;kind:string;title:string;siteId?:string;siteName?:string}[]}>(event, `/api/admin/portal/workspace/${person.customerId}`);
 const options = workspace.work.map(w => ({scope:w.kind, id:w.id, label:w.title || 'Untitled work'}));
 const sites = new Map(workspace.work.filter(w => w.siteId).map(w => [w.siteId!, w.siteName || 'Project site']));
 options.push(...[...sites].map(([id,label]) => ({scope:'site',id,label})));
 return response({version:state.version, enabled:state.configuration.enabled, grants:state.grants.filter(g=>g.userId===person.userId), options});
}
export async function POST(event: RequestEvent) {
 // JSON endpoint: enforce same-origin mutations independently of form CSRF handling.
 if (event.request.headers.get('origin') !== event.url.origin) error(403, 'Use this company workspace to change access.');
 const input = await event.request.json();
 const state = await read(event), person = contact(state, event.params.id);
 if (input.version !== state.version) error(409, 'Access changed. Reload the access section before saving.');
 if (input.action === 'disable') {
  await peopleRequest(event, `/api/admin/portal/contacts/${person.userId}/revoke`, {method:'POST',body:JSON.stringify({expectedVersion:input.version})});
 } else if (input.action === 'revoke') {
  const grant = state.grants.find(g=>g.id===input.id && g.userId===person.userId);
  if (!grant) error(404, 'Access grant not found for this contact.');
  await peopleRequest(event, `/api/admin/portal/grants/${grant.id}/revoke`, {method:'POST',body:JSON.stringify({expectedVersion:input.version})});
 } else if (input.action === 'grant') {
  await peopleRequest(event, '/api/admin/portal/grants', {method:'POST',body:JSON.stringify({expectedVersion:input.version,grant:{userId:person.userId,customerId:person.customerId,scope:input.scope,recordId:input.scope==='customer'?person.customerId:input.recordId,expiresAtUtc:input.expiresAtUtc}})});
 } else error(400, 'Choose a valid portal access action.');
 return response({message:input.action==='grant'?'Portal access granted.':'Portal access revoked.'});
}
