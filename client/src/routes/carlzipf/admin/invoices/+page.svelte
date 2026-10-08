<script lang="ts">
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';
	let { data, form }: PageProps = $props();
	let query = $state('');
	let saving = $state(false);
	const invoices = $derived(data.invoices.filter((invoice) =>
		[invoice.invoiceNumber, invoice.customerName, invoice.siteName, invoice.serviceSummary].join(' ').toLowerCase().includes(query.toLowerCase().trim())
	));
	const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
</script>

<svelte:head><title>Invoices · Carl Zipf</title></svelte:head>
<section class="mx-auto max-w-6xl space-y-5 pb-12">
	<header><p class="text-sm font-semibold text-[var(--accent-text)]">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">Invoices</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Approved quotes become invoices through the shared TurnKeyOps billing workflow. Completion signatures and payment status remain separate.</p></header>
	{#if data.loadError}<p role="alert" class="rounded-lg border border-[var(--critical-text)] bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]">{data.loadError} <a href="/carlzipf/admin/invoices" class="underline">Retry</a></p>{/if}
	{#if form?.error}<p role="alert" class="rounded-lg border border-[var(--critical-text)] bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]">{form.error}</p>{/if}
	{#if form?.message}<p role="status" class="rounded-lg border border-[var(--positive-text)] bg-[var(--positive-soft)] p-4 text-[var(--positive-text)]">{form.message}</p>{/if}
	<div class="flex flex-wrap items-end gap-3"><label class="grow text-sm font-semibold">Find an invoice<input bind:value={query} placeholder="Customer, site, or invoice number" class="mt-2 w-full rounded-lg border bg-[var(--surface)] p-3 font-normal" /></label><form method="POST" action="?/sync" use:enhance={() => { saving = true; return async ({ update }) => { await update(); saving = false; }; }}><button disabled={saving} class="admin-primary min-h-12 rounded-lg bg-[var(--cta)] px-5 font-semibold text-white disabled:opacity-50">{saving ? 'Checking…' : 'Create invoices from approved quotes'}</button></form></div>
	<p class="text-xs text-[var(--text-muted)]">This checks approved quotes and reuses invoices already created. It does not collect payment or deliver a message.</p>
	<section aria-label="Invoices" class="grid gap-4 md:grid-cols-2">
		{#each invoices as invoice}
			<article class="rounded-xl border bg-[var(--surface)] p-5"><div class="flex justify-between gap-3"><div><h2 class="text-lg font-bold">{invoice.invoiceNumber}</h2><p class="mt-1 text-sm text-[var(--text-muted)]">{invoice.customerName || 'Customer'} · {invoice.siteName || invoice.serviceSummary || 'Service work'}</p></div><span class="text-right font-bold">{money(invoice.total)}</span></div>
			<div class="mt-4 flex flex-wrap gap-3 text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]"><span>{invoice.status}</span><span>Balance {money(invoice.balanceDue)}</span><span>{invoice.completionSignature ? 'Completion signed' : 'Completion unsigned'}</span></div>
			{#if invoice.quoteRequestId}<a class="mt-4 inline-block text-sm font-semibold text-[var(--accent-text)] underline" href={`/carlzipf/admin/estimates?request=${encodeURIComponent(invoice.quoteRequestId)}`}>Review source quote</a>{/if}
			{#if invoice.status.toLowerCase() === 'draft'}<form method="POST" action="?/activate" class="mt-5 border-t pt-4" use:enhance={() => { saving = true; return async ({ update }) => { await update(); saving = false; }; }}><input type="hidden" name="invoiceId" value={invoice.id} /><input type="hidden" name="version" value={invoice.version} /><label class="flex gap-2 text-sm"><input type="checkbox" name="reviewed" value="yes" required /><span>I reviewed this invoice and its customer contact.</span></label><button disabled={saving} class="mt-3 min-h-11 rounded-lg border border-[var(--accent-text)] px-4 font-semibold text-[var(--accent-text)] disabled:opacity-50">Activate invoice</button><p class="mt-2 text-xs text-[var(--text-muted)]">Moves it to sent status so field completion can be signed. This action does not send email or text.</p></form>{/if}
			{#if invoice.completionSignature}<p class="mt-4 text-sm text-[var(--positive-text)]">Acknowledged by {invoice.completionSignature.signerPrintedName} on {new Date(invoice.completionSignature.signedAtUtc).toLocaleString('en-US')}.</p>{/if}
			<p class="mt-4 text-xs text-[var(--text-muted)]">The assigned technician can open this invoice from the field workspace after the job is linked.</p>
			</article>
		{:else}
			<p class="rounded-xl border border-dashed p-8 text-center text-sm text-[var(--text-muted)] md:col-span-2">{data.invoices.length ? 'No invoices match your search.' : 'No approved quote invoices yet. Approve a quote, then create its invoice here.'}</p>
		{/each}
	</section>
</section>
