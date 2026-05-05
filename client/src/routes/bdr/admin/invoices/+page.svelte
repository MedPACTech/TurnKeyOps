<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildInvoiceViews, getScaffoldBanner } from '$lib/mvp-display';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type InvoiceView = ReturnType<typeof buildInvoiceViews>[number];
	type BillingDeskState = 'Overdue' | 'Follow-up' | 'Open' | 'Paid';
	type InvoiceMode = 'ready' | 'active' | 'paid';
	type BobSuggestion = {
		label: string;
		detail: string;
	};
	type ApprovedEstimateDraft = {
		invoiceId?: string;
		invoiceNumber?: string;
		invoiceState?: 'draft' | 'sent' | 'paid';
		requestId: string;
		revisionNumber: number;
		customerName: string;
		siteName: string;
		serviceSummary: string;
		scopeLineItems: string[];
		notes: string;
		status: 'draft' | 'ready-to-send' | 'sent';
		savedAtUtc: string;
		sentAtUtc?: string;
		delivery?: {
			status: 'sent' | 'approved' | 'changes-requested';
			method?: 'review-link';
			reviewUrl: string;
			email: string;
			phone: string;
			sentAtUtc: string;
			approvedAtUtc?: string;
		};
	};
	type InvoicePageData = PageProps['data'] & {
		approvedEstimateDrafts?: ApprovedEstimateDraft[];
		lifecycleInvoices?: Array<{
			id: string;
			sourceRequestId: string;
			invoiceNumber: string;
			state: 'draft' | 'sent' | 'paid';
			customerName: string;
			siteName: string;
			serviceSummary: string;
			amount: number;
			customerEmail: string;
			customerPhone: string;
			reviewUrl: string;
			approvedBy: string;
			approvalMethod: 'customer review link';
			approvedAtUtc?: string;
			sentAtUtc?: string;
			paidAtUtc?: string;
			reminderSentAtUtc?: string;
			lineItems: string[];
		}>;
	};

	let { data, form }: PageProps = $props();
	const pageData = $derived(data as InvoicePageData);

	const allInvoices = $derived(buildInvoiceViews(pageData.invoices, pageData.customers));
	const lifecycleInvoices = $derived(pageData.lifecycleInvoices ?? []);
	const approvedEstimateDrafts = $derived(
		lifecycleInvoices
			.filter((invoice) => invoice.state === 'draft')
			.map(
				(invoice) =>
					({
						invoiceId: invoice.id,
						invoiceNumber: invoice.invoiceNumber,
						invoiceState: invoice.state,
						requestId: invoice.sourceRequestId,
						revisionNumber: 1,
						customerName: invoice.customerName,
						siteName: invoice.siteName,
						serviceSummary: invoice.serviceSummary,
						scopeLineItems: invoice.lineItems,
						notes: '',
						status: 'sent',
						savedAtUtc: invoice.createdAtUtc,
						sentAtUtc: invoice.sentAtUtc,
						delivery: {
							status: 'approved',
							method: 'review-link',
							reviewUrl: invoice.reviewUrl,
							email: invoice.customerEmail,
							phone: invoice.customerPhone,
							sentAtUtc: invoice.sentAtUtc ?? invoice.createdAtUtc,
							approvedAtUtc: invoice.approvedAtUtc
						}
					}) satisfies ApprovedEstimateDraft
			)
	);
	const receivablesValue = $derived(
		allInvoices.reduce((sum, invoice) => sum + invoice.balanceDue, 0) +
			lifecycleInvoices
				.filter((invoice) => invoice.state === 'sent')
				.reduce((sum, invoice) => sum + invoice.amount, 0)
	);

	let invoiceMode = $state<InvoiceMode>('ready');
	let selectedInvoiceId = $state('');
	let selectedLifecycleInvoiceId = $state('');
	let selectedReadyEstimateId = $state('');
	let reviewingReadyInvoiceId = $state('');
	let sendingReadyInvoiceId = $state('');
	let queuedReadyInvoiceId = $state('');
	let copiedCustomerLink = $state('');
	const activeInvoiceMode = $derived(
		invoiceMode === 'ready' && !approvedEstimateDrafts.length ? 'active' : invoiceMode
	);

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

	const visibleInvoices = $derived.by(() =>
		activeInvoiceMode === 'ready'
			? []
			: allInvoices.filter((invoice) => (activeInvoiceMode === 'active' ? !isPaid(invoice) : isPaid(invoice)))
	);
	const visibleLifecycleInvoices = $derived.by(() =>
		activeInvoiceMode === 'ready'
			? []
			: lifecycleInvoices.filter((invoice) =>
					activeInvoiceMode === 'active' ? invoice.state === 'sent' : invoice.state === 'paid'
				)
	);

	const selectedLifecycleInvoice = $derived.by(() => {
		if (activeInvoiceMode === 'ready') return null;
		const current = visibleLifecycleInvoices.find((invoice) => invoice.id === selectedLifecycleInvoiceId);
		return current ?? visibleLifecycleInvoices[0] ?? null;
	});
	const selectedInvoice = $derived.by(() => {
		if (activeInvoiceMode === 'ready') return null;
		if (selectedLifecycleInvoice) return null;
		const current = visibleInvoices.find((invoice) => invoice.id === selectedInvoiceId);
		return current ?? visibleInvoices[0] ?? null;
	});
	const selectedReadyEstimate = $derived(
		approvedEstimateDrafts.find((estimate) => estimate.requestId === selectedReadyEstimateId) ??
			approvedEstimateDrafts[0] ??
			null
	);
	const parseApprovedEstimateTotal = (estimate: ApprovedEstimateDraft) => {
		if (estimate.invoiceId) {
			const invoice = lifecycleInvoices.find((record) => record.id === estimate.invoiceId);
			if (invoice) return invoice.amount;
		}
		const totalLine = [...estimate.scopeLineItems]
			.reverse()
			.find((line) => line.toLowerCase().startsWith('estimated total'));
		const match = totalLine?.match(/\$([0-9,]+(?:\.\d{1,2})?)/);
		return match ? Number.parseFloat(match[1].replaceAll(',', '')) : 0;
	};
	const getApprovalMethodLabel = (estimate: ApprovedEstimateDraft) =>
		estimate.delivery?.reviewUrl ? 'customer review link' : 'customer approval';
	const getApprovalPersonLabel = (estimate: ApprovedEstimateDraft) =>
		estimate.delivery?.email ? `${estimate.customerName} (${estimate.delivery.email})` : estimate.customerName;
	const getApprovalDateLabel = (estimate: ApprovedEstimateDraft) =>
		estimate.delivery?.approvedAtUtc ? formatDate(estimate.delivery.approvedAtUtc) : 'date not captured';
	const getDraftInvoiceNumber = (estimate: ApprovedEstimateDraft) =>
		estimate.invoiceNumber ?? `INV-DRAFT-${estimate.requestId.slice(0, 8).toUpperCase()}`;
	const invoicesReturnTo = '/bdr/admin/invoices';
	const getPacketPreviewHref = (estimate: ApprovedEstimateDraft) => {
		const rawHref = estimate.invoiceId
			? `/bdr/invoice/${encodeURIComponent(estimate.invoiceId)}`
			: estimate.delivery?.reviewUrl?.trim() || `/bdr/estimate/${encodeURIComponent(estimate.requestId)}`;
		const separator = rawHref.includes('?') ? '&' : '?';
		return `${rawHref}${separator}returnTo=${encodeURIComponent(invoicesReturnTo)}`;
	};
	const getInvoicePacketHref = (invoiceId: string) =>
		`/bdr/invoice/${encodeURIComponent(invoiceId)}?returnTo=${encodeURIComponent(invoicesReturnTo)}`;
	const copyCustomerLink = async (href: string) => {
		const origin = globalThis.location?.origin ?? '';
		await navigator.clipboard.writeText(`${origin}${href}`);
		copiedCustomerLink = href;
	};
	const isReviewingReadyInvoice = $derived(
		Boolean(selectedReadyEstimate && reviewingReadyInvoiceId === selectedReadyEstimate.requestId)
	);
	const isSendingReadyInvoice = $derived(
		Boolean(selectedReadyEstimate && sendingReadyInvoiceId === selectedReadyEstimate.requestId)
	);
	const isReadyInvoiceQueued = $derived(
		Boolean(selectedReadyEstimate && queuedReadyInvoiceId === selectedReadyEstimate.requestId)
	);
	const bobSuggestions = $derived.by(() => {
		if (activeInvoiceMode === 'ready' && selectedReadyEstimate) {
			return [
				{
					label: 'Confirm invoice total',
					detail: `${getDraftInvoiceNumber(selectedReadyEstimate)} is ready at ${formatCurrency(parseApprovedEstimateTotal(selectedReadyEstimate))}.`
				},
				{
					label: 'Send the billing packet',
					detail: selectedReadyEstimate.delivery?.email
						? `Use email first for ${selectedReadyEstimate.delivery.email}; SMS can carry the same review link.`
						: 'Customer email is missing, so phone follow-up should come first.'
				},
				{
					label: 'Prepare handoff',
					detail: 'Once sent, keep scheduling and production handoff attached to this customer record.'
				}
			] satisfies BobSuggestion[];
		}

		if (selectedLifecycleInvoice) {
			return [
				{
					label: selectedLifecycleInvoice.state === 'paid' ? 'Close the loop' : 'Work billing follow-up',
					detail:
						selectedLifecycleInvoice.state === 'paid'
							? `${selectedLifecycleInvoice.invoiceNumber} is paid and ready for closeout.`
							: `${selectedLifecycleInvoice.invoiceNumber} was sent; keep payment and reminder timing visible.`
				},
				{
					label: 'Customer context',
					detail: `${selectedLifecycleInvoice.customerName} approved via ${selectedLifecycleInvoice.approvalMethod}.`
				},
				{
					label: 'Packet access',
					detail: 'Use Open invoice to review the customer-facing invoice packet with a return path.'
				}
			] satisfies BobSuggestion[];
		}

		if (selectedInvoice) {
			const state = getBillingDeskState(selectedInvoice);
			return [
				{
					label: state === 'Overdue' ? 'Prioritize collection' : 'Work next billing step',
					detail: selectedInvoice.nextStep
				},
				{
					label: 'Check due timing',
					detail: isOverdue(selectedInvoice)
						? `Past due since ${formatDate(selectedInvoice.dueDateUtc)}.`
						: `Due ${formatDate(selectedInvoice.dueDateUtc)}.`
				},
				{
					label: 'Resolve hold status',
					detail: selectedInvoice.checkHold
				}
			] satisfies BobSuggestion[];
		}

		return [
			{
				label: 'Pick an invoice',
				detail: 'Bob will surface the next billing move once a draft or active invoice is selected.'
			}
		] satisfies BobSuggestion[];
	});

	const metrics = $derived([
		{ label: 'Draft invoices', value: String(approvedEstimateDrafts.length), detail: 'Customer-approved estimate invoices waiting on billing review' },
		{ label: 'Receivables', value: formatCurrency(receivablesValue), detail: 'Outstanding balance visible from the billing desk' },
		{
			label: 'Invoices',
			value: String(allInvoices.length + lifecycleInvoices.length),
			detail: getScaffoldBanner(pageData.source)
		}
	]);

	$effect(() => {
		if (selectedLifecycleInvoice && selectedLifecycleInvoiceId !== selectedLifecycleInvoice.id) {
			selectedLifecycleInvoiceId = selectedLifecycleInvoice.id;
		}
	});

	$effect(() => {
		if (selectedInvoice && selectedInvoiceId !== selectedInvoice.id) {
			selectedInvoiceId = selectedInvoice.id;
		}
	});

	$effect(() => {
		if (selectedReadyEstimate && selectedReadyEstimateId !== selectedReadyEstimate.requestId) {
			selectedReadyEstimateId = selectedReadyEstimate.requestId;
		}
	});

	$effect(() => {
		if (!selectedReadyEstimate || reviewingReadyInvoiceId !== selectedReadyEstimate.requestId) {
			reviewingReadyInvoiceId = '';
		}
	});

	$effect(() => {
		if (!selectedReadyEstimate || sendingReadyInvoiceId !== selectedReadyEstimate.requestId) {
			sendingReadyInvoiceId = '';
		}
	});
</script>

<AdminWorkspace
	kicker="External Admin / Invoices"
	title="Invoices"
	description="Keep collection risk and next billing actions obvious without turning the page into an accounting dashboard."
	{metrics}
	contextLabel="Invoice views"
	focusLabel="Invoice list"
>
	{#snippet context()}
		<div class="space-y-3">
			<button
				type="button"
				class={`w-full rounded-lg border px-3 py-3 text-left transition ${activeInvoiceMode === 'ready' ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
				onclick={() => (invoiceMode = 'ready')}
			>
				<p class="text-sm font-semibold text-[var(--text-strong)]">Draft invoices</p>
				<p class="mt-1 text-xs text-[var(--text-muted)]">{approvedEstimateDrafts.length} customer-approved invoice draft{approvedEstimateDrafts.length === 1 ? '' : 's'} waiting for review</p>
			</button>

			<button
				type="button"
				class={`w-full rounded-lg border px-3 py-3 text-left transition ${activeInvoiceMode === 'active' ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
				onclick={() => (invoiceMode = 'active')}
			>
				<p class="text-sm font-semibold text-[var(--text-strong)]">Active invoices</p>
				<p class="mt-1 text-xs text-[var(--text-muted)]">{allInvoices.filter((invoice) => !isPaid(invoice)).length} visible for collection or release decisions</p>
			</button>

			<button
				type="button"
				class={`w-full rounded-lg border px-3 py-3 text-left transition ${activeInvoiceMode === 'paid' ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
				onclick={() => (invoiceMode = 'paid')}
			>
				<p class="text-sm font-semibold text-[var(--text-strong)]">Paid invoices</p>
				<p class="mt-1 text-xs text-[var(--text-muted)]">{allInvoices.filter((invoice) => isPaid(invoice)).length} cleared and closed records</p>
			</button>
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			{#if activeInvoiceMode === 'ready'}
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
					{approvedEstimateDrafts.length} drafts
				</p>
				{#each approvedEstimateDrafts as estimate}
					<button
						type="button"
						class={`w-full rounded-lg border px-3 py-3 text-left transition ${selectedReadyEstimate?.requestId === estimate.requestId ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
						onclick={() => (selectedReadyEstimateId = estimate.requestId)}
					>
						<p class="text-sm font-semibold text-[var(--text-strong)]">{getDraftInvoiceNumber(estimate)}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{estimate.customerName} · {estimate.siteName}</p>
						<div class="mt-3 flex items-start justify-between gap-3">
							<p class="text-lg font-semibold text-[var(--text-strong)]">{formatCurrency(parseApprovedEstimateTotal(estimate))}</p>
							<p class="text-right text-[0.7rem] uppercase tracking-[0.14em] text-[var(--muted)]">{getApprovalDateLabel(estimate)}</p>
						</div>
						<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{estimate.serviceSummary}</p>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">
							Approved by {getApprovalPersonLabel(estimate)} via {getApprovalMethodLabel(estimate)}
						</p>
					</button>
				{/each}
				{#if !approvedEstimateDrafts.length}
					<div class="rounded-lg bg-white/80 px-4 py-5 text-sm text-[var(--text-muted)] shadow-sm">
						No draft invoices are waiting for billing review.
					</div>
				{/if}
			{:else}
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
					{visibleInvoices.length + visibleLifecycleInvoices.length} invoices
				</p>
				{#each visibleLifecycleInvoices as invoice}
					<button
						type="button"
						class={`w-full rounded-lg border px-3 py-3 text-left transition ${selectedLifecycleInvoice?.id === invoice.id ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
						onclick={() => {
							selectedLifecycleInvoiceId = invoice.id;
							selectedInvoiceId = '';
						}}
					>
						<p class="text-sm font-semibold text-[var(--text-strong)]">{invoice.invoiceNumber}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{invoice.customerName} · {invoice.siteName}</p>
						<div class="mt-3 flex items-start justify-between gap-3">
							<p class="text-lg font-semibold text-[var(--text-strong)]">{formatCurrency(invoice.amount)}</p>
							<p class="text-right text-[0.7rem] uppercase tracking-[0.14em] text-[var(--muted)]">{invoice.state}</p>
						</div>
						<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{invoice.serviceSummary}</p>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">
							Approved by {invoice.approvedBy} via {invoice.approvalMethod}
						</p>
					</button>
				{/each}
				{#each visibleInvoices as invoice}
					{@const state = getBillingDeskState(invoice)}
					<button
						type="button"
						class={`w-full rounded-lg border px-3 py-3 text-left transition ${selectedInvoice?.id === invoice.id ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
						onclick={() => {
							selectedInvoiceId = invoice.id;
							selectedLifecycleInvoiceId = '';
						}}
					>
						<p class="text-sm font-semibold text-[var(--text-strong)]">{invoice.invoiceNumber}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{invoice.customer?.displayName ?? 'Unknown customer'}</p>
						<div class="mt-3 flex items-start justify-between gap-3">
							<p class="text-lg font-semibold text-[var(--text-strong)]">{formatCurrency(invoice.balanceDue)}</p>
							<p class="text-right text-[0.7rem] uppercase tracking-[0.14em] text-[var(--muted)]">{state}</p>
						</div>
						<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{invoice.nextStep}</p>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{invoice.billingPhase} · Due {formatDate(invoice.dueDateUtc)}</p>
					</button>
				{/each}
			{/if}
		</div>
	{/snippet}

	{#snippet work()}
		{#if activeInvoiceMode === 'ready'}
			{#if selectedReadyEstimate}
				<div id="ready-estimate" class="space-y-4">
					<div class="grid gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(280px,0.42fr)]">
						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex flex-wrap items-start justify-between gap-3">
								<div>
									<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Draft invoice</p>
									<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{getDraftInvoiceNumber(selectedReadyEstimate)}</h4>
									<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedReadyEstimate.customerName} · {selectedReadyEstimate.serviceSummary} · {selectedReadyEstimate.siteName}</p>
								</div>
								<div class="text-right">
									<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(parseApprovedEstimateTotal(selectedReadyEstimate))}</p>
									<p class="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Estimate total</p>
								</div>
							</div>
							{#if selectedReadyEstimate.delivery?.approvedAtUtc}
								<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
									{selectedReadyEstimate.customerName} approved via {getApprovalMethodLabel(selectedReadyEstimate)} on {formatDate(selectedReadyEstimate.delivery.approvedAtUtc)}.
								</p>
							{/if}
						</div>

						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<div class="flex items-start justify-between gap-3">
								<div>
									<p class="text-sm font-semibold text-[var(--text-strong)]">Bob</p>
									<p class="mt-1 text-xs text-[var(--text-muted)]">Suggestions</p>
								</div>
								<span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-lg shadow-sm">👷</span>
							</div>
							<div class="mt-3 grid gap-2">
								{#each bobSuggestions as suggestion}
									<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2 shadow-sm">
										<p class="text-sm font-semibold text-[var(--text-strong)]">{suggestion.label}</p>
										<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{suggestion.detail}</p>
									</div>
								{/each}
							</div>
						</div>
					</div>

					<div class="grid gap-3 md:grid-cols-3">
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Approved by</p>
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedReadyEstimate.customerName}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedReadyEstimate.delivery?.email || 'Email not captured'}</p>
						</div>
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Approval method</p>
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">Customer review link</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">{getApprovalDateLabel(selectedReadyEstimate)}</p>
						</div>
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Customer phone</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedReadyEstimate.delivery?.phone || 'Not captured'}</p>
						</div>
					</div>

					<div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.8fr)]">
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice basis</p>
							<div class="mt-3 grid gap-2">
								{#each selectedReadyEstimate.scopeLineItems as lineItem}
									<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2 text-sm text-[var(--text-base)] shadow-sm">{lineItem}</div>
								{/each}
							</div>
						</div>

						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice actions</p>
							<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">This invoice draft was created when the customer approved the estimate. Review it, then send the billing packet or move it into scheduling handoff.</p>
							<div class="mt-4 grid gap-2">
								<button
									type="button"
									class="inline-flex w-full justify-center rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]"
									onclick={() => (reviewingReadyInvoiceId = isReviewingReadyInvoice ? '' : selectedReadyEstimate.requestId)}
								>
									Review invoice
								</button>
								<button
									type="button"
									class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]"
									onclick={() => (sendingReadyInvoiceId = isSendingReadyInvoice ? '' : selectedReadyEstimate.requestId)}
								>
									Send invoice
								</button>
								<a href={getPacketPreviewHref(selectedReadyEstimate)} class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
									Preview approved packet
								</a>
							</div>
						</div>
					</div>

					{#if isSendingReadyInvoice}
						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex flex-wrap items-start justify-between gap-3">
								<div>
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Send invoice</p>
									<h5 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Delivery packet</h5>
									<p class="mt-1 text-sm text-[var(--text-muted)]">Choose how this draft invoice should reach the customer.</p>
								</div>
								<span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-semibold text-[var(--accent-text)]">
									{isReadyInvoiceQueued ? 'Queued' : 'Ready'}
								</span>
							</div>

							<div class="mt-4 grid gap-3 md:grid-cols-3">
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Email</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedReadyEstimate.delivery?.email || 'Not captured'}</p>
									<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Provider not configured. Use customer link for v1.</p>
								</div>
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">SMS</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedReadyEstimate.delivery?.phone || 'Not captured'}</p>
									<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Provider not configured. No SMS is sent.</p>
								</div>
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Customer link</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{getDraftInvoiceNumber(selectedReadyEstimate)}</p>
									<button
										type="button"
										class="mt-2 text-xs font-semibold text-[var(--accent-text)]"
										onclick={() => copyCustomerLink(getPacketPreviewHref(selectedReadyEstimate))}
									>
										{copiedCustomerLink === getPacketPreviewHref(selectedReadyEstimate) ? 'Copied' : 'Copy link'}
									</button>
								</div>
							</div>

							{#if isReadyInvoiceQueued}
								<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
									Invoice send is queued locally for this demo. The production hook should send email/SMS and move this invoice to Active.
								</p>
							{/if}
							{#if form?.invoiceActionMessage}
								<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
									{form.invoiceActionMessage}
								</p>
							{/if}

							<div class="mt-4 flex flex-col gap-2 sm:flex-row">
								<form method="POST" action="?/submitInvoice" class="contents">
									<input type="hidden" name="invoiceId" value={selectedReadyEstimate.invoiceId ?? ''} />
									<button
										type="submit"
										class="inline-flex justify-center rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]"
										onclick={() => (queuedReadyInvoiceId = selectedReadyEstimate.requestId)}
									>
										Submit invoice
									</button>
								</form>
								<a href={getPacketPreviewHref(selectedReadyEstimate)} class="inline-flex justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
									Open packet preview
								</a>
								<button
									type="button"
									class="inline-flex justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-muted)] shadow-sm"
									disabled
								>
									Email unavailable
								</button>
								<button
									type="button"
									class="inline-flex justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-muted)] shadow-sm"
									disabled
								>
									SMS unavailable
								</button>
								<button
									type="button"
									class="inline-flex justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]"
									onclick={() => (sendingReadyInvoiceId = '')}
								>
									Close
								</button>
							</div>
						</div>
					{/if}

					{#if isReviewingReadyInvoice}
						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex flex-wrap items-start justify-between gap-3">
								<div>
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice review</p>
									<h5 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Draft billing packet</h5>
									<p class="mt-1 text-sm text-[var(--text-muted)]">Customer-approved invoice draft for {selectedReadyEstimate.siteName}</p>
								</div>
								<p class="text-2xl font-semibold text-[var(--text-strong)]">{formatCurrency(parseApprovedEstimateTotal(selectedReadyEstimate))}</p>
							</div>
							<div class="mt-4 grid gap-3 md:grid-cols-3">
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Bill to</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedReadyEstimate.customerName}</p>
									<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedReadyEstimate.delivery?.email || 'Email not captured'}</p>
								</div>
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{getDraftInvoiceNumber(selectedReadyEstimate)}</p>
									<p class="mt-1 text-xs text-[var(--text-muted)]">{getApprovalDateLabel(selectedReadyEstimate)}</p>
								</div>
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next step</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">Send invoice</p>
									<p class="mt-1 text-xs text-[var(--text-muted)]">Use the delivery packet above to submit this invoice.</p>
								</div>
							</div>
						</div>
					{/if}
				</div>
			{:else}
				<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
					No draft invoices are ready for billing review.
				</div>
			{/if}
		{:else if selectedLifecycleInvoice}
			<div id="invoice-record" class="space-y-4">
				<div class="grid gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(280px,0.42fr)]">
					<div class="flex flex-wrap items-start justify-between gap-3 rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<div>
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
								{selectedLifecycleInvoice.state === 'paid' ? 'Paid invoice' : 'Active invoice'}
							</p>
							<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.invoiceNumber}</h4>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedLifecycleInvoice.customerName} · {selectedLifecycleInvoice.serviceSummary} · {selectedLifecycleInvoice.siteName}</p>
						</div>
						<div class="text-right">
							<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(selectedLifecycleInvoice.amount)}</p>
							<p class="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">{selectedLifecycleInvoice.state}</p>
						</div>
					</div>

					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">Bob</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">Suggestions</p>
							</div>
							<span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-lg shadow-sm">👷</span>
						</div>
						<div class="mt-3 grid gap-2">
							{#each bobSuggestions as suggestion}
								<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2 shadow-sm">
									<p class="text-sm font-semibold text-[var(--text-strong)]">{suggestion.label}</p>
									<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{suggestion.detail}</p>
								</div>
							{/each}
						</div>
					</div>
				</div>

				{#if form?.invoiceActionMessage}
					<p class="rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
						{form.invoiceActionMessage}
					</p>
				{/if}

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Approved by</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.approvedBy}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedLifecycleInvoice.approvedAtUtc ? formatDate(selectedLifecycleInvoice.approvedAtUtc) : 'Date not captured'}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Delivery</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.sentAtUtc ? `Sent ${formatDate(selectedLifecycleInvoice.sentAtUtc)}` : 'Not sent'}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedLifecycleInvoice.customerEmail || selectedLifecycleInvoice.customerPhone || 'No delivery contact'}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Payment</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.paidAtUtc ? `Paid ${formatDate(selectedLifecycleInvoice.paidAtUtc)}` : 'Payment open'}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedLifecycleInvoice.reminderSentAtUtc ? `Reminder ${formatDate(selectedLifecycleInvoice.reminderSentAtUtc)}` : 'No reminder recorded'}</p>
					</div>
				</div>

				<div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice basis</p>
						<div class="mt-3 grid gap-2">
							{#each selectedLifecycleInvoice.lineItems as lineItem}
								<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2 text-sm text-[var(--text-base)] shadow-sm">{lineItem}</div>
							{/each}
						</div>
					</div>

					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice actions</p>
						<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">Complete the billing step from this selected invoice.</p>
						<div class="mt-4 grid gap-2">
							{#if selectedLifecycleInvoice.state !== 'paid'}
								<form method="POST" action="?/recordPayment">
									<input type="hidden" name="invoiceId" value={selectedLifecycleInvoice.id} />
									<button type="submit" class="inline-flex w-full justify-center rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">
										Record payment
									</button>
								</form>
								<form method="POST" action="?/sendReminder">
									<input type="hidden" name="invoiceId" value={selectedLifecycleInvoice.id} />
									<button type="submit" class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
										Send reminder
									</button>
								</form>
							{/if}
							<a href={`/bdr/invoice/${encodeURIComponent(selectedLifecycleInvoice.id)}?returnTo=${encodeURIComponent(invoicesReturnTo)}`} class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
								Open invoice
							</a>
							<button
								type="button"
								class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]"
								onclick={() => copyCustomerLink(getInvoicePacketHref(selectedLifecycleInvoice.id))}
							>
								{copiedCustomerLink === getInvoicePacketHref(selectedLifecycleInvoice.id) ? 'Link copied' : 'Copy customer link'}
							</button>
							<button type="button" class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-muted)] shadow-sm" disabled>
								Email provider not configured
							</button>
							<button type="button" class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-muted)] shadow-sm" disabled>
								SMS provider not configured
							</button>
						</div>
					</div>
				</div>
			</div>
		{:else if selectedInvoice}
			<div id="invoice-record" class="space-y-4">
				<div class="grid gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(280px,0.42fr)]">
					<div class="flex flex-wrap items-start justify-between gap-3 rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<div>
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Invoice record</p>
							<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedInvoice.invoiceNumber}</h4>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedInvoice.customer?.displayName ?? 'Unknown customer'} · {getBillingDeskState(selectedInvoice)} · Due {formatDate(selectedInvoice.dueDateUtc)}</p>
						</div>
						<div class="text-right">
							<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(selectedInvoice.balanceDue)}</p>
							<p class="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">{selectedInvoice.status}</p>
						</div>
					</div>

					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">Bob</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">Suggestions</p>
							</div>
							<span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-lg shadow-sm">👷</span>
						</div>
						<div class="mt-3 grid gap-2">
							{#each bobSuggestions as suggestion}
								<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2 shadow-sm">
									<p class="text-sm font-semibold text-[var(--text-strong)]">{suggestion.label}</p>
									<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{suggestion.detail}</p>
								</div>
							{/each}
						</div>
					</div>
				</div>

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Due timing</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{isOverdue(selectedInvoice) ? `Past due · ${formatDate(selectedInvoice.dueDateUtc)}` : `Due ${formatDate(selectedInvoice.dueDateUtc)}`}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Queue owner</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.owner}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next move</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.nextStep}</p>
					</div>
				</div>

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Billing phase</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.billingPhase}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Payment method</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.paymentMethod}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Amount due</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatCurrency(selectedInvoice.balanceDue)}</p>
					</div>
				</div>

				<div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice detail</p>
						<div class="mt-3 space-y-3">
							<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Check-hold status</p>
								<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.checkHold}</p>
							</div>
							<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next step</p>
								<p class="mt-2 text-sm text-[var(--text-base)]">{selectedInvoice.nextStep}</p>
							</div>
						</div>
					</div>

					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice actions</p>
						<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">Complete the billing step from this selected invoice.</p>
						<div class="mt-4 grid gap-2">
							<button type="button" class="inline-flex w-full justify-center rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">
								Record payment
							</button>
							<button type="button" class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
								Send reminder
							</button>
							<button type="button" class="inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
								Open invoice
							</button>
						</div>
					</div>
				</div>
			</div>
		{:else}
			<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
				No invoices are available in this {activeInvoiceMode} view.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
