<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildInvoiceViews, getScaffoldBanner } from '$lib/mvp-display';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type InvoiceView = ReturnType<typeof buildInvoiceViews>[number];

	let { data }: PageProps = $props();

	const allInvoices = $derived(buildInvoiceViews(data.invoices, data.customers));
	const receivablesValue = $derived(allInvoices.reduce((sum, invoice) => sum + invoice.balanceDue, 0));

	let invoiceMode = $state<'active' | 'paid'>('active');
	let selectedInvoiceId = $state('');

	const isPaid = (invoice: InvoiceView) => invoice.status.toLowerCase().includes('paid');

	const visibleInvoices = $derived.by(() =>
		allInvoices.filter((invoice) => (invoiceMode === 'active' ? !isPaid(invoice) : isPaid(invoice)))
	);

	const selectedInvoice = $derived.by(() => {
		const current = visibleInvoices.find((invoice) => invoice.id === selectedInvoiceId);
		return current ?? visibleInvoices[0] ?? null;
	});

	const metrics = $derived([
		{ label: 'Invoices', value: String(allInvoices.length), detail: getScaffoldBanner(data.source) },
		{ label: 'Receivables', value: formatCurrency(receivablesValue), detail: 'Outstanding balance visible from the billing desk' },
		{
			label: 'Check-hold watch',
			value: String(allInvoices.filter((invoice) => !invoice.checkHold.startsWith('No hold')).length),
			detail: 'Invoices with a payment hold or billing dependency'
		}
	]);

	$effect(() => {
		if (selectedInvoice && selectedInvoiceId !== selectedInvoice.id) {
			selectedInvoiceId = selectedInvoice.id;
		}
	});
</script>

<AdminWorkspace
	kicker="Invoices"
	title="Collections lane with active and paid invoice views"
	description="Invoices now follow the same shell pattern: section rail for active versus paid, a focus list of invoice names, and a full billing record in the work area."
	{metrics}
	contextLabel="Billing mode"
	focusLabel="Invoice list"
>
	{#snippet context()}
		<div class="space-y-3">
			<button
				type="button"
				class={`w-full rounded-md border px-3 py-3 text-left transition ${invoiceMode === 'active' ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
				onclick={() => (invoiceMode = 'active')}
			>
				<p class="text-sm font-semibold text-[var(--text-strong)]">Active invoices</p>
				<p class="mt-1 text-xs text-[var(--text-muted)]">{allInvoices.filter((invoice) => !isPaid(invoice)).length} visible for collection or release decisions</p>
			</button>

			<button
				type="button"
				class={`w-full rounded-md border px-3 py-3 text-left transition ${invoiceMode === 'paid' ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
				onclick={() => (invoiceMode = 'paid')}
			>
				<p class="text-sm font-semibold text-[var(--text-strong)]">Paid invoices</p>
				<p class="mt-1 text-xs text-[var(--text-muted)]">{allInvoices.filter((invoice) => isPaid(invoice)).length} cleared and closed records</p>
			</button>

			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3 text-sm text-[var(--text-base)]">
				Current mode keeps the billing desk simple for operators who mainly need either live collections work or completed payment history.
			</div>
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			{#each visibleInvoices as invoice}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedInvoice?.id === invoice.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedInvoiceId = invoice.id)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{invoice.invoiceNumber}</p>
					<p class="mt-1 text-xs text-[var(--text-muted)]">{invoice.customer?.displayName ?? 'Unknown customer'}</p>
					<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{invoice.status} · {formatCurrency(invoice.balanceDue)}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedInvoice}
			<div class="space-y-4">
				<div class="flex flex-wrap items-start justify-between gap-3">
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Invoice record</p>
						<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedInvoice.invoiceNumber}</h4>
						<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedInvoice.customer?.displayName ?? 'Unknown customer'} · Due {formatDate(selectedInvoice.dueDateUtc)}</p>
					</div>
					<div class="text-right">
						<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(selectedInvoice.balanceDue)}</p>
						<p class="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">{selectedInvoice.status}</p>
					</div>
				</div>

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Billing phase</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.billingPhase}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Payment method</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.paymentMethod}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Queue owner</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.owner}</p>
					</div>
				</div>

				<div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice detail</p>
						<div class="mt-3 space-y-3">
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Check-hold status</p>
								<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.checkHold}</p>
							</div>
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next step</p>
								<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.nextStep}</p>
							</div>
						</div>
					</div>

					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Customer path</p>
						<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">
							Deposit, progress billing, and final billing stay attached to this one record so accounting can decide whether work should continue, pause, or close.
						</p>
					</div>
				</div>
			</div>
		{:else}
			<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
				No invoices are available in the {invoiceMode} lane.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
