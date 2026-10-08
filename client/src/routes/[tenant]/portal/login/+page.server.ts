import {env} from '$env/dynamic/private';
import {fail,redirect} from '@sveltejs/kit';
import {startOtp,completeOtp,extractAccessToken} from '$lib/server/auth-session';
import {portalTenant,portalCookie,formText} from '$lib/server/portal';
import {getTurnKeyApiBaseUrl} from '$lib/server/turnkey-api';
export const actions={
 request:async(event)=>{portalTenant(event);const f=await event.request.formData();const identifier=formText(f,'identifier');try{
  // Tenant-neutral identity verification deliberately does not select employee membership.
  const state=await startOtp(event.fetch,identifier);return {identifier,challengeId:state.challengeId,message:'Check your email or phone for your sign-in code.'};
 }catch{return fail(400,{message:'Unable to request a code. Check your email or phone and try again.'});}},
 verify:async(event)=>{const tenant=portalTenant(event),f=await event.request.formData(),identifier=formText(f,'identifier'),challengeId=formText(f,'challengeId');
  try{const result=await completeOtp(event.fetch,identifier,formText(f,'code'),challengeId);const token=extractAccessToken(result)??(typeof result.preTenantToken==='string'?result.preTenantToken:null);
   if(!token)throw new Error('No identity');
   const response=await event.fetch(`${getTurnKeyApiBaseUrl()}/api/portal-identity/${tenant.slug}/session`,{method:'POST',headers:{Authorization:`Bearer ${token}`}});
   if(!response.ok)throw new Error('Unavailable');const session=await response.json();
   if(typeof session.token!=='string'||!/^[0-9A-F]{64}$/.test(session.token))throw new Error('Invalid session');
   event.cookies.set(portalCookie(tenant.slug),session.token,{path:`/${tenant.slug}/portal`,httpOnly:true,sameSite:'strict',secure:env.NODE_ENV==='production'||event.url.protocol==='https:',maxAge:8*3600});
  }catch{return fail(401,{identifier,challengeId,message:'Sign-in could not be completed. Request a new code, or ask your contractor to enable your customer access.'});}
  redirect(303,`/${tenant.slug}/portal`);
 }
};
