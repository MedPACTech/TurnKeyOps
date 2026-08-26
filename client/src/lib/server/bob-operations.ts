import { loadBdrInvoices, getBdrInvoiceBalanceDue } from '$lib/server/bdr-invoices';
import { loadBdrScheduledJobs } from '$lib/server/bdr-job-scheduling';
import { loadQuoteRequests, recordQuoteRequestActivity } from '$lib/server/quote-requests';
import { updateBdrInvoiceState } from '$lib/server/bdr-invoices';
import { addBdrScheduledJobNote } from '$lib/server/bdr-job-scheduling';
import { resolveMvpScaffold } from '$lib/server/mvp';
import { bdrTenant, type TenantDefinition } from '$lib/config/tenants';

export type BobActionKind = 'invoice-reminder' | 'quote-follow-up' | 'job-note' | 'open-record';

export type BobRecommendation = {
	id: string;
	kind: BobActionKind;
	title: string;
	reason: string;
	impact: string;
	href: string;
	targetId?: string;
	draft?: string;
	approvalRequired: boolean;
};

export type BobBriefing = {
	generatedAtUtc: string;
	headline: string;
	summary: string;
	metrics: Array<{ label: string; value: string; detail: string; tone: 'neutral' | 'warning' | 'positive' }>;
	attention: Array<{ title: string; detail: string; href: string; severity: 'high' | 'medium' | 'low' }>;
	recommendations: BobRecommendation[];
	context: {
		quoteRequests: Array<Record<string, unknown>>;
		invoices: Array<Record<string, unknown>>;
		jobs: Array<Record<string, unknown>>;
		estimates: Record<string, unknown>;
	};
};

export type BobEstimateFollowup = {
	id: string;
	customer: string;
	project: string;
	status: string;
	reason: string;
	nextAction: string;
	ageDays: number;
	href: string;
	priority: 'high' | 'medium' | 'low';
};

const money = (value: number) =>
	new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(value);

const ageInDays = (value: string | null | undefined) => {
	const timestamp = value ? new Date(value).getTime() : Date.now();
	return Math.max(0, Math.floor((Date.now() - timestamp) / 86_400_000));
};

export const buildEstimateFollowups = async (
	fetch: typeof globalThis.fetch,
	tenant: TenantDefinition = bdrTenant
): Promise<BobEstimateFollowup[]> => {
	const { requests } = await loadQuoteRequests(fetch, tenant.id);
	return requests
		.filter((request) =>
			['qualified', 'inspection-scheduled', 'estimate-drafted', 'estimate-sent'].includes(request.status)
		)
		.map((request) => {
			const latestActivity = request.timeline.at(-1)?.occurredAtUtc ?? request.submittedAtUtc;
			const ageDays = ageInDays(latestActivity);
			const state =
				request.status === 'estimate-sent'
					? {
							reason: ageDays
								? `Customer response has been pending for ${ageDays} day${ageDays === 1 ? '' : 's'}.`
								: 'The estimate was sent and is awaiting the customer.',
							nextAction: request.nextAction || 'Confirm receipt and ask whether the customer has questions.'
						}
					: request.status === 'estimate-drafted'
						? {
								reason: 'The internal estimate is drafted but has not been sent.',
								nextAction: request.nextAction || 'Review pricing, assumptions, and customer-ready terms.'
							}
						: request.status === 'inspection-scheduled'
							? {
									reason: 'The opportunity has reached site inspection but not an estimate draft.',
									nextAction: request.nextAction || 'Capture visit findings and prepare the estimate.'
								}
							: {
									reason: 'The request is qualified and ready for estimating.',
									nextAction: request.nextAction || 'Assign an estimator and start the estimate.'
								};
			return {
				id: request.id,
				customer: request.contactName || request.customerName,
				project: request.serviceType || request.projectType || 'Project',
				status: request.status,
				reason: state.reason,
				nextAction: state.nextAction,
				ageDays,
				href: `${tenant.adminPath.replace(/\/bob$/, '')}/estimates?request=${encodeURIComponent(request.id)}`,
				priority:
					(request.status === 'estimate-sent' && ageDays >= 3) ||
					(request.status !== 'estimate-sent' && ageDays >= 5)
						? 'high'
						: ageDays >= 2
							? 'medium'
							: 'low'
			} satisfies BobEstimateFollowup;
		})
		.sort((left, right) => {
			const priority = { high: 3, medium: 2, low: 1 };
			return priority[right.priority] - priority[left.priority] || right.ageDays - left.ageDays;
		});
};

export const buildBobBriefing = async (
	fetch: typeof globalThis.fetch,
	tenant: TenantDefinition = bdrTenant
): Promise<BobBriefing> => {
	if (tenant.slug !== 'bdr') {
		const { requests } = await loadQuoteRequests(fetch, tenant.id);
		const activeRequests = requests.filter((request) => !['won', 'closed'].includes(request.status));
		const newRequests = requests.filter((request) => request.status === 'new');
		const blockedRequests = requests.filter(
			(request) =>
				request.status === 'needs-info' ||
				Boolean(request.qualification?.missingInfoReasonCodes?.length)
		);
		const base = tenant.adminPath.replace(/\/bob$/, '');
		const attention: BobBriefing['attention'] = [];
		if (newRequests.length) {
			attention.push({
				title: `${newRequests.length} new request${newRequests.length === 1 ? '' : 's'} need first response`,
				detail: 'Confirm the property, clearing scope, access, disposal plan, and assessment timing.',
				href: `${base}/requests`,
				severity: 'high'
			});
		}
		if (blockedRequests.length) {
			attention.push({
				title: `${blockedRequests.length} request${blockedRequests.length === 1 ? '' : 's'} are blocked`,
				detail: 'Acreage, vegetation, access, disposal, or customer details are still missing.',
				href: `${base}/requests`,
				severity: 'medium'
			});
		}
		const recommendations: BobRecommendation[] = newRequests.slice(0, 3).map((request) => ({
			id: `quote-${request.id}`,
			kind: 'quote-follow-up',
			title: `Prepare first response for ${request.contactName || request.customerName}`,
			reason: request.need || request.message || 'A new land-clearing request is waiting for review.',
			impact: 'Moves the request toward a property assessment and estimate.',
			href: `${base}/requests?request=${encodeURIComponent(request.id)}`,
			targetId: request.id,
			draft: `Hi ${request.contactName || request.customerName}, thanks for contacting ${tenant.name}. We received your request for ${request.serviceType || request.projectType || 'land-clearing work'} and will follow up with the next step shortly.`,
			approvalRequired: true
		}));
		return {
			generatedAtUtc: new Date().toISOString(),
			headline: attention.length
				? `${attention.length} operating priorities need a decision`
				: 'The land-clearing pipeline is clear',
			summary: `Bob reviewed ${requests.length} ${tenant.shortName} requests and the active estimate pipeline.`,
			metrics: [
				{ label: 'Needs attention', value: String(attention.length), detail: 'Exceptions Bob surfaced', tone: attention.length ? 'warning' : 'positive' },
				{ label: 'Open requests', value: String(activeRequests.length), detail: `${newRequests.length} waiting on first response`, tone: newRequests.length ? 'warning' : 'neutral' },
				{ label: 'Blocked', value: String(blockedRequests.length), detail: 'Waiting on job-site or scope details', tone: blockedRequests.length ? 'warning' : 'positive' }
			],
			attention,
			recommendations,
			context: {
				quoteRequests: requests.slice(0, 20).map((request) => ({
					id: request.id,
					customer: request.contactName || request.customerName,
					address: request.serviceAddress,
					status: request.status,
					service: request.serviceType || request.projectType,
					scope: request.need || request.message,
					timeline: request.requestedTimeline || request.preferredTimeline,
					nextAction: request.nextAction,
					missingInfo: request.qualification?.missingInfoReasonCodes ?? []
				})),
				invoices: [],
				jobs: [],
				estimates: { count: requests.filter((request) => request.status.includes('estimate')).length }
			}
		};
	}
	const [{ requests }, invoices, jobs, { snapshot }] = await Promise.all([
		loadQuoteRequests(fetch),
		loadBdrInvoices(fetch),
		loadBdrScheduledJobs(fetch),
		resolveMvpScaffold(fetch)
	]);

	const activeQuotes = requests.filter((request) => !['won', 'closed'].includes(request.status));
	const newQuotes = requests.filter((request) => request.status === 'new');
	const firstResponseCandidates = newQuotes.filter(
		(request) => !request.timeline.some((event) => event.label === 'Bob follow-up approved')
	);
	const blockedQuotes = requests.filter(
		(request) => request.status === 'needs-info' || request.qualification?.missingInfoReasonCodes?.length
	);
	const unpaidInvoices = invoices.filter((invoice) => getBdrInvoiceBalanceDue(invoice) > 0.01);
	const reminderCandidates = unpaidInvoices.filter((invoice) => !invoice.reminderSentAtUtc);
	const unpaidBalance = unpaidInvoices.reduce((sum, invoice) => sum + getBdrInvoiceBalanceDue(invoice), 0);
	const activeJobs = jobs.filter((job) => ['scheduled', 'in-progress', 'on-hold'].includes(job.status));
	const heldJobs = jobs.filter((job) => job.status === 'on-hold');
	const unconfirmedJobs = activeJobs.filter((job) => job.planning.customer.confirmationStatus !== 'confirmed');

	const attention: BobBriefing['attention'] = [];
	if (newQuotes.length) {
		attention.push({
			title: `${newQuotes.length} new quote request${newQuotes.length === 1 ? '' : 's'} need first response`,
			detail: 'Triage scope, confirm contact details, and assign the next office action.',
			href: '/bdr/admin/requests',
			severity: 'high'
		});
	}
	if (blockedQuotes.length) {
		attention.push({
			title: `${blockedQuotes.length} quote${blockedQuotes.length === 1 ? '' : 's'} are blocked`,
			detail: 'Missing scope, site, attachment, contact, or timing information is preventing progress.',
			href: '/bdr/admin/requests',
			severity: 'medium'
		});
	}
	if (unpaidInvoices.length) {
		attention.push({
			title: `${money(unpaidBalance)} remains open across ${unpaidInvoices.length} invoice${unpaidInvoices.length === 1 ? '' : 's'}`,
			detail: 'Review customer posture before sending payment reminders.',
			href: '/bdr/admin/invoices',
			severity: unpaidBalance > 25000 ? 'high' : 'medium'
		});
	}
	if (unconfirmedJobs.length || heldJobs.length) {
		attention.push({
			title: `${unconfirmedJobs.length + heldJobs.length} production exception${unconfirmedJobs.length + heldJobs.length === 1 ? '' : 's'} need attention`,
			detail: `${unconfirmedJobs.length} customer confirmation issue(s) and ${heldJobs.length} held job(s).`,
			href: '/bdr/admin/jobs',
			severity: 'medium'
		});
	}

	const recommendations: BobRecommendation[] = [];
	for (const request of firstResponseCandidates.slice(0, 2)) {
		recommendations.push({
			id: `quote-${request.id}`,
			kind: 'quote-follow-up',
			title: `Prepare first response for ${request.contactName || request.customerName}`,
			reason: request.need || request.message || 'New public-site request is waiting for office triage.',
			impact: 'Protects response time and moves the request toward qualification.',
			href: `/bdr/admin/requests?request=${encodeURIComponent(request.id)}`,
			targetId: request.id,
			draft: `Hi ${request.contactName || request.customerName}, thanks for reaching out to BDR. We received your request for ${request.serviceType || request.projectType || 'your project'}. We are reviewing the details now and will follow up with the next step shortly.`,
			approvalRequired: true
		});
	}
	for (const invoice of reminderCandidates.slice(0, 2)) {
		const balance = getBdrInvoiceBalanceDue(invoice);
		recommendations.push({
			id: `invoice-${invoice.id}`,
			kind: 'invoice-reminder',
			title: `Draft payment reminder for ${invoice.customerName}`,
			reason: `${invoice.invoiceNumber} has an open balance of ${money(balance)}.`,
			impact: 'Creates a documented collection touch without changing the balance.',
			href: '/bdr/admin/invoices',
			targetId: invoice.id,
			draft: `Hi ${invoice.customerName}, this is a friendly reminder that ${money(balance)} remains due on ${invoice.invoiceNumber}. Please let us know if you need another copy or have any questions.`,
			approvalRequired: true
		});
	}
	for (const job of [...heldJobs, ...unconfirmedJobs].slice(0, 1)) {
		recommendations.push({
			id: `job-${job.id}`,
			kind: 'job-note',
			title: `Add an exception note to ${job.siteName || job.customerName}`,
			reason:
				job.status === 'on-hold'
					? 'The job is on hold and needs a visible recovery action.'
					: 'Customer confirmation is not complete.',
			impact: 'Keeps the production record legible for the office and crew.',
			href: `/bdr/admin/jobs?job=${encodeURIComponent(job.id)}`,
			targetId: job.id,
			draft:
				job.status === 'on-hold'
					? 'Bob review: confirm hold reason, owner, and next recovery date before dispatch.'
					: 'Bob review: customer confirmation is still pending; verify the production window before dispatch.',
			approvalRequired: true
		});
	}

	return {
		generatedAtUtc: new Date().toISOString(),
		headline: attention.length ? `${attention.length} operating priorities need a decision` : 'The back office is clear',
		summary: `Bob reviewed ${requests.length} quote requests, ${invoices.length} invoices, ${jobs.length} production jobs, and the current estimate pipeline.`,
		metrics: [
			{
				label: 'Needs attention',
				value: String(attention.length),
				detail: 'Cross-workflow exceptions Bob surfaced',
				tone: attention.length ? 'warning' : 'positive'
			},
			{
				label: 'Open quotes',
				value: String(activeQuotes.length),
				detail: `${newQuotes.length} waiting on first response`,
				tone: newQuotes.length ? 'warning' : 'neutral'
			},
			{
				label: 'Open receivables',
				value: money(unpaidBalance),
				detail: `${unpaidInvoices.length} invoice${unpaidInvoices.length === 1 ? '' : 's'} with balance due`,
				tone: unpaidBalance ? 'warning' : 'positive'
			},
			{
				label: 'Active production',
				value: String(activeJobs.length),
				detail: `${heldJobs.length} currently on hold`,
				tone: heldJobs.length ? 'warning' : 'neutral'
			}
		],
		attention,
		recommendations,
		context: {
			quoteRequests: requests.slice(0, 12).map((request) => ({
				id: request.id,
				customer: request.contactName || request.customerName,
				company: request.companyName,
				email: request.email,
				phone: request.phone,
				address: request.serviceAddress,
				status: request.status,
				service: request.serviceType || request.projectType,
				scope: request.need || request.message,
				timeline: request.requestedTimeline || request.preferredTimeline,
				nextAction: request.nextAction,
				updatedAtUtc: request.timeline.at(-1)?.occurredAtUtc ?? request.submittedAtUtc,
				missingInfo: request.qualification?.missingInfoReasonCodes ?? []
			})),
			invoices: invoices.slice(0, 12).map((invoice) => ({
				id: invoice.id,
				number: invoice.invoiceNumber,
				customer: invoice.customerName,
				state: invoice.state,
				amount: invoice.amount,
				balanceDue: getBdrInvoiceBalanceDue(invoice),
				lastReminderAtUtc: invoice.reminderSentAtUtc
			})),
			jobs: jobs.slice(0, 12).map((job) => ({
				id: job.id,
				customer: job.customerName,
				site: job.siteName,
				status: job.status,
				date: job.scheduledDate,
				crew: job.crew,
				customerConfirmation: job.planning.customer.confirmationStatus,
				checklistComplete: Object.values(job.planning.checklist).filter(Boolean).length
			})),
			estimates: {
				count: snapshot.summary.estimateCount,
				value: snapshot.summary.estimateValue,
				pipelineValue: snapshot.summary.pipelineValue
			}
		}
	};
};

export const executeBobRecommendation = async (
	fetch: typeof globalThis.fetch,
	recommendation: BobRecommendation
) => {
	if (!recommendation.targetId) throw new Error('Bob action is missing its target record.');

	switch (recommendation.kind) {
		case 'invoice-reminder':
			await updateBdrInvoiceState(recommendation.targetId, { reminder: true });
			return 'Payment reminder approved and recorded.';
		case 'quote-follow-up':
			await recordQuoteRequestActivity(fetch, {
				id: recommendation.targetId,
				label: 'Bob follow-up approved',
				note: recommendation.draft,
				type: 'operator-updated',
				nextAction: 'Send approved follow-up and continue qualification.'
			});
			return 'Customer follow-up approved and recorded on the quote.';
		case 'job-note':
			await addBdrScheduledJobNote(recommendation.targetId, {
				note: recommendation.draft || 'Bob exception review approved.',
				actor: 'Bob · approved by office admin'
			});
			return 'Production note approved and added to the job.';
		default:
			return 'Record opened for operator review.';
	}
};
