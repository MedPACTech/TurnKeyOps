<script lang="ts">
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';
	let { data, form }: PageProps = $props();
	let saving = $state(false);
	const invoice = $derived(form?.signedInvoice || data.invoice);
	const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
	const signedDate = (value: string) => new Date(value).toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' });
	const consent = 'I confirm that the work described on this invoice has been completed. I intend my typed name to record my acknowledgement of that work.';
</script>

<svelte:head><title>{invoice.invoiceNumber} · Carl Zipf</title></svelte:head>
<main class="mx-auto max-w-3xl space-y-5 px-4 py-7 pb-20 text-slate-900">
	<a href="/carlzipf/tech/invoices" class="text-sm font-semibold text-emerald-900 underline">← Field invoices</a>
	<header class="rounded-xl border bg-white p-5"><p class="text-sm font-semibold text-emerald-900">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">{invoice.invoiceNumber}</h1><p class="mt-2 text-slate-600">{invoice.customerName || 'Customer'} · {invoice.siteName || invoice.serviceSummary || 'Service work'}</p><p class="mt-3 text-sm font-semibold uppercase tracking-wide text-slate-500">{invoice.status}</p></header>
	<section aria-label="Invoice details" class="rounded-xl border bg-white p-5"><h2 class="text-lg font-bold">Work and total</h2>
		{#if invoice.scopeLineItems?.length}<ul class="mt-4 space-y-2">{#each invoice.scopeLineItems as line}<li class="border-b pb-2 text-sm">{line}</li>{/each}</ul>{:else if invoice.lineItems?.length}<ul class="mt-4 space-y-2">{#each invoice.lineItems as line}<li class="flex justify-between gap-3 border-b pb-2 text-sm"><span>{line.description}</span><span>{money(line.lineTotal)}</span></li>{/each}</ul>{/if}
		<div class="mt-5 space-y-1 text-sm"><div class="flex justify-between"><span>Subtotal</span><span>{money(invoice.subtotal)}</span></div><div class="flex justify-between"><span>Tax</span><span>{money(invoice.taxAmount)}</span></div><div class="flex justify-between border-t pt-2 text-lg font-bold"><span>Total</span><span>{money(invoice.total)}</span></div><div class="flex justify-between text-slate-600"><span>Balance due</span><span>{money(invoice.balanceDue)}</span></div></div>
	</section>
	{#if form?.message}<p role={form.signedInvoice ? 'status' : 'alert'} class:!text-red-800={!form.signedInvoice} class="rounded-xl border bg-white p-4 text-sm">{form.message}</p>{/if}
	{#if invoice.completionSignature}
		<section aria-label="Completion acknowledgement" class="rounded-xl border border-emerald-200 bg-emerald-50 p-5"><h2 class="text-lg font-bold text-emerald-900">Work completion acknowledged</h2><p class="mt-2 text-sm">Signed by {invoice.completionSignature.signerPrintedName} on {signedDate(invoice.completionSignature.signedAtUtc)}.</p><p class="mt-3 text-sm">{invoice.completionSignature.consentText}</p><p class="mt-3 break-all text-xs text-slate-600">Document reference: {invoice.completionSignature.documentHash}</p></section>
	{:else if invoice.status.toLowerCase() === 'draft' || invoice.status.toLowerCase() === 'void'}
		<p class="rounded-xl border border-amber-200 bg-amber-50 p-5 text-sm">The invoice must be sent before the customer can acknowledge completed work.</p>
	{:else}
		<section aria-label="Sign work completion" class="rounded-xl border bg-white p-5"><h2 class="text-lg font-bold">Customer completion acknowledgement</h2><p class="mt-2 text-sm text-slate-600">Ask the customer to review the work above. This signature records completion; payment is handled separately.</p>
			<form method="POST" action="?/sign" class="mt-5 space-y-4" use:enhance={() => { saving = true; return async ({ update }) => { await update(); saving = false; }; }}>
				<input type="hidden" name="expectedVersion" value={invoice.version} />
				<label class="block text-sm font-semibold">Customer’s printed name<input name="signerPrintedName" required minlength="2" maxlength="200" autocomplete="name" class="mt-2 w-full rounded-lg border p-3 font-normal" /></label>
				<label class="flex gap-3 rounded-lg border p-4 text-sm"><input type="checkbox" name="intentToSign" required class="mt-1 h-5 w-5 shrink-0" /><span>{consent}</span></label>
				<button disabled={saving} class="min-h-12 w-full rounded-lg bg-emerald-900 px-5 font-semibold text-white disabled:opacity-50">{saving ? 'Recording…' : 'Sign and acknowledge completion'}</button>
			</form>
		</section>
	{/if}
</main>
