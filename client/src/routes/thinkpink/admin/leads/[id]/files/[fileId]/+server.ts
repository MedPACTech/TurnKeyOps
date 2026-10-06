import { error } from '@sveltejs/kit';
import { authTokenCookie } from '$lib/server/auth-session';
import { getTurnKeyApiBaseUrl } from '$lib/server/turnkey-api';
export const GET = async ({params, cookies, fetch, locals}) => {
 const token=cookies.get(authTokenCookie);
 if (!token || !locals.adminSession) throw error(401,'Sign in to view files.');
 const response=await fetch(`${getTurnKeyApiBaseUrl()}/api/leads/${params.id}/files/${params.fileId}`,{headers:{Authorization:`Bearer ${token}`}});
 if(!response.ok) throw error(response.status,'File is unavailable.');
 return new Response(response.body,{headers:{'Content-Type':response.headers.get('Content-Type')||'application/octet-stream','Content-Disposition':response.headers.get('Content-Disposition')||'attachment','Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'}});
};
