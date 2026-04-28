export type QuoteRequestPriority = 'standard' | 'priority' | 'emergency';
export type QuoteRequestStatus =
	| 'new'
	| 'in-review'
	| 'needs-info'
	| 'qualified'
	| 'contacted'
	| 'inspection-scheduled'
	| 'estimate-drafted'
	| 'estimate-sent'
	| 'won'
	| 'closed';

export type QuoteRequestAttachment = {
	id: string;
	fileName: string;
	contentType: string;
	sizeBytes: number;
	uploadedAtUtc: string;
	tenantId?: string;
	blobContainer?: string;
	blobName?: string;
	blobUrl?: string;
};

export type QuoteRequestSubmittedPayload = {
	companyName: string;
	contactName: string;
	email: string;
	phone: string;
	siteName: string;
	serviceAddress: string;
	serviceType: string;
	propertyType: string;
	requestedTimeline: string;
	priority: QuoteRequestPriority;
	need: string;
	attachments: QuoteRequestAttachment[];
};

export type QuoteRequestTimelineEvent = {
	id: string;
	occurredAtUtc: string;
	type: 'submitted' | 'operator-updated';
	actor: string;
	label: string;
	payload?: QuoteRequestSubmittedPayload;
};

export type QuoteRequest = {
	id: string;
	submittedAtUtc: string;
	companyName: string;
	contactName: string;
	customerName: string;
	email: string;
	phone: string;
	siteName: string;
	serviceAddress: string;
	serviceType: string;
	projectType: string;
	propertyType: string;
	requestedTimeline: string;
	preferredTimeline: string;
	priority: QuoteRequestPriority;
	need: string;
	message: string;
	attachments: QuoteRequestAttachment[];
	source: 'public-site' | 'office' | 'referral';
	status: QuoteRequestStatus;
	assignedTo: string;
	nextAction: string;
	intakeSummary: string;
	submittedPayload: QuoteRequestSubmittedPayload;
	timeline: QuoteRequestTimelineEvent[];
};

export type QuoteRequestFormInput = {
	id?: string;
	companyName: string;
	contactName: string;
	email: string;
	phone: string;
	siteName: string;
	serviceAddress: string;
	serviceType: string;
	propertyType: string;
	requestedTimeline: string;
	priority: QuoteRequestPriority;
	need: string;
	attachments: QuoteRequestAttachment[];
};

export const quoteRequestStatuses: QuoteRequestStatus[] = [
	'new',
	'in-review',
	'needs-info',
	'qualified',
	'contacted',
	'inspection-scheduled',
	'estimate-drafted',
	'estimate-sent',
	'won',
	'closed'
];

export const quoteRequestStatusMeta: Record<
	QuoteRequestStatus,
	{ label: string; detail: string; tone: 'amber' | 'blue' | 'violet' | 'emerald' | 'slate' }
> = {
	new: { label: 'New', detail: 'Needs office triage and first response.', tone: 'amber' },
	'in-review': { label: 'In Review', detail: 'Office is reviewing scope, contact details, and next steps.', tone: 'blue' },
	'needs-info': { label: 'Needs Info', detail: 'Customer follow-up is needed before qualification can finish.', tone: 'amber' },
	qualified: { label: 'Qualified', detail: 'Request has enough context to schedule or estimate.', tone: 'emerald' },
	contacted: { label: 'Contacted', detail: 'BDR has made first contact and is qualifying scope.', tone: 'blue' },
	'inspection-scheduled': {
		label: 'Inspection scheduled',
		detail: 'Site visit is booked and waiting on field follow-through.',
		tone: 'violet'
	},
	'estimate-drafted': {
		label: 'Estimate drafted',
		detail: 'Scope and internal costing are being finalized.',
		tone: 'violet'
	},
	'estimate-sent': {
		label: 'Estimate sent',
		detail: 'Customer has the packet and follow-up is active.',
		tone: 'blue'
	},
	won: { label: 'Won', detail: 'Request converted and is ready for schedule handoff.', tone: 'emerald' },
	closed: { label: 'Closed', detail: 'Request was declined, lost, or archived.', tone: 'slate' }
};

export const quoteRequestStatusOptions = quoteRequestStatuses.map((status) => ({
	value: status,
	label: quoteRequestStatusMeta[status].label
}));

export const seededQuoteRequests: QuoteRequest[] = [
	{
		id: 'qr-storm-lakeview',
		submittedAtUtc: '2026-03-30T13:25:00.000Z',
		companyName: 'Lakeview Place',
		contactName: 'Melissa Carter',
		customerName: 'Melissa Carter',
		email: 'melissa@lakeviewplace.com',
		phone: '(704) 555-0113',
		siteName: 'Lakeview Place residence',
		serviceAddress: '421 Lakeview Place, Charlotte, NC',
		serviceType: 'Storm damage roof replacement',
		projectType: 'Storm damage roof replacement',
		propertyType: 'Residential',
		requestedTimeline: 'ASAP this week',
		preferredTimeline: 'ASAP this week',
		priority: 'emergency',
		need: 'We had a leak after the last storm and need someone to inspect the roof and interior stain damage.',
		message: 'We had a leak after the last storm and need someone to inspect the roof and interior stain damage.',
		attachments: [],
		source: 'public-site',
		status: 'new',
		assignedTo: 'Office intake',
		nextAction: 'Call within 15 minutes and offer same-day inspection slot.',
		intakeSummary: 'Leak + storm damage + homeowner needs immediate response.',
		submittedPayload: {
			companyName: 'Lakeview Place',
			contactName: 'Melissa Carter',
			email: 'melissa@lakeviewplace.com',
			phone: '(704) 555-0113',
			siteName: 'Lakeview Place residence',
			serviceAddress: '421 Lakeview Place, Charlotte, NC',
			serviceType: 'Storm damage roof replacement',
			propertyType: 'Residential',
			requestedTimeline: 'ASAP this week',
			priority: 'emergency',
			need: 'We had a leak after the last storm and need someone to inspect the roof and interior stain damage.',
			attachments: []
		},
		timeline: [
			{
				id: 'qr-storm-lakeview-submitted',
				occurredAtUtc: '2026-03-30T13:25:00.000Z',
				type: 'submitted',
				actor: 'Customer Admin',
				label: 'Quote request submitted',
				payload: {
					companyName: 'Lakeview Place',
					contactName: 'Melissa Carter',
					email: 'melissa@lakeviewplace.com',
					phone: '(704) 555-0113',
					siteName: 'Lakeview Place residence',
					serviceAddress: '421 Lakeview Place, Charlotte, NC',
					serviceType: 'Storm damage roof replacement',
					propertyType: 'Residential',
					requestedTimeline: 'ASAP this week',
					priority: 'emergency',
					need: 'We had a leak after the last storm and need someone to inspect the roof and interior stain damage.',
					attachments: []
				}
			}
		]
	},
	{
		id: 'qr-elmwood-hoa',
		submittedAtUtc: '2026-03-30T11:10:00.000Z',
		companyName: 'Elmwood HOA',
		contactName: 'Sandra Holt',
		customerName: 'Sandra Holt',
		email: 'sholt@elmwoodhoa.org',
		phone: '(704) 555-0141',
		siteName: 'Elmwood Commons',
		serviceAddress: '900 Elmwood Commons, Huntersville, NC',
		serviceType: 'Multi-building inspection and quote',
		projectType: 'Multi-building inspection and quote',
		propertyType: 'HOA / multi-family',
		requestedTimeline: 'Need proposal before next board meeting',
		preferredTimeline: 'Need proposal before next board meeting',
		priority: 'priority',
		need: 'Our board needs a quote for repairs across three buildings and wants phased pricing options.',
		message: 'Our board needs a quote for repairs across three buildings and wants phased pricing options.',
		attachments: [],
		source: 'referral',
		status: 'contacted',
		assignedTo: 'Ella - office admin',
		nextAction: 'Send board-ready scope checklist before tomorrow noon.',
		intakeSummary: 'HOA board timing matters more than speed; quote needs phased options.',
		submittedPayload: {
			companyName: 'Elmwood HOA',
			contactName: 'Sandra Holt',
			email: 'sholt@elmwoodhoa.org',
			phone: '(704) 555-0141',
			siteName: 'Elmwood Commons',
			serviceAddress: '900 Elmwood Commons, Huntersville, NC',
			serviceType: 'Multi-building inspection and quote',
			propertyType: 'HOA / multi-family',
			requestedTimeline: 'Need proposal before next board meeting',
			priority: 'priority',
			need: 'Our board needs a quote for repairs across three buildings and wants phased pricing options.',
			attachments: []
		},
		timeline: [
			{
				id: 'qr-elmwood-hoa-submitted',
				occurredAtUtc: '2026-03-30T11:10:00.000Z',
				type: 'submitted',
				actor: 'Customer Admin',
				label: 'Quote request submitted',
				payload: {
					companyName: 'Elmwood HOA',
					contactName: 'Sandra Holt',
					email: 'sholt@elmwoodhoa.org',
					phone: '(704) 555-0141',
					siteName: 'Elmwood Commons',
					serviceAddress: '900 Elmwood Commons, Huntersville, NC',
					serviceType: 'Multi-building inspection and quote',
					propertyType: 'HOA / multi-family',
					requestedTimeline: 'Need proposal before next board meeting',
					priority: 'priority',
					need: 'Our board needs a quote for repairs across three buildings and wants phased pricing options.',
					attachments: []
				}
			}
		]
	},
	{
		id: 'qr-riverside-retail',
		submittedAtUtc: '2026-03-29T16:40:00.000Z',
		companyName: 'Riverside Retail',
		contactName: 'Marcus Wynn',
		customerName: 'Marcus Wynn',
		email: 'mwynn@riversideretail.com',
		phone: '(704) 555-0187',
		siteName: 'Riverside Commerce Dr',
		serviceAddress: '1130 Riverside Commerce Dr, Gastonia, NC',
		serviceType: 'Commercial flat roof repair',
		projectType: 'Commercial flat roof repair',
		propertyType: 'Commercial',
		requestedTimeline: 'After-hours site walk next week',
		preferredTimeline: 'After-hours site walk next week',
		priority: 'standard',
		need: 'Need repair pricing for recurring ponding and flashing issues behind two tenant units.',
		message: 'Need repair pricing for recurring ponding and flashing issues behind two tenant units.',
		attachments: [],
		source: 'office',
		status: 'inspection-scheduled',
		assignedTo: 'Estimator queue',
		nextAction: 'Confirm ladder access and after-hours contact before Tuesday.',
		intakeSummary: 'Commercial repair request already qualified and ready for site walk.',
		submittedPayload: {
			companyName: 'Riverside Retail',
			contactName: 'Marcus Wynn',
			email: 'mwynn@riversideretail.com',
			phone: '(704) 555-0187',
			siteName: 'Riverside Commerce Dr',
			serviceAddress: '1130 Riverside Commerce Dr, Gastonia, NC',
			serviceType: 'Commercial flat roof repair',
			propertyType: 'Commercial',
			requestedTimeline: 'After-hours site walk next week',
			priority: 'standard',
			need: 'Need repair pricing for recurring ponding and flashing issues behind two tenant units.',
			attachments: []
		},
		timeline: [
			{
				id: 'qr-riverside-retail-submitted',
				occurredAtUtc: '2026-03-29T16:40:00.000Z',
				type: 'submitted',
				actor: 'Customer Admin',
				label: 'Quote request submitted',
				payload: {
					companyName: 'Riverside Retail',
					contactName: 'Marcus Wynn',
					email: 'mwynn@riversideretail.com',
					phone: '(704) 555-0187',
					siteName: 'Riverside Commerce Dr',
					serviceAddress: '1130 Riverside Commerce Dr, Gastonia, NC',
					serviceType: 'Commercial flat roof repair',
					propertyType: 'Commercial',
					requestedTimeline: 'After-hours site walk next week',
					priority: 'standard',
					need: 'Need repair pricing for recurring ponding and flashing issues behind two tenant units.',
					attachments: []
				}
			}
		]
	}
];

const statusOrder = new Map(quoteRequestStatuses.map((status, index) => [status, index]));
const unassignedOwnerLabels = new Set(['', 'office intake', 'estimator queue', 'unassigned']);

export const isQuoteRequestUnassigned = (request: Pick<QuoteRequest, 'assignedTo'>) =>
	unassignedOwnerLabels.has(request.assignedTo.trim().toLowerCase());

export const getQuoteRequestRiskScore = (request: Pick<QuoteRequest, 'priority' | 'status' | 'assignedTo'>) => {
	let score = 0;
	if (isQuoteRequestUnassigned(request)) score += 100;
	if (request.status === 'new') score += 40;
	if (request.priority === 'emergency') score += 30;
	if (request.priority === 'priority') score += 15;
	return score;
};

export const createQuoteRequestFromForm = (form: QuoteRequestFormInput): QuoteRequest => {
	const submittedAtUtc = new Date().toISOString();
	const id = form.id ?? crypto.randomUUID();
	const submittedPayload: QuoteRequestSubmittedPayload = {
		companyName: form.companyName,
		contactName: form.contactName,
		email: form.email,
		phone: form.phone,
		siteName: form.siteName,
		serviceAddress: form.serviceAddress,
		serviceType: form.serviceType,
		propertyType: form.propertyType,
		requestedTimeline: form.requestedTimeline,
		priority: form.priority,
		need: form.need,
		attachments: form.attachments
	};

	return {
		id,
		submittedAtUtc,
		companyName: form.companyName,
		contactName: form.contactName,
		customerName: form.contactName,
		email: form.email,
		phone: form.phone,
		siteName: form.siteName,
		serviceAddress: form.serviceAddress,
		serviceType: form.serviceType,
		projectType: form.serviceType,
		propertyType: form.propertyType,
		requestedTimeline: form.requestedTimeline,
		preferredTimeline: form.requestedTimeline,
		priority: form.priority,
		need: form.need,
		message: form.need,
		attachments: form.attachments,
		source: 'public-site',
		status: 'new',
		assignedTo: 'Office intake',
		nextAction: 'Review submission, call customer, and assign inspection owner.',
		intakeSummary: `${form.companyName} · ${form.serviceType} · ${form.requestedTimeline}`,
		submittedPayload,
		timeline: [
			{
				id: crypto.randomUUID(),
				occurredAtUtc: submittedAtUtc,
				type: 'submitted',
				actor: 'Customer Admin',
				label: 'Quote request submitted',
				payload: submittedPayload
			}
		]
	};
};

export const buildQuoteRequestInbox = (requests: QuoteRequest[]) =>
	[...requests].sort((a, b) => {
		const riskDelta = getQuoteRequestRiskScore(b) - getQuoteRequestRiskScore(a);
		if (riskDelta !== 0) return riskDelta;

		const timeDelta = new Date(b.submittedAtUtc).getTime() - new Date(a.submittedAtUtc).getTime();
		if (timeDelta !== 0) return timeDelta;
		return (statusOrder.get(a.status) ?? 0) - (statusOrder.get(b.status) ?? 0);
	});

export const getQuoteRequestMetrics = (requests: QuoteRequest[]) => ({
	total: requests.length,
	newCount: requests.filter((request) => request.status === 'new').length,
	activeCount: requests.filter((request) => !['won', 'closed'].includes(request.status)).length,
	wonCount: requests.filter((request) => request.status === 'won').length
});

export const getQuoteRequestToneClasses = (tone: 'amber' | 'blue' | 'violet' | 'emerald' | 'slate') => {
	if (tone === 'amber') return 'border-amber-400/30 bg-amber-400/10 text-amber-200';
	if (tone === 'blue') return 'border-sky-400/30 bg-sky-400/10 text-sky-200';
	if (tone === 'violet') return 'border-violet-400/30 bg-violet-400/10 text-violet-200';
	if (tone === 'emerald') return 'border-emerald-400/30 bg-emerald-400/10 text-emerald-200';
	return 'border-slate-400/20 bg-slate-400/10 text-slate-200';
};
