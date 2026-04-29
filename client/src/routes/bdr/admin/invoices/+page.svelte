<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildInvoiceViews, getScaffoldBanner } from '$lib/mvp-display';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type InvoiceView = ReturnType<typeof buildInvoiceViews>[number];
	type BillingDeskState = 'Overdue' | 'Follow-up' | 'Open' | 'Paid';
	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};

	let { data }: PageProps = $props();

	const allInvoices = $derived(buildInvoiceViews(data.invoices, data.customers));
	const receivablesValue = $derived(allInvoices.reduce((sum, invoice) => sum + invoice.balanceDue, 0));

	let invoiceMode = $state<'active' | 'paid'>('active');
	let selectedInvoiceId = $state('');

	const isPaid = (invoice: InvoiceView) => invoice.status.toLowerCase().includes('paid');
	const invoiceDueTime = (invoice: InvoiceView) => new Date(invoice.dueDateUtc ?? '').getTime();
	const isOverdue = (invoice: InvoiceView) => !isPaid(invoice) && Number.isFinite(invoiceDueTime(invoice)) && invoiceDueTime(invoice) < Date.now();
	const needsFollowUp = (invoice: InvoiceView) =>
		!isPaid(invoice) &&
		(!invoice.checkHold.toLowerCase().startsWith('no hold') ||
			invoice.owner.toLowerCase().includes('collections') ||
			invoice.nextStep.toLowerCase().includes('collect'));
	const getBillingDeskState = (invoice: InvoiceView): BillingDeskState => {
		if (isPaid(invoice)) return 'Paid';
		if (isOverdue(invoice)) return 'Overdue';
		if (needsFollowUp(invoice)) return 'Follow-up';
		return 'Open';
	};
	const billingDeskStateTone = (state: BillingDeskState) => {
		switch (state) {
			case 'Overdue':
				return 'border-rose-400/35 bg-rose-400/10 text-rose-700';
			case 'Follow-up':
				return 'border-amber-400/35 bg-amber-400/10 text-amber-700';
			case 'Paid':
				return 'border-emerald-400/35 bg-emerald-400/10 text-emerald-300';
			default:
				return 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]';
		}
	};

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
	const billingSummary = $derived([
		{
			label: 'Overdue',
			value: String(allInvoices.filter((invoice) => getBillingDeskState(invoice) === 'Overdue').length),
			detail: 'Highest collection risk'
		},
		{
			label: 'Follow-up',
			value: String(allInvoices.filter((invoice) => getBillingDeskState(invoice) === 'Follow-up').length),
			detail: 'Needs reminder or release check'
		},
		{
			label: 'Open',
			value: String(allInvoices.filter((invoice) => getBillingDeskState(invoice) === 'Open').length),
			detail: 'Normal billing work'
		},
		{
			label: 'Paid',
			value: String(allInvoices.filter((invoice) => getBillingDeskState(invoice) === 'Paid').length),
			detail: 'Cleared records'
		}
	]);
	const bobMoves = $derived.by(() => {
		if (!selectedInvoice) {
			return [
				{
					label: 'Review billing queue',
					detail: `${visibleInvoices.length} invoice${visibleInvoices.length === 1 ? '' : 's'} in view`,
					href: '/bdr/admin/invoices?role=office-admin'
				}
			] satisfies BobMove[];
		}

		const state = getBillingDeskState(selectedInvoice);
		return [
			{
				label: state === 'Overdue' ? 'Draft collection follow-up' : 'Review next billing move',
				detail: selectedInvoice.nextStep,
				href: '#invoice-record'
			},
			{
				label: state === 'Follow-up' ? 'Flag payment risk' : 'Check due timing',
				detail: state === 'Overdue' ? `Past due since ${formatDate(selectedInvoice.dueDateUtc)}` : `Due ${formatDate(selectedInvoice.dueDateUtc)}`,
				href: '#invoice-record'
			},
			{
				label: 'Check hold or release',
				detail: selectedInvoice.checkHold,
				href: '#invoice-record'
			}
		] satisfies BobMove[];
	});

	$effect(() => {
		if (selectedInvoice && selectedInvoiceId !== selectedInvoice.id) {
			selectedInvoiceId = selectedInvoice.id;
		}
	});
</script>

<AdminWorkspace
	kicker="External Admin / Invoices"
	title="Calm billing desk for overdue, follow-up, open, and paid work"
	description="Keep collection risk and next billing actions obvious without turning the page into an accounting dashboard."
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

			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Billing states</p>
				<div class="mt-3 grid gap-2">
					{#each billingSummary as item}
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5">
							<div class="flex items-center justify-between gap-3">
								<p class="text-sm font-semibold text-[var(--text-strong)]">{item.label}</p>
								<span class="text-sm font-semibold text-[var(--text-strong)]">{item.value}</span>
							</div>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
						</div>
					{/each}
				</div>
			</div>

			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<div class="flex items-start justify-between gap-3">
					<div>
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Bob collections assist</p>
						<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{selectedInvoice?.invoiceNumber ?? 'Billing queue'}</p>
					</div>
					<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm">
						✨
					</span>
				</div>
				<div class="mt-3 space-y-2">
					{#each bobMoves as move}
						<a
							href={move.href}
							class="block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5 transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]"
						>
							<p class="text-sm font-semibold text-[var(--text-strong)]">{move.label}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{move.detail}</p>
						</a>
					{/each}
				</div>
			</div>
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
				{visibleInvoices.length} invoices
			</p>
			{#each visibleInvoices as invoice}
				{@const state = getBillingDeskState(invoice)}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedInvoice?.id === invoice.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedInvoiceId = invoice.id)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{invoice.invoiceNumber}</p>
					<p class="mt-1 text-xs text-[var(--text-muted)]">{invoice.customer?.displayName ?? 'Unknown customer'}</p>
					<div class="mt-3 flex flex-wrap items-center gap-2">
						<span class={`rounded-md border px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] ${billingDeskStateTone(state)}`}>
							{state}
						</span>
						<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--text-base)]">
							{invoice.status}
						</span>
					</div>
					<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{formatCurrency(invoice.balanceDue)} · {invoice.nextStep}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedInvoice}
			<div id="invoice-record" class="space-y-4">
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
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Billing state</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{getBillingDeskState(selectedInvoice)}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Due timing</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{isOverdue(selectedInvoice) ? `Past due · ${formatDate(selectedInvoice.dueDateUtc)}` : `Due ${formatDate(selectedInvoice.dueDateUtc)}`}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next move</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.nextStep}</p>
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
						<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">Deposit, progress billing, and final billing stay attached to the same record for one follow-up path.</p>
					</div>
				</div>
			</div>
		{:else}
			<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
				No invoices are available in this {invoiceMode} view.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
