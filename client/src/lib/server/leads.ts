import { error, fail, redirect, type RequestEvent } from '@sveltejs/kit';
import { authTokenCookie } from '$lib/server/auth-session';
import { getTurnKeyApiBaseUrl } from '$lib/server/turnkey-api';
import type { Lead, LeadWorkspace, Duplicate } from '$lib/leads';

export const leadApi = async <T>(event: RequestEvent, path: string, body?: unknown, method = 'POST'): Promise<T> => {
 const token = event.cookies.get(authTokenCookie);
 if (!token || !event.locals.adminSession) throw error(401, 'Sign in to use Leads.');
 const multipart = body instanceof FormData;
 const response = await event.fetch(`${getTurnKeyApiBaseUrl()}/api/${path}`, {
  method: body === undefined ? 'GET' : method,
  headers: { Authorization: `Bearer ${token}`, Accept: 'application/json', ...(body !== undefined && !multipart ? {'Content-Type':'application/json'} : {}) },
  body: body === undefined ? undefined : multipart ? body : JSON.stringify(body)
 });
 const payload = await response.json().catch(() => null);
 if (!response.ok) throw error(response.status, payload?.errors?.[0]?.message || payload?.message || `Lead operation failed (${response.status}). Refresh and try again.`);
 return payload && typeof payload === 'object' && 'data' in payload ? payload.data : payload;
};
export const loadLeads = async (event: RequestEvent) => {
 event.setHeaders({'Cache-Control':'private, no-store'});
 const workspace = await leadApi<LeadWorkspace>(event, 'leads');
 const id = event.params.id || event.url.searchParams.get('lead');
 const selected = id ? await leadApi<Lead>(event, `leads/${encodeURIComponent(id)}`) : null;
 const duplicates = selected ? await leadApi<Duplicate[]>(event, `leads/${selected.id}/duplicates`) : [];
 const intake = selected?.intakeRequestId ? await leadApi<{attachments:{id:string; fileName:string}[]; timeline:{id:string; label:string; occurredAtUtc:string}[]} | null>(event, `leads/${selected.id}/intake`) : null;
 return {workspace, selected, duplicates, intake, basePath: event.url.pathname.split('/leads')[0] + '/leads'};
};
const string = (form:FormData, key:string) => String(form.get(key) ?? '').trim();
const nullable = (form:FormData, key:string) => string(form,key) || null;
const assignment = (form:FormData) => {
 const value = string(form,'assignedAssociate');
 return {ownerProfileId:value.startsWith('profile:') ? value.slice(8) : null,
  ownerMembershipId:value.startsWith('member:') ? value.slice(7) : null};
};
const editable = (form:FormData) => ({
 title:string(form,'title'), customerId:nullable(form,'customerId'), contactName:string(form,'contactName'), companyName:string(form,'companyName'),
 email:string(form,'email'), phone:string(form,'phone'), siteAddress:string(form,'siteAddress'), requestedWork:string(form,'requestedWork'),
 source:string(form,'source') || 'Manual', tradeProfile:string(form,'tradeProfile') || 'general', service:string(form,'service'), propertyType:string(form,'propertyType'),
 ...assignment(form), estimatedValue:string(form,'estimatedValue') ? Number(string(form,'estimatedValue')) : null,
 referralName:string(form,'referralName'), referralContactId:nullable(form,'referralContactId'), nextAction:string(form,'nextAction'),
 followUpAtUtc:nullable(form,'followUpAtUtc'),
 qualification:Object.fromEntries([...form.entries()].filter(([key])=>key.startsWith('q.')).map(([key,value])=>[key.slice(2),String(value)]))
});
export const leadActions = {
 save: async (event:RequestEvent) => {
  const form = await event.request.formData();
  const action = string(form,'action'); const id = string(form,'id'); const expectedVersion = string(form,'expectedVersion');
  try {
   if (action === 'create') {
    const created = await leadApi<Lead>(event,'leads',{...editable(form), id:string(form,'creationId'), createCustomer:form.get('createCustomer') === 'on'});
    return {success:true, message:'Lead created.', createdId:created.id};
   }
   if (action === 'reconcile') {const count = await leadApi<number>(event,'leads/reconcile-intake',{}); return {success:true,message:`${count} intake records linked to Leads.`};}
   if (!/^[0-9a-f-]{36}$/i.test(id)) throw error(400,'Select a lead.');
   if (action === 'bob') {
    const result=await leadApi<{id:string;status:string}>(event,`leads/${id}/bob`,{toolKey:'lead.assign',idempotencyKey:`lead.assign:${id}:${expectedVersion}`,input:{leadId:id,expectedVersion}});
    return {success:true,message:result.status==='completed'?'Bob applied the assignment rules.':'Bob’s assignment is ready for your approval.',bobActionId:result.status==='completed'?null:result.id};
   }
   if (action === 'bob-approve') {await leadApi(event,`leads/${id}/bob/${string(form,'bobActionId')}/approve`,{});return {success:true,message:'Bob completed the approved action.'};}
   if (action === 'associate' || action === 'update'  || action === 'link' || action === 'qualify') {
    const current = await leadApi<Lead>(event,`leads/${id}`);
    await leadApi(event,`leads/${id}`,{...current,...(action === 'associate' ? assignment(form) : action === 'update' ? editable(form) : action === 'link' ? {customerId:string(form,'customerId')} : {requestedWork:string(form,'requestedWork') || current.requestedWork,siteAddress:string(form,'siteAddress') || current.siteAddress,email:string(form,'email') || current.email,qualification:{...current.qualification,...editable(form).qualification}}),expectedVersion},'PUT');
   } else if (action === 'stage') await leadApi(event,`leads/${id}/stage`,{stage:string(form,'stage'),reason:string(form,'reason'),expectedVersion});
   else if (action === 'note') await leadApi(event,`leads/${id}/activity`,{text:string(form,'text'),type:string(form,'type') || 'note',expectedVersion});
   else if (['assign','estimate','job','customer'].includes(action)) await leadApi(event,`leads/${id}/${action}`,{expectedVersion});
   else if (action === 'schedule') await leadApi(event,`leads/${id}/schedule`,{startUtc:string(form,'startUtc'),endUtc:string(form,'endUtc'),expectedVersion});
   else if (action === 'draft' || action === 'send') await leadApi(event,`leads/${id}/${action}`,{channel:string(form,'channel'),subject:string(form,'subject'),body:string(form,'body'),expectedVersion});
   else if (action === 'files') { const payload=new FormData();payload.set('expectedVersion',expectedVersion); for(const file of form.getAll('files')) payload.append('files',file); await leadApi(event,`leads/${id}/files`,payload); }
   else throw error(400,'Unsupported lead action.');
   return {success:true,message:'Saved.'};
  } catch (caught) {
   const problem = caught as {status?:number; body?:{message?:string}; message?:string};
   return fail(problem.status && problem.status >= 400 && problem.status < 600 ? problem.status : 500,{success:false,message:problem.body?.message || 'Could not save. Your changes have not been confirmed; refresh before retrying.'});
  }
 }
};
