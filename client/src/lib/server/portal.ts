import { error, redirect, fail, isHttpError, type RequestEvent } from '@sveltejs/kit';
import { getTenant } from '$lib/config/tenants';
import { getTurnKeyApiBaseUrl } from '$lib/server/turnkey-api';
export const portalCookie = (slug: string) => `tko_portal_${slug}`;
export const portalTenant = (event: Pick<RequestEvent,'params'>) => {
 const tenant=getTenant(event.params.tenant??''); if(!tenant)error(404,'Unknown company'); return tenant;
};
export async function portalRequest<T=any>(event: RequestEvent,path='',init: RequestInit={}):Promise<T> {
 const tenant=portalTenant(event),token=event.cookies.get(portalCookie(tenant.slug));
 if(!token)redirect(303,`/${tenant.slug}/portal/login`);
 const headers=new Headers(init.headers);headers.set('X-Portal-Session',token);
 if(init.body&&!(init.body instanceof FormData))headers.set('Content-Type','application/json');
 const response=await event.fetch(`${getTurnKeyApiBaseUrl()}/api/portal/${tenant.slug}${path}`,{...init,headers,cache:'no-store'});
 if(response.status===401){event.cookies.delete(portalCookie(tenant.slug),{path:`/${tenant.slug}/portal`});redirect(303,`/${tenant.slug}/portal/login`);}
 if(!response.ok){let message='Unable to complete this action. Please try again.';try{message=(await response.json()).message??message;}catch{}error(response.status,message);}
 return response.status===204?undefined as T:await response.json();
}
export const portalLoad=async(event: RequestEvent)=>{
 event.setHeaders({'Cache-Control':'private, no-store','Referrer-Policy':'no-referrer'});
 return {tenant:portalTenant(event),home:await portalRequest(event)};
};
export const actionFailure=(cause: unknown)=>{if(isHttpError(cause))return fail(cause.status,{message:cause.body.message});throw cause;};
export const formText=(f:FormData,key:string)=>String(f.get(key)??'');
