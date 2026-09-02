<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildInvoiceViews } from '$lib/mvp-display';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type InvoiceView = ReturnType<typeof buildInvoiceViews>[number];
	type BillingDeskState = 'Overdue' | 'Follow-up' | 'Open' | 'Paid';
	type InvoiceMode = 'ready' | 'active' | 'paid';
	type LifecyclePayment = {
		id: string;
		amount: number;
		method: 'ACH' | 'Card' | 'Check' | 'Cash' | 'Other';
		note?: string;
		receivedAtUtc: string;
		receivedBy: string;
	};
	type ScheduledJob = {
		id: string;
		invoiceId: string;
		scheduledDate: string;
		windowStart: string;
		windowEnd: string;
		crew: string;
		notes?: string;
		scheduledBy: string;
	};
	type ScheduleReadyJob = {
		invoiceId: string;
		amountPaid: number;
		balanceDue: number;
		requiredDepositAmount: number;
		depositPercentRequired: number;
		paidPercent: number;
		isScheduled: boolean;
		scheduledJob?: ScheduledJob;
	};
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
		errors?: string[];
		loadedAtUtc?: string;
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
			payments: LifecyclePayment[];
			customerEmail: string;
			customerPhone: string;
			reviewUrl: string;
			approvedBy: string;
			approvalMethod: 'customer review link';
			createdAtUtc: string;
			updatedAtUtc: string;
			approvedAtUtc?: string;
			sentAtUtc?: string;
			paidAtUtc?: string;
			reminderSentAtUtc?: string;
			lineItems: string[];
		}>;
		billingSettings?: {
			depositPercentRequired: number;
		};
		scheduledJobs?: ScheduledJob[];
		scheduleReadyJobs?: ScheduleReadyJob[];
	};

	let { data, form }: PageProps = $props();
	const pageData = $derived(data as InvoicePageData);

	const allInvoices = $derived(buildInvoiceViews(pageData.invoices, pageData.customers));
	const lifecycleInvoices = $derived(pageData.lifecycleInvoices ?? []);
	const billingSettings = $derived(pageData.billingSettings ?? { depositPercentRequired: 50 });
	const scheduledJobs = $derived(pageData.scheduledJobs ?? []);
	const scheduleReadyJobs = $derived(pageData.scheduleReadyJobs ?? []);
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
				.reduce((sum, invoice) => sum + getLifecycleBalanceDue(invoice), 0)
	);
	const collectedValue = $derived(
		allInvoices.filter((invoice) => isPaid(invoice)).reduce((sum, invoice) => sum + invoice.balanceDue, 0) +
			lifecycleInvoices.reduce((sum, invoice) => sum + getLifecycleAmountPaid(invoice), 0)
	);
	const activeInvoiceCount = $derived(
		allInvoices.filter((invoice) => !isPaid(invoice)).length +
			lifecycleInvoices.filter((invoice) => invoice.state === 'sent' && !isLifecycleInvoicePaid(invoice)).length
	);
	const paidInvoiceCount = $derived(
		allInvoices.filter((invoice) => isPaid(invoice)).length +
			lifecycleInvoices.filter((invoice) => isLifecycleInvoicePaid(invoice)).length
	);
	const totalInvoiceCount = $derived(allInvoices.length + lifecycleInvoices.length);

	let invoiceMode = $state<InvoiceMode>('ready');
	let selectedInvoiceId = $state('');
	let selectedLifecycleInvoiceId = $state('');
	let selectedReadyEstimateId = $state('');
	let reviewingReadyInvoiceId = $state('');
	let sendingReadyInvoiceId = $state('');
	let copiedCustomerLink = $state('');
	let paymentDraftInvoiceId = $state('');
	let paymentAmount = $state('');
	let paymentMethod = $state<'ACH' | 'Card' | 'Check' | 'Cash' | 'Other'>('ACH');
	let paymentNote = $state('');
	let scheduleDraftInvoiceId = $state('');
	let scheduleDate = $state('');
	let scheduleWindowStart = $state('08:00');
	let scheduleWindowEnd = $state('12:00');
	let scheduleCrew = $state('Production crew');
	let scheduleNotes = $state('');
	const activeInvoiceMode = $derived(
		invoiceMode === 'ready' && !approvedEstimateDrafts.length ? 'active' : invoiceMode
	);

	function isPaid(invoice: InvoiceView) {
		return invoice.status.toLowerCase().includes('paid');
	}
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
	function getLifecycleAmountPaid(invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) {
		const paymentTotal = (invoice.payments ?? []).reduce((sum, payment) => sum + payment.amount, 0);
		if (paymentTotal > 0) return Math.min(paymentTotal, invoice.amount);
		if (invoice.state === 'paid' || invoice.paidAtUtc) return invoice.amount;
		return 0;
	}
	function getLifecycleBalanceDue(invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) {
		return Math.max(invoice.amount - getLifecycleAmountPaid(invoice), 0);
	}
	function isLifecycleInvoicePaid(invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) {
		return getLifecycleBalanceDue(invoice) <= 0.01 || invoice.state === 'paid';
	}
	function getLifecycleStateLabel(invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) {
		if (isLifecycleInvoicePaid(invoice)) return 'Paid';
		if (getLifecycleAmountPaid(invoice) > 0) return 'Partial';
		return invoice.state === 'draft' ? 'Draft' : 'Open';
	}
	function getLifecycleRequiredDepositAmount(invoice: { amount: number }) {
		return Math.min(invoice.amount, invoice.amount * (billingSettings.depositPercentRequired / 100));
	}
	function getLifecycleRemainingDepositAmount(invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) {
		return Math.max(getLifecycleRequiredDepositAmount(invoice) - getLifecycleAmountPaid(invoice), 0);
	}
	function canScheduleLifecycleInvoice(invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) {
		return getLifecycleRemainingDepositAmount(invoice) <= 0.01;
	}
	function getDefaultScheduleDate() {
		const date = new Date();
		date.setDate(date.getDate() + 1);
		const year = date.getFullYear();
		const month = String(date.getMonth() + 1).padStart(2, '0');
		const day = String(date.getDate()).padStart(2, '0');
		return `${year}-${month}-${day}`;
	}
	function formatTimeLabel(value: string) {
		const [hoursText = '', minutesText = ''] = value.split(':');
		const hours = Number(hoursText);
		const minutes = Number(minutesText);
		if (Number.isNaN(hours) || Number.isNaN(minutes)) return value;
		return new Date(2026, 0, 1, hours, minutes).toLocaleTimeString('en-US', {
			hour: 'numeric',
			minute: '2-digit'
		});
	}
	const getInvoiceCardStatusBorder = (status: string) => {
		const normalized = status.toLowerCase();
		if (normalized.includes('overdue')) return 'border-t-red-500';
		if (normalized.includes('partial')) return 'border-t-yellow-400';
		if (normalized.includes('paid')) return 'border-t-emerald-500';
		return 'border-t-transparent';
	};
	const setPaymentPreset = (amount: number) => {
		paymentAmount = Math.max(0, amount).toFixed(2);
	};
	const setDepositPaymentPreset = (invoice: { amount: number; payments?: LifecyclePayment[]; state: string; paidAtUtc?: string }) => {
		paymentAmount = Math.max(0, getLifecycleRequiredDepositAmount(invoice) - getLifecycleAmountPaid(invoice)).toFixed(2);
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
					activeInvoiceMode === 'active'
						? invoice.state === 'sent' && !isLifecycleInvoicePaid(invoice)
						: isLifecycleInvoicePaid(invoice)
				)
	);

	const selectedLifecycleInvoice = $derived.by(() => {
		if (activeInvoiceMode === 'ready') return null;
		const current = visibleLifecycleInvoices.find((invoice) => invoice.id === selectedLifecycleInvoiceId);
		return current ?? visibleLifecycleInvoices[0] ?? null;
	});
	const selectedScheduleReadyJob = $derived(
		selectedLifecycleInvoice ? scheduleReadyJobs.find((job) => job.invoiceId === selectedLifecycleInvoice.id) ?? null : null
	);
	const selectedScheduledJob = $derived(
		selectedLifecycleInvoice ? scheduledJobs.find((job) => job.invoiceId === selectedLifecycleInvoice.id) ?? selectedScheduleReadyJob?.scheduledJob ?? null : null
	);
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
			const remainingDeposit = getLifecycleRemainingDepositAmount(selectedLifecycleInvoice);
			return [
				{
					label: selectedScheduledJob ? 'Job scheduled' : canScheduleLifecycleInvoice(selectedLifecycleInvoice) ? 'Schedule the job' : 'Collect deposit',
					detail:
						selectedScheduledJob
							? `${selectedLifecycleInvoice.invoiceNumber} is on the calendar for ${selectedScheduledJob.scheduledDate}.`
							: canScheduleLifecycleInvoice(selectedLifecycleInvoice)
								? `${billingSettings.depositPercentRequired}% deposit is met; production scheduling can start.`
								: `${formatCurrency(remainingDeposit)} more deposit is needed before scheduling.`
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
		{
			label: 'Draft invoices',
			value: String(approvedEstimateDrafts.length),
			detail: 'Customer-approved estimate invoices waiting on billing review',
			icon: '🧾'
		},
		{
			label: 'Receivables',
			value: formatCurrency(receivablesValue),
			detail: 'Outstanding balance visible from the billing desk',
			icon: '💰'
		},
		{
			label: 'Collected',
			value: formatCurrency(collectedValue),
			detail: 'Payments recorded against invoice balances',
			icon: '💵'
		},
		{
			label: 'Invoices',
			value: String(totalInvoiceCount),
			detail: pageData.loadedAtUtc ? `Live data refreshed ${formatDate(pageData.loadedAtUtc)}` : 'Live TurnKeyOps API data',
			icon: '💸'
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

	$effect(() => {
		if (selectedLifecycleInvoice && paymentDraftInvoiceId !== selectedLifecycleInvoice.id) {
			paymentDraftInvoiceId = selectedLifecycleInvoice.id;
			paymentAmount = getLifecycleBalanceDue(selectedLifecycleInvoice).toFixed(2);
			paymentMethod = 'ACH';
			paymentNote = '';
		}
	});

	$effect(() => {
		if (selectedLifecycleInvoice && scheduleDraftInvoiceId !== selectedLifecycleInvoice.id) {
			scheduleDraftInvoiceId = selectedLifecycleInvoice.id;
			scheduleDate = selectedScheduledJob?.scheduledDate ?? getDefaultScheduleDate();
			scheduleWindowStart = selectedScheduledJob?.windowStart ?? '08:00';
			scheduleWindowEnd = selectedScheduledJob?.windowEnd ?? '12:00';
			scheduleCrew = selectedScheduledJob?.crew ?? 'Production crew';
			scheduleNotes = selectedScheduledJob?.notes ?? '';
		}
	});
</script>

{#if pageData.errors?.length}
	<div class="mb-4 rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900" role="alert">
		<p class="font-semibold">Some live billing data could not be loaded.</p>
		<p class="mt-1">{pageData.errors.join(' ')}</p>
		<a href="/bdr/admin/invoices" class="mt-2 inline-block font-semibold underline">Retry</a>
	</div>
{/if}

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
				class={`flex w-full items-center justify-between gap-3 rounded-lg border px-3 py-3 text-left transition ${activeInvoiceMode === 'ready' ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
				onclick={() => (invoiceMode = 'ready')}
			>
				<span class="text-sm font-semibold text-[var(--text-strong)]">Draft invoices</span>
				<span class="min-w-8 text-right text-sm font-semibold text-[var(--text-muted)]">{approvedEstimateDrafts.length}</span>
			</button>

			<button
				type="button"
				class={`flex w-full items-center justify-between gap-3 rounded-lg border px-3 py-3 text-left transition ${activeInvoiceMode === 'active' ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
				onclick={() => (invoiceMode = 'active')}
			>
				<span class="text-sm font-semibold text-[var(--text-strong)]">Active invoices</span>
				<span class="min-w-8 text-right text-sm font-semibold text-[var(--text-muted)]">{activeInvoiceCount}</span>
			</button>

			<button
				type="button"
				class={`flex w-full items-center justify-between gap-3 rounded-lg border px-3 py-3 text-left transition ${activeInvoiceMode === 'paid' ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
				onclick={() => (invoiceMode = 'paid')}
			>
				<span class="text-sm font-semibold text-[var(--text-strong)]">Paid invoices</span>
				<span class="min-w-8 text-right text-sm font-semibold text-[var(--text-muted)]">{paidInvoiceCount}</span>
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
						<p class="mt-1 text-xs text-[var(--text-muted)]">{estimate.siteName} / {estimate.customerName}</p>
						<p class="mt-3 text-lg font-semibold text-[var(--text-strong)]">{formatCurrency(parseApprovedEstimateTotal(estimate))}</p>
						<p class="mt-1 text-xs font-semibold text-emerald-700">$0 collected</p>
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
					{@const status = getLifecycleStateLabel(invoice)}
					<button
						type="button"
						class={`w-full rounded-lg border border-t-4 border-x-transparent border-b-transparent px-3 py-3 text-left transition ${getInvoiceCardStatusBorder(status)} ${selectedLifecycleInvoice?.id === invoice.id ? 'bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'bg-white/80 shadow-sm hover:bg-white'}`}
						onclick={() => {
							selectedLifecycleInvoiceId = invoice.id;
							selectedInvoiceId = '';
						}}
					>
						<p class="text-sm font-semibold text-[var(--text-strong)]">{invoice.invoiceNumber}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{invoice.siteName} / {invoice.customerName}</p>
						<p class="mt-3 text-lg font-semibold text-[var(--text-strong)]">{formatCurrency(invoice.amount)}</p>
						<p class="mt-1 text-xs font-semibold text-emerald-700">{formatCurrency(getLifecycleAmountPaid(invoice))} collected</p>
					</button>
				{/each}
				{#each visibleInvoices as invoice}
					{@const state = getBillingDeskState(invoice)}
					<button
						type="button"
						class={`w-full rounded-lg border border-t-4 border-x-transparent border-b-transparent px-3 py-3 text-left transition ${getInvoiceCardStatusBorder(state)} ${selectedInvoice?.id === invoice.id ? 'bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'bg-white/80 shadow-sm hover:bg-white'}`}
						onclick={() => {
							selectedInvoiceId = invoice.id;
							selectedLifecycleInvoiceId = '';
						}}
					>
						<p class="text-sm font-semibold text-[var(--text-strong)]">{invoice.invoiceNumber}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">
							{invoice.customer?.displayName ?? 'Unknown project'} / {invoice.customer?.primaryContactName ?? invoice.customer?.displayName ?? 'Unknown owner'}
						</p>
						<p class="mt-3 text-lg font-semibold text-[var(--text-strong)]">{formatCurrency(invoice.balanceDue)}</p>
						<p class="mt-1 text-xs font-semibold text-emerald-700">{formatCurrency(isPaid(invoice) ? invoice.balanceDue : 0)} collected</p>
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
									Ready
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
							<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(getLifecycleBalanceDue(selectedLifecycleInvoice))}</p>
							<p class="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Balance due</p>
							{#if getLifecycleAmountPaid(selectedLifecycleInvoice) > 0}
								<p class="mt-2 text-xs font-semibold text-emerald-700">{formatCurrency(getLifecycleAmountPaid(selectedLifecycleInvoice))} collected</p>
							{/if}
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

				<div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Approved by</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.approvedBy}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedLifecycleInvoice.approvedAtUtc ? formatDate(selectedLifecycleInvoice.approvedAtUtc) : 'Date not captured'}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Project description</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.serviceSummary}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedLifecycleInvoice.approvalMethod}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Delivery</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedLifecycleInvoice.sentAtUtc ? `Sent ${formatDate(selectedLifecycleInvoice.sentAtUtc)}` : 'Not sent'}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedLifecycleInvoice.customerEmail || selectedLifecycleInvoice.customerPhone || 'No delivery contact'}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Payment</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">
							{isLifecycleInvoicePaid(selectedLifecycleInvoice)
								? selectedLifecycleInvoice.paidAtUtc
									? `Paid ${formatDate(selectedLifecycleInvoice.paidAtUtc)}`
									: 'Paid'
								: getLifecycleAmountPaid(selectedLifecycleInvoice) > 0
									? 'Partial payment received'
									: 'Payment open'}
						</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">
							Collected {formatCurrency(getLifecycleAmountPaid(selectedLifecycleInvoice))} · Balance {formatCurrency(getLifecycleBalanceDue(selectedLifecycleInvoice))}
						</p>
						{#if selectedLifecycleInvoice.reminderSentAtUtc}
							<p class="mt-1 text-xs text-[var(--text-muted)]">Reminder {formatDate(selectedLifecycleInvoice.reminderSentAtUtc)}</p>
						{/if}
					</div>
				</div>

				{#if selectedLifecycleInvoice.payments.length}
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Payment history</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatCurrency(getLifecycleAmountPaid(selectedLifecycleInvoice))} collected</p>
							</div>
							<p class="text-sm font-semibold text-[var(--text-muted)]">{formatCurrency(getLifecycleBalanceDue(selectedLifecycleInvoice))} balance</p>
						</div>
						<div class="mt-3 grid gap-2">
							{#each selectedLifecycleInvoice.payments as payment}
								<div class="grid gap-2 rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2 text-sm shadow-sm sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
									<div>
										<p class="font-semibold text-[var(--text-strong)]">{formatCurrency(payment.amount)} · {payment.method}</p>
										<p class="mt-1 text-xs text-[var(--text-muted)]">{payment.note || 'Payment recorded'} · {formatDate(payment.receivedAtUtc)}</p>
									</div>
									<p class="text-xs font-semibold text-[var(--text-muted)]">{payment.receivedBy}</p>
								</div>
							{/each}
						</div>
					</div>
				{/if}

				<div class={`rounded-lg p-4 shadow-[var(--shell-shadow)] ${selectedScheduledJob ? 'bg-emerald-50' : canScheduleLifecycleInvoice(selectedLifecycleInvoice) ? 'bg-white/90 ring-1 ring-emerald-200' : 'bg-amber-50'}`}>
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div>
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Schedule readiness</p>
							<h5 class="mt-2 text-lg font-semibold text-[var(--text-strong)]">
								{selectedScheduledJob
									? 'Job scheduled'
									: canScheduleLifecycleInvoice(selectedLifecycleInvoice)
										? 'Ready to schedule'
										: 'Deposit needed'}
							</h5>
							<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">
								{formatCurrency(getLifecycleAmountPaid(selectedLifecycleInvoice))} collected toward a {billingSettings.depositPercentRequired}% deposit gate.
							</p>
							{#if !canScheduleLifecycleInvoice(selectedLifecycleInvoice)}
								<p class="mt-1 text-sm font-semibold text-amber-700">
									Collect {formatCurrency(getLifecycleRemainingDepositAmount(selectedLifecycleInvoice))} more to unlock scheduling.
								</p>
							{/if}
						</div>
						<div class="rounded-lg bg-white/80 px-3 py-2 text-sm shadow-sm">
							<p class="text-xs uppercase tracking-[0.16em] text-[var(--muted)]">Required deposit</p>
							<p class="mt-1 font-semibold text-[var(--text-strong)]">{formatCurrency(getLifecycleRequiredDepositAmount(selectedLifecycleInvoice))}</p>
						</div>
					</div>

					{#if selectedScheduledJob}
						<div class="mt-4 grid gap-3 md:grid-cols-3">
							<div class="rounded-lg bg-white/90 p-3 shadow-sm">
								<p class="text-xs uppercase tracking-[0.16em] text-[var(--muted)]">Production date</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatDate(selectedScheduledJob.scheduledDate)}</p>
							</div>
							<div class="rounded-lg bg-white/90 p-3 shadow-sm">
								<p class="text-xs uppercase tracking-[0.16em] text-[var(--muted)]">Window</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatTimeLabel(selectedScheduledJob.windowStart)} - {formatTimeLabel(selectedScheduledJob.windowEnd)}</p>
							</div>
							<div class="rounded-lg bg-white/90 p-3 shadow-sm">
								<p class="text-xs uppercase tracking-[0.16em] text-[var(--muted)]">Crew</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedScheduledJob.crew}</p>
							</div>
						</div>
						<div class="mt-4 flex flex-wrap gap-2">
							<a
								href={`/bdr/admin/jobs?job=${encodeURIComponent(selectedScheduledJob.id)}`}
								class="inline-flex rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]"
							>
								Open job
							</a>
							<a href="/bdr/admin/calendar" class="inline-flex rounded-md bg-white px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
								Open calendar
							</a>
						</div>
					{:else if canScheduleLifecycleInvoice(selectedLifecycleInvoice)}
						<form method="POST" action="?/scheduleJob" class="mt-4 grid gap-3 rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm lg:grid-cols-2">
							<input type="hidden" name="invoiceId" value={selectedLifecycleInvoice.id} />
							<label class="grid gap-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Production date</span>
								<input
									type="date"
									name="scheduledDate"
									bind:value={scheduleDate}
									class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
								/>
							</label>
							<label class="grid gap-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Crew</span>
								<input
									name="crew"
									bind:value={scheduleCrew}
									class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
								/>
							</label>
							<label class="grid gap-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Start</span>
								<input
									type="time"
									name="windowStart"
									bind:value={scheduleWindowStart}
									class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
								/>
							</label>
							<label class="grid gap-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">End</span>
								<input
									type="time"
									name="windowEnd"
									bind:value={scheduleWindowEnd}
									class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
								/>
							</label>
							<label class="grid gap-1 lg:col-span-2">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Schedule notes</span>
								<input
									name="scheduleNotes"
									bind:value={scheduleNotes}
									class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
									placeholder="Access notes, prep constraints, production handoff..."
								/>
							</label>
							<button type="submit" class="inline-flex justify-center rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)] lg:col-span-2">
								Schedule job
							</button>
						</form>
					{/if}
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
							{#if !isLifecycleInvoicePaid(selectedLifecycleInvoice)}
								<form method="POST" action="?/recordPayment" class="rounded-lg bg-[var(--shell-panel-strong)] p-3 shadow-sm">
									<input type="hidden" name="invoiceId" value={selectedLifecycleInvoice.id} />
									<div class="flex items-start justify-between gap-3">
										<div>
											<p class="text-sm font-semibold text-[var(--text-strong)]">Record payment</p>
											<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Use partial amounts for deposits, draws, or retainers.</p>
										</div>
										<span class="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-[var(--accent-text)]">
											{formatCurrency(getLifecycleBalanceDue(selectedLifecycleInvoice))} due
										</span>
									</div>
									<div class="mt-3 grid gap-2 sm:grid-cols-[minmax(0,1fr)_9rem]">
										<label class="grid gap-1">
											<span class="text-xs font-semibold text-[var(--text-muted)]">Amount</span>
											<input
												name="paymentAmount"
												bind:value={paymentAmount}
												inputmode="decimal"
												class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
												placeholder="0.00"
											/>
										</label>
										<label class="grid gap-1">
											<span class="text-xs font-semibold text-[var(--text-muted)]">Method</span>
											<select
												name="paymentMethod"
												bind:value={paymentMethod}
												class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
											>
												<option>ACH</option>
												<option>Card</option>
												<option>Check</option>
												<option>Cash</option>
												<option>Other</option>
											</select>
										</label>
									</div>
									<div class="mt-2 flex flex-wrap gap-2">
										<button
											type="button"
											class="rounded-md bg-white px-3 py-2 text-xs font-semibold text-[var(--accent-text)] shadow-sm transition hover:bg-[var(--accent-soft)]"
											onclick={() => setDepositPaymentPreset(selectedLifecycleInvoice)}
										>
											{billingSettings.depositPercentRequired}% deposit
										</button>
										<button
											type="button"
											class="rounded-md bg-white px-3 py-2 text-xs font-semibold text-[var(--accent-text)] shadow-sm transition hover:bg-[var(--accent-soft)]"
											onclick={() => setPaymentPreset(getLifecycleBalanceDue(selectedLifecycleInvoice))}
										>
											Remaining balance
										</button>
									</div>
									<label class="mt-3 grid gap-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Note</span>
										<input
											name="paymentNote"
											bind:value={paymentNote}
											class="h-11 rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]"
											placeholder="Deposit before production, progress draw, final payment..."
										/>
									</label>
									<button type="submit" class="mt-3 inline-flex w-full justify-center rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">
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
