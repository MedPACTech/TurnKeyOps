import {redirect} from '@sveltejs/kit';
import {portalRequest,portalTenant,portalCookie,actionFailure,formText} from '$lib/server/portal';
export const load=async(event)=>({home:await portalRequest(event)});
export const actions={
 preferences:async(event)=>{try{const f=await event.request.formData();await portalRequest(event,'/preferences',{method:'POST',body:JSON.stringify(f.getAll('channels').map(String))});return {message:'Notification preferences saved.'};}catch(e){return actionFailure(e);}},
 appointment:async(event)=>{try{const f=await event.request.formData();await portalRequest(event,`/appointments/${encodeURIComponent(formText(f,'id'))}`,{method:'POST',body:JSON.stringify({expectedVersion:formText(f,'version'),action:formText(f,'response'),text:formText(f,'text'),slotId:formText(f,'slotId')||null})});return {message:'Your appointment response has been recorded.'};}catch(e){return actionFailure(e);}},
 logout:async(event)=>{await portalRequest(event,'/logout',{method:'POST'});const t=portalTenant(event);event.cookies.delete(portalCookie(t.slug),{path:`/${t.slug}/portal`});redirect(303,`/${t.slug}/portal/login`);}
};
