<script lang="ts">
 import { page } from '$app/state';
 type Grant = {id:string; customerId:string; scope:string; recordId:string; expiresAtUtc:string; revoked:boolean};
 type Access = {version:string; enabled:boolean; grants:Grant[]; options:{scope:string;id:string;label:string}[]};
 let {personId, customerId, active=true}: {personId:string;customerId?:string;active?:boolean} = $props();
 let access = $state<Access|null>(null), loading=$state(false), pending=$state(false), error=$state(''), message=$state('');
 let adding=$state(false), scope=$state('customer'), recordId=$state(''), expires=$state(''), confirmDisable=$state(false);
 let revision=$state(0);
 const slug=$derived(page.url.pathname.split('/')[1]);
 const endpoint=$derived(`/${slug}/admin/settings/portal/contacts/${personId}`);
 const loginPath=$derived(`/${slug}/portal/login`);
 const current=$derived(access?.grants.filter(g=>!g.revoked && Date.parse(g.expiresAtUtc)>Date.now() && g.customerId===customerId)??[]);
 const status=$derived(!active?'Inactive':current.length?'Active':access?.grants.some(g=>!g.revoked&&g.customerId===customerId)?'Expired':access?.grants.length?'Revoked':'Inactive');
 $effect(()=>{
  const url=endpoint, linked=customerId, enabled=active; revision;
  const abort=new AbortController(); access=null; error=''; adding=false; confirmDisable=false;
  if(!linked||!enabled)return;
  loading=true;
  fetch(url,{signal:abort.signal,cache:'no-store'}).then(async r=>{const data=await r.json();if(!r.ok)throw Error(data.message??'Could not load portal access.');return data;})
   .then(data=>{if(!abort.signal.aborted)access=data;}).catch(e=>{if(!abort.signal.aborted)error=e.message;}).finally(()=>{if(!abort.signal.aborted)loading=false;});
  return ()=>abort.abort();
 });
 async function change(input:Record<string,unknown>) {
  if(!access||pending)return;
  pending=true;error='';message='';
  try {
   const r=await fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({...input,version:access.version})});
   const result=await r.json();if(!r.ok)throw Error(result.message??'Could not change portal access.');
   message=result.message;revision++;
  }catch(e){error=e instanceof Error?e.message:'Could not change portal access.';}
  finally{pending=false;}
 }
 function grant(event:SubmitEvent){event.preventDefault();if(!expires)return;void change({action:'grant',scope,recordId,expiresAtUtc:new Date(`${expires}T23:59:59`).toISOString()});}
 async function copyLogin(){try{await navigator.clipboard.writeText(`Sign in to your customer portal at ${new URL(loginPath,location.origin)} using your verified email or phone. Enter the one-time code to continue.`);message='Login instructions copied. No invitation has been sent.';}catch{error='Could not copy. Open the customer login link to copy its address.';}}
 const label=(g:Grant)=>g.scope==='customer'?'All work for linked customer':access?.options.find(o=>o.scope===g.scope&&o.id===g.recordId)?.label??`${g.scope} (no longer available)`;
</script>

<section class="portal-access" aria-labelledby={`portal-access-${personId}`}>
 <h3 id={`portal-access-${personId}`}>Customer Portal</h3>
 <p class="help">Portal access is separate from employee roles and module permissions. The contact verifies their identity when signing in.</p>
 {#if message}<p role="status">{message}</p>{/if}
 {#if error}<p role="alert" class="error">{error}</p><button class="btn-secondary" type="button" onclick={()=>revision++}>Reload access</button>{/if}
 {#if !active}<p>Restore this contact before granting portal access.</p>
 {:else if !customerId}<p>Save this contact with a linked customer record before granting portal access.</p>
 {:else if loading}<p role="status">Loading portal access…</p>
 {:else if access}
  <p><strong>Status: {status}</strong>{#if !access.enabled} · Company portal is off{/if}</p>
  {#if !access.enabled}<p class="help">Grants are saved, but customers cannot sign in until the portal is enabled in <a href={`/${slug}/admin/settings/portal`}>company portal settings</a>.</p>{/if}
  <label class="toggle"><input type="checkbox" checked={current.length>0||adding} disabled={pending} onchange={event=>{if(event.currentTarget.checked){adding=true;}else if(current.length){event.currentTarget.checked=true;confirmDisable=true;}else adding=false;}}/>Allow portal access</label>
  {#if confirmDisable}<div class="confirmation"><p>Revoke all portal access and end this contact’s portal sessions?</p><div class="actions"><button type="button" class="btn-danger" disabled={pending} onclick={()=>change({action:'disable'})}>Revoke all portal access</button><button type="button" class="btn-secondary" onclick={()=>confirmDisable=false}>Keep access</button></div></div>{/if}
  {#if current.length}<button type="button" class="btn-secondary" disabled={pending} onclick={()=>adding=!adding}>{adding?'Cancel additional access':'Add access scope'}</button>{/if}
  {#if adding}<form onsubmit={grant}>
   <fieldset disabled={pending}><legend class="sr-only">Grant customer portal access</legend>
    <label>Access scope<select bind:value={scope} onchange={()=>recordId=''}><option value="customer">All work for linked customer</option><option value="site">One site</option><option value="job">One project</option><option value="lead">One request</option><option value="estimate">One proposal</option></select></label>
    {#if scope!=='customer'}<label>Work or site<select bind:value={recordId} required><option value="">Choose {scope==='site'?'a site':'work'}</option>{#each access.options.filter(o=>o.scope===scope) as option}<option value={option.id}>{option.label}</option>{/each}</select></label>{#if !access.options.some(o=>o.scope===scope)}<p class="help">No eligible {scope==='site'?'sites':'work'} for this customer yet.</p>{/if}{/if}
    <label>Access expires<input type="date" bind:value={expires} required/></label><p class="help">Choose a date within two years. Access ends at the end of that date in your local time.</p>
    <button class="btn-primary" type="submit">{pending?'Saving…':'Grant portal access'}</button>
   </fieldset>
  </form>{/if}
  {#if access.grants.length}<ul class="grants">{#each access.grants as g}<li><div><strong>{label(g)}</strong><p>{g.revoked?'Revoked':g.customerId!==customerId?'Inactive — previous customer link':Date.parse(g.expiresAtUtc)<=Date.now()?'Expired':'Active'} · Expires {new Date(g.expiresAtUtc).toLocaleDateString()}</p></div>{#if !g.revoked}<button class="btn-secondary" type="button" disabled={pending} aria-label={`Revoke ${label(g)}`} onclick={()=>change({action:'revoke',id:g.id})}>Revoke</button>{/if}</li>{/each}</ul>{/if}
  <div class="actions"><a href={loginPath} target="_blank" rel="noreferrer">Open customer login</a><button class="btn-secondary" type="button" onclick={copyLogin}>Copy login instructions</button></div>
 {/if}
</section>
<style>
 .portal-access{border-top:1px solid var(--border);margin-top:1.5rem;padding-top:1.5rem}h3{font-weight:700;margin-bottom:.75rem}p{margin:.5rem 0;overflow-wrap:anywhere}.help{font-size:.85rem;color:var(--text-muted)}.error{color:var(--critical-text)}label{display:flex;flex-direction:column;gap:.4rem;margin:.9rem 0;font-size:.9rem}.toggle{flex-direction:row;align-items:center;min-height:44px}input,select{min-height:44px;max-width:100%;width:100%;border:1px solid var(--input-border);border-radius:6px;padding:.65rem;background:var(--surface);color:var(--text-strong)}input[type=checkbox]{width:20px;min-height:20px;height:20px}fieldset{min-width:0}.actions{display:flex;align-items:center;flex-wrap:wrap;gap:.75rem;margin-top:1rem}a{text-decoration:underline;display:inline-flex;align-items:center;min-height:44px}.grants{margin:1rem 0}.grants li{display:flex;align-items:center;justify-content:space-between;gap:1rem;border-bottom:1px solid var(--border);padding:.8rem 0}.grants li>div{min-width:0}.grants p{font-size:.85rem;color:var(--text-muted)}.confirmation{border-left:3px solid var(--critical-text);padding-left:1rem}button{white-space:normal}
</style>
