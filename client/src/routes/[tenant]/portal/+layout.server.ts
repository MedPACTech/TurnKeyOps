import {portalTenant} from '$lib/server/portal';
export const load=async(event)=>{event.setHeaders({'Cache-Control':'private, no-store','Referrer-Policy':'no-referrer'});return {tenant:portalTenant(event)};};
