<script lang="ts">
 import {money,type EstimatePacket} from '$lib/estimates';
 let {packet}:{packet:EstimatePacket}=$props();
</script>
<article class="proposal" aria-label="Customer-facing proposal">
 <header><p class="font-semibold">{packet.document?.companyName||'Project proposal'}</p><h2 class="mt-2 text-2xl font-semibold">Proposal · revision {packet.revisionNumber}</h2><p class="mt-3">Prepared for {packet.customerName}</p><p>{packet.siteName}</p>{#if packet.expiresAtUtc}<p>Valid through {new Date(packet.expiresAtUtc).toLocaleDateString()}</p>{:else}<p>Valid for {packet.document?.validDays??30} days from issue</p>{/if}</header>
 <section class="mt-6"><h3 class="font-semibold">Scope of work</h3><p class="mt-2 whitespace-pre-wrap">{packet.document?.scope||packet.serviceSummary}</p></section>
 {#each packet.pricing?.options??[] as option}<section class="mt-6"><h3 class="font-semibold">{option.name} · {option.required?'Included scope':'Optional upgrade'}</h3><ul class="my-3 space-y-2">{#each option.lines as line}<li class="flex flex-wrap justify-between gap-2"><span>{line.name} · {line.quantity} {line.unit}</span><span>{money(line.total)}</span></li>{/each}</ul><p>Subtotal {money(option.subtotal)}</p>{#if option.discount}<p>Discount −{money(option.discount)}</p>{/if}<p>Tax {money(option.tax)}</p><p class="mt-1 font-semibold">{option.name}: {money(option.total)}</p></section>{/each}
 {#if packet.document&&Object.keys(packet.document.inputs).length}<section class="mt-6"><h3 class="font-semibold">Confirmed measurements</h3><ul>{#each Object.entries(packet.document.inputs).filter(([,value])=>value.confirmed) as [key,value]}<li>{key}: {value.value}</li>{/each}</ul></section>{/if}
 <p class="mt-6 text-xl font-semibold">Required scope total: {money(packet.totals.estimatedTotal)}</p>
 {#if packet.document?.timing}<section class="mt-6"><h3 class="font-semibold">Timing & commitments</h3><p class="whitespace-pre-wrap">{packet.document.timing}</p></section>{/if}
 {#if packet.document?.exclusions}<section class="mt-6"><h3 class="font-semibold">Exclusions</h3><p class="whitespace-pre-wrap">{packet.document.exclusions}</p></section>{/if}
 <section class="mt-6"><h3 class="font-semibold">Terms</h3><p class="whitespace-pre-wrap">{packet.document?.terms||'Review terms with the office before approval.'}</p><p class="mt-2">Deposit: {packet.document?.depositPercent??0}% of the accepted scope total.</p></section>
 {#if packet.document?.attachments.length}<section class="mt-6"><h3 class="font-semibold">Included attachments</h3><ul>{#each packet.document.attachments as file}<li>{file.name}</li>{/each}</ul></section>{/if}
 {#if packet.approvalSignature}<section class="mt-6"><h3 class="font-semibold">Signed acceptance</h3><p>{packet.approvalSignature.signerPrintedName} · {new Date(packet.approvalSignature.signedAtUtc).toLocaleString()}</p><p>Accepted total: {money(packet.approvalSignature.total)}</p><p>Revision {packet.approvalSignature.revisionNumber}</p></section>{/if}
 <p class="mt-6 break-all text-xs">Document reference: {packet.documentHash||'Draft — not issued'}</p>
</article>
<style>.proposal{color:var(--text-strong,#17212b);max-width:52rem;margin:auto;font-size:1rem;line-height:1.6}@media print{.proposal{color:#111!important;background:white!important}section{break-inside:avoid}}</style>
