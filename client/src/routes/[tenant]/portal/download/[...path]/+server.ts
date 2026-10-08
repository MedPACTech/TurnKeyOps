import {error,redirect} from '@sveltejs/kit';
import {portalTenant,portalCookie} from '$lib/server/portal';
import {getTurnKeyApiBaseUrl} from '$lib/server/turnkey-api';
export const GET=async(event)=>{const t=portalTenant(event),token=event.cookies.get(portalCookie(t.slug));if(!token)redirect(303,`/${t.slug}/portal/login`);
 const path=event.params.path??'';if(!/^(files\/(job|estimate)\/[a-f0-9-]{36}\/[a-f0-9-]{36}|messages\/(job|lead|estimate)\/[a-f0-9-]{36}\/files\/[a-f0-9-]{36})$/i.test(path))error(404);
 const r=await event.fetch(`${getTurnKeyApiBaseUrl()}/api/portal/${t.slug}/${path}`,{headers:{'X-Portal-Session':token},cache:'no-store'});if(!r.ok)error(r.status,'File unavailable');
 return new Response(r.body,{headers:{'Content-Type':r.headers.get('content-type')??'application/octet-stream','Content-Disposition':r.headers.get('content-disposition')??'attachment','Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff','Content-Security-Policy':"sandbox; default-src 'none'"}});
};
