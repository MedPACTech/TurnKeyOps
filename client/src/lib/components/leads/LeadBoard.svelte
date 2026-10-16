<script lang="ts">
 import PersonAvatar from '$lib/components/people/PersonAvatar.svelte';
 import { enhance } from '$app/forms';
 import { tick } from 'svelte';
 import { leadStages, stageLabel, type Lead, type LeadConfiguration } from '$lib/leads';
 let {leads,config,canWrite,basePath}:{leads:Lead[];config:LeadConfiguration;canWrite:boolean;basePath:string}=$props();
 let dragging=$state<string|null>(null), over=$state<string|null>(null), busy=$state(false);
 let pending=$state<{lead:Lead;stage:string}|null>(null);
 let moveForm=$state<HTMLFormElement>();
 const outcome=$derived(pending?.stage==='WON'||pending?.stage==='LOST');
 async function move(lead:Lead,stage:string){
  if(!canWrite||busy||pending||lead.stage===stage)return;
  pending={lead,stage};await tick();if(!outcome)moveForm?.requestSubmit();
 }
 function drop(event:DragEvent,stage:string){event.preventDefault();const lead=leads.find(l=>l.id===dragging);dragging=null;over=null;if(lead)void move(lead,stage);}
</script>
<p class="hint">{canWrite?'Drag a card to a status, or use its Move to control.':'Leads are grouped by their current status.'}</p>
{#if pending}
 <form bind:this={moveForm} method="POST" action="?/save" use:enhance={()=>{busy=true;return async({update})=>{try{await update({reset:false});}finally{busy=false;pending=null;}};}} class="move-form" aria-busy={busy}>
  <input type="hidden" name="action" value="stage"/><input type="hidden" name="id" value={pending.lead.id}/><input type="hidden" name="expectedVersion" value={pending.lead.version}/><input type="hidden" name="stage" value={pending.stage}/>
  <p>Move <strong>{pending.lead.title}</strong> to {stageLabel(pending.stage,config)}{busy?'…':'?'}</p>
  {#if outcome}<label>Outcome reason<select aria-label="Outcome reason" name="reason" required disabled={busy}><option value="">Choose reason</option>{#each pending.stage==='WON'?config.wonReasons:config.lostReasons as reason}<option>{reason}</option>{/each}</select></label><button disabled={busy}>Save status</button><button type="button" disabled={busy} onclick={()=>pending=null}>Cancel</button>{/if}
 </form>
{/if}
<div class="board" role="region" aria-label="Lead status board">
 {#each leadStages as stage}
  {@const items=leads.filter(l=>l.stage===stage)}
  <section class="column" class:over={over===stage} aria-label={`${stageLabel(stage,config)} status`} ondragover={event=>{if(canWrite&&dragging&&!busy&&!pending){event.preventDefault();over=stage;}}} ondragleave={event=>{if(!event.currentTarget.contains(event.relatedTarget as Node))over=null;}} ondrop={event=>drop(event,stage)}>
   <h2>{stageLabel(stage,config)} <span>{items.length}</span></h2>
   <div class="cards">{#each items as item (item.id)}
    <article class:dragging={dragging===item.id} draggable={canWrite&&!busy&&!pending} ondragstart={event=>{dragging=item.id;if(event.dataTransfer){event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/plain',item.id);}}} ondragend={()=>{dragging=null;over=null;}}>
     <a draggable="false" href={`${basePath}/${item.id}`}>{item.title}</a><p>{item.contactName}{item.companyName?` · ${item.companyName}`:''}</p>
     {#if item.estimatedValue!==null}<p>{new Intl.NumberFormat('en-US',{style:'currency',currency:'USD',maximumFractionDigits:0}).format(item.estimatedValue)}</p>{/if}
     <p class="owner"><PersonAvatar name={item.ownerName} identity={item.ownerProfileId || item.ownerMembershipId || ''} assigned={!!(item.ownerProfileId || item.ownerMembershipId)}/><span>{item.ownerName}</span></p><p>{item.nextAction}</p>
     {#if canWrite}<label>Move to<select aria-label={`Move ${item.title} to status`} value={item.stage} disabled={busy||!!pending} onchange={event=>{const target=event.currentTarget.value;event.currentTarget.value=item.stage;void move(item,target);}}>{#each leadStages as target}<option value={target}>{stageLabel(target,config)}</option>{/each}</select></label>{/if}
    </article>
   {/each}{#if !items.length}<p class="empty">{canWrite?'Drop a lead here':'No leads'}</p>{/if}</div>
  </section>
 {/each}
</div>
<style>
 .hint{font-size:.875rem;color:var(--lead-muted);margin-bottom:.75rem}.board{display:flex;gap:1rem;overflow-x:auto;max-width:100%;padding:.25rem .25rem 1rem;min-height:24rem}.column{flex:0 0 18rem;min-width:0;border:1px solid var(--shell-border,#d1d5db);border-radius:.75rem;background:var(--surface-elevated,var(--lead-surface))}.column.over{border-color:#0f766e;box-shadow:inset 0 0 0 2px #0f766e}h2{display:flex;justify-content:space-between;padding:1rem;font-size:.875rem;font-weight:650;border-bottom:1px solid var(--shell-border,#d1d5db)}h2 span{color:var(--lead-muted)}.cards{display:flex;flex-direction:column;gap:.75rem;padding:.75rem;min-height:18rem}article{display:flex;flex-direction:column;gap:.75rem;padding:1rem;border:1px solid var(--shell-border,#d1d5db);border-radius:.5rem;background:var(--lead-surface)}article[draggable=true]{cursor:grab}article.dragging{opacity:.45}a{font-weight:650;overflow-wrap:anywhere}a:hover{text-decoration:underline}p,label{font-size:.8rem;color:var(--lead-muted);overflow-wrap:anywhere}.owner{display:flex;align-items:center;gap:.5rem}.empty{text-align:center;padding:2rem .5rem;border:1px dashed var(--shell-border,#d1d5db);border-radius:.5rem}select{display:block;width:100%;min-height:44px;margin-top:.4rem;padding:.5rem;border:1px solid var(--shell-border-strong,#9ca3af);border-radius:.4rem;background:var(--lead-surface);color:inherit}.move-form{display:flex;flex-wrap:wrap;align-items:center;gap:1rem;padding:1rem;margin-bottom:1rem;border:1px solid #0f766e;border-radius:.5rem}.move-form button{min-height:44px;padding:.5rem 1rem;border:1px solid var(--shell-border-strong,#9ca3af);border-radius:.4rem}a:focus-visible,select:focus-visible,button:focus-visible,.board:focus-visible{outline:3px solid #0f766e;outline-offset:3px}
</style>
