<script lang="ts">
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();
	const amountPaid = $derived(
		data.invoice.payments.reduce((sum, payment) => sum + payment.amount, 0) ||
			(data.invoice.state === 'paid' || data.invoice.paidAtUtc ? data.invoice.amount : 0)
	);
	const balanceDue = $derived(Math.max(data.invoice.amount - amountPaid, 0));
</script>

<svelte:head>
	<title>{data.invoice.invoiceNumber} · BDR Construction</title>
</svelte:head>

<main class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(255,116,23,0.12),transparent_28%),radial-gradient(circle_at_top_right,rgba(59,130,246,0.10),transparent_30%),linear-gradient(180deg,#fffaf4_0%,#f8fafc_58%,#f4f7fb_100%)] px-4 py-5 text-slate-950 sm:px-6 lg:px-8">
	<div class="mx-auto max-w-4xl space-y-5">
		<header class="flex flex-col gap-4 rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)] md:flex-row md:items-center md:justify-between">
			<div class="flex items-center gap-4">
				<img
					src="/clientFiles/BDRLogo.png"
					alt="BDR Construction"
					class="flex-none rounded-md object-contain"
					style="width: 5rem; height: 5rem;"
				/>
				<div>
					<p class="text-xs font-semibold uppercase tracking-[0.18em] text-orange-600">Invoice Packet</p>
					<h1 class="mt-1 text-2xl font-semibold">BDR Construction</h1>
				</div>
			</div>
			<div class="flex flex-col items-start gap-2 md:items-end">
				{#if data.returnTo}
					<a href={data.returnTo} class="inline-flex w-fit justify-center rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-900 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-50">
						Return to invoices
					</a>
				{/if}
				<span class="w-fit rounded-full bg-orange-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-orange-700">
					{data.invoice.state}
				</span>
			</div>
		</header>

		<section class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
			<div class="grid gap-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
				<div>
					<p class="text-sm font-semibold text-slate-600">{data.invoice.customerName}</p>
					<h2 class="mt-1 text-3xl font-semibold">{data.invoice.invoiceNumber}</h2>
					<p class="mt-2 text-sm leading-6 text-slate-600">{data.invoice.serviceSummary} · {data.invoice.siteName}</p>
				</div>
				<div class="rounded-lg bg-orange-50 px-4 py-3 text-left sm:text-right">
					<p class="text-xs font-semibold uppercase tracking-[0.16em] text-orange-700">Amount Due</p>
					<p class="mt-1 text-3xl font-semibold text-slate-950">{formatCurrency(balanceDue)}</p>
					{#if amountPaid > 0}
						<p class="mt-1 text-xs font-semibold text-emerald-700">{formatCurrency(amountPaid)} paid</p>
					{/if}
				</div>
			</div>
		</section>

		<section class="grid gap-4 md:grid-cols-3">
			<div class="rounded-lg bg-white/90 p-4 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
				<p class="text-xs uppercase tracking-[0.18em] text-slate-500">Approved by</p>
				<p class="mt-2 font-semibold">{data.invoice.approvedBy}</p>
				<p class="mt-1 text-sm text-slate-600">{data.invoice.approvedAtUtc ? formatDate(data.invoice.approvedAtUtc) : 'Date not captured'}</p>
			</div>
			<div class="rounded-lg bg-white/90 p-4 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
				<p class="text-xs uppercase tracking-[0.18em] text-slate-500">Sent</p>
				<p class="mt-2 font-semibold">{data.invoice.sentAtUtc ? formatDate(data.invoice.sentAtUtc) : 'Not sent yet'}</p>
				<p class="mt-1 text-sm text-slate-600">{data.invoice.customerEmail || data.invoice.customerPhone || 'No delivery contact'}</p>
			</div>
			<div class="rounded-lg bg-white/90 p-4 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
				<p class="text-xs uppercase tracking-[0.18em] text-slate-500">Payment</p>
				<p class="mt-2 font-semibold">{balanceDue <= 0 ? (data.invoice.paidAtUtc ? formatDate(data.invoice.paidAtUtc) : 'Paid') : amountPaid > 0 ? 'Partial payment' : 'Open'}</p>
				<p class="mt-1 text-sm text-slate-600">{formatCurrency(amountPaid)} paid · {formatCurrency(balanceDue)} due</p>
			</div>
		</section>

		<section class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
			<h2 class="text-xl font-semibold">Invoice basis</h2>
			<div class="mt-4 grid gap-2">
				{#each data.invoice.lineItems as lineItem}
					<div class="rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-700">{lineItem}</div>
				{/each}
			</div>
		</section>

		<footer class="pb-8 text-center text-xs text-slate-500">
			BDR Construction · {data.invoice.customerEmail || data.invoice.customerPhone}
		</footer>
	</div>
</main>
