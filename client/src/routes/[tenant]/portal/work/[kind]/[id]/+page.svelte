<script lang="ts">
 import {FileText,ArrowLeft} from 'lucide-svelte';
 let {data,form}=$props();let selected=$state<string[]>([]);let proposalKey=$state('');
 $effect(()=>{const key=`${data.id}:${data.detail.documentHash}`;if(key!==proposalKey){proposalKey=key;selected=(data.detail.options??[]).filter((o:any)=>o.required).map((o:any)=>o.id);}});let d=$derived(data.detail);let base=$derived(`/${data.tenant.slug}/portal`);let cfg=$derived(data.home.configuration);
 const money=(n:number)=>new Intl.NumberFormat('en-US',{style:'currency',currency:'USD'}).format(n);
</script>
<a href={base} class="inline-flex min-h-12 items-center gap-2"><ArrowLeft size={18}/>My work</a>
{#if form?.message}<p role="status" class="notice">{form.message}</p>{/if}
<h1>{d.title??d.serviceSummary??'Your proposal'}</h1><p class="muted">{d.status}{d.site?` · ${d.site}`:''}</p>
{#if d.nextStep}<p class="notice">{d.nextStep}</p>{/if}
<h2>{data.kind==='estimate'?'Proposed scope':'Your scope'}</h2><p class="prose">{d.scope||'Your contractor will share scope details here.'}</p>
{#if data.kind==='estimate'}
 <p class="muted">Revision {d.revisionNumber} · {d.expiresAtUtc?`Valid until ${new Date(d.expiresAtUtc).toLocaleDateString()}`:''}</p>
 <h2>Pricing and options</h2>{#each d.options??[] as o}<section class="row"><h3>{o.name} · {money(o.total)}</h3>{#each o.lines??[] as line}<p>{line.name} · {line.quantity} {line.unit} · {money(line.total)}</p>{/each}</section>{/each}
 <p><strong>Proposal total: {money(d.total)}</strong></p>
 <h2>Terms</h2><p class="prose">{d.terms}</p><h3>Exclusions</h3><p class="prose">{d.exclusions||'No exclusions listed.'}</p><h3>Timing</h3><p class="prose">{d.timing||'To be agreed with your contractor.'}</p>
 {#if d.signature}<p class="notice">Signed by {d.signature.signerPrintedName} on {new Date(d.signature.signedAtUtc).toLocaleString()} · Revision {d.signature.revisionNumber}</p>
 {:else if d.status==='sent'&&new Date(d.expiresAtUtc)>new Date()}
 <h2>Your decision</h2><form method="POST" action="?/proposal"><input type="hidden" name="revision" value={d.revisionNumber}/><input type="hidden" name="hash" value={d.documentHash}/><input type="hidden" name="consentVersion" value={d.consentVersion}/>
 <fieldset><legend>Choose your scope</legend>{#each d.options??[] as o}<label><input type="checkbox" name="options" value={o.id} bind:group={selected}/>{o.name} — {money(o.total)}{o.required?' (required)':''}{o.exclusiveGroup?` · Choose one from ${o.exclusiveGroup}`:''}</label>{/each}</fieldset>
 <p><strong>Selected scope total: {money((d.options??[]).filter((o:any)=>selected.includes(o.id)).reduce((sum:number,o:any)=>sum+o.total,0))}</strong></p>
 <label for="signer">Your full name for electronic signature</label><input id="signer" name="signer" maxlength="200" autocomplete="name"/>
 <label><input name="consent" type="checkbox"/>{d.consentText}</label>
 <label for="proposal-note">Question or reason for declining / requesting changes</label><textarea id="proposal-note" name="text" maxlength="2000"></textarea>
 <div class="actions"><button class="primary" name="decision" value="accept">Sign and accept proposal</button><button name="decision" value="request-change">Request change</button><button name="decision" value="decline">Decline proposal</button></div></form>{/if}
{/if}
{#if data.kind==='job'}
 {#if d.acceptedProposal}<details class="mt-6"><summary>Your approved proposal · revision {d.acceptedProposal.revision}</summary><p class="prose">{d.acceptedProposal.scope}</p><h3>Accepted terms</h3><p class="prose">{d.acceptedProposal.terms}</p><h3>Exclusions</h3><p class="prose">{d.acceptedProposal.exclusions}</p>{#each d.acceptedProposal.options??[] as option}<p>{option.name} · {money(option.total)}</p>{/each}{#if d.acceptedProposal.signer}<p>Signed by {d.acceptedProposal.signer} on {new Date(d.acceptedProposal.signedAtUtc).toLocaleDateString()}.</p>{/if}</details>{/if}
 {#if d.updates?.length}<h2>Updates</h2>{#each d.updates as u}<p class="row"><span class="muted">{new Date(u.atUtc).toLocaleDateString()}</span><br/>{u.text}</p>{/each}{/if}
 {#if d.changes?.length}<h2>Changes</h2>{#each d.changes as c}<section class="row"><h3>{c.description}</h3><p class="prose">{c.reason}</p><p>Scope: {c.scopeImpact}</p><p>Schedule: {c.scheduleImpact}</p><p>{c.status.replaceAll('_',' ').toLowerCase()}</p>
 {#if c.acceptedEstimateId}<a href={`${base}/work/estimate/${c.acceptedEstimateId}`}>Review change proposal — revision {c.acceptedRevision}</a>{/if}
 {#if c.status==='APPROVAL_REQUIRED'&&cfg.changeApprovalEnabled}<form method="POST" action="?/job"><input type="hidden" name="version" value={d.version}/><input type="hidden" name="itemId" value={c.id}/><input type="hidden" name="hash" value={c.hash}/><label><input type="checkbox" name="consent" required/>I have reviewed this change and confirm my decision.</label><div class="actions"><button class="primary" name="action" value="approve-change">Approve change</button><button name="action" value="decline-change">Decline change</button></div></form>{/if}</section>{/each}{/if}
 {#if d.completion?.ready}<h2>Completion review</h2><p>Review your completed scope above and the shared documents below.</p><form method="POST" action="?/job"><input type="hidden" name="version" value={d.version}/><input type="hidden" name="action" value="accept-completion"/><input type="hidden" name="hash" value={d.completion.hash}/><label for="completion-name">Your full name for electronic signature</label><input id="completion-name" name="signer" required maxlength="200" autocomplete="name"/>
 {#if d.completion.allowQualifiedAcceptance}<label for="exceptions">Exceptions, if any</label><textarea id="exceptions" name="text" maxlength="4000"></textarea>{/if}
 <label><input name="consent" type="checkbox" required/>{d.completion.statement}</label><div class="actions"><button class="primary">Sign and accept completion</button></div></form>{:else if d.completion?.acceptedAtUtc}<p class="notice">Completion accepted on {new Date(d.completion.acceptedAtUtc).toLocaleDateString()}.</p>{/if}
 <h2>Need something corrected?</h2><form method="POST" action="?/job"><input type="hidden" name="version" value={d.version}/><input type="hidden" name="action" value="report-issue"/><label for="issue">Describe the issue</label><textarea id="issue" name="text" required maxlength="4000"></textarea><div class="actions"><button>Report issue</button></div></form>
 {#each d.issues??[] as issue}<p class="row">{issue.description}<br/><span class="muted">{issue.status}</span></p>{/each}
{/if}
<h2 id="documents">Documents and photos</h2>
{#each d.files??[] as file}<a class="row flex min-h-12 items-center gap-2" href={`${base}/download/files/${data.kind}/${data.id}/${file.id}`}><FileText size={20}/>{file.name}</a>{/each}
{#each data.messages.filter((m:any)=>m.file) as m}<a class="row flex min-h-12 items-center gap-2" href={`${base}/download/messages/${data.kind}/${data.id}/files/${m.file.id}`}><FileText size={20}/>{m.file.name}</a>{/each}
{#if !(d.files?.length)&&!data.messages.some((m:any)=>m.file)}<p class="muted">No documents have been shared yet.</p>{/if}
{#if cfg.uploadEnabled}<form method="POST" action="?/upload" enctype="multipart/form-data"><label for="file">Upload a photo or document</label><input id="file" type="file" name="file" accept="image/jpeg,image/png,application/pdf" required aria-describedby="file-help"/><p id="file-help" class="muted">JPEG, PNG or PDF · up to 10 MB</p><div class="actions"><button>Upload file</button></div></form>{/if}
<h2 id="messages">Messages</h2>{#each data.messages.filter((m:any)=>!m.file) as m}<section class="row"><p class="muted">{m.from} · {new Date(m.atUtc).toLocaleString()}</p><p class="prose">{m.text}</p></section>{/each}
{#if cfg.messagingEnabled}<form method="POST" action="?/message"><label for="message">Your message to the contractor</label><textarea id="message" name="text" required maxlength="4000"></textarea><div class="actions"><button class="primary">Send message</button></div></form>{/if}
{#if cfg.customerBobEnabled}<details class="mt-8"><summary>Help with next steps</summary><p>Your project information above is what your contractor has shared. Bob can explain the available actions.</p><form method="POST" action="?/explain"><button>Ask Bob about this work</button></form></details>{/if}
