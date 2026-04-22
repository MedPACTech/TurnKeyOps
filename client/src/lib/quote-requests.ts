export type QuoteRequestPriority = 'standard' | 'priority' | 'emergency';
export type QuoteRequestStatus =
	| 'new'
	| 'contacted'
	| 'inspection-scheduled'
	| 'estimate-drafted'
	| 'estimate-sent'
	| 'won'
	| 'closed';

export type QuoteRequest = {
	id: string;
	submittedAtUtc: string;
	customerName: string;
	email: string;
	phone: string;
	serviceAddress: string;
	projectType: string;
	propertyType: string;
	preferredTimeline: string;
	priority: QuoteRequestPriority;
	message: string;
	source: 'public-site' | 'office' | 'referral';
	status: QuoteRequestStatus;
	assignedTo: string;
	nextAction: string;
	intakeSummary: string;
};

export type QuoteRequestFormInput = {
	customerName: string;
	email: string;
	phone: string;
	serviceAddress: string;
	projectType: string;
	propertyType: string;
	preferredTimeline: string;
	priority: QuoteRequestPriority;
	message: string;
};

export const quoteRequestStatuses: QuoteRequestStatus[] = [
	'new',
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
		customerName: 'Melissa Carter',
		email: 'melissa@lakeviewplace.com',
		phone: '(704) 555-0113',
		serviceAddress: '421 Lakeview Place, Charlotte, NC',
		projectType: 'Storm damage roof replacement',
		propertyType: 'Residential',
		preferredTimeline: 'ASAP this week',
		priority: 'emergency',
		message: 'We had a leak after the last storm and need someone to inspect the roof and interior stain damage.',
		source: 'public-site',
		status: 'new',
		assignedTo: 'Office intake',
		nextAction: 'Call within 15 minutes and offer same-day inspection slot.',
		intakeSummary: 'Leak + storm damage + homeowner needs immediate response.'
	},
	{
		id: 'qr-elmwood-hoa',
		submittedAtUtc: '2026-03-30T11:10:00.000Z',
		customerName: 'Sandra Holt',
		email: 'sholt@elmwoodhoa.org',
		phone: '(704) 555-0141',
		serviceAddress: '900 Elmwood Commons, Huntersville, NC',
		projectType: 'Multi-building inspection and quote',
		propertyType: 'HOA / multi-family',
		preferredTimeline: 'Need proposal before next board meeting',
		priority: 'priority',
		message: 'Our board needs a quote for repairs across three buildings and wants phased pricing options.',
		source: 'referral',
		status: 'contacted',
		assignedTo: 'Ella - office admin',
		nextAction: 'Send board-ready scope checklist before tomorrow noon.',
		intakeSummary: 'HOA board timing matters more than speed; quote needs phased options.'
	},
	{
		id: 'qr-riverside-retail',
		submittedAtUtc: '2026-03-29T16:40:00.000Z',
		customerName: 'Marcus Wynn',
		email: 'mwynn@riversideretail.com',
		phone: '(704) 555-0187',
		serviceAddress: '1130 Riverside Commerce Dr, Gastonia, NC',
		projectType: 'Commercial flat roof repair',
		propertyType: 'Commercial',
		preferredTimeline: 'After-hours site walk next week',
		priority: 'standard',
		message: 'Need repair pricing for recurring ponding and flashing issues behind two tenant units.',
		source: 'office',
		status: 'inspection-scheduled',
		assignedTo: 'Estimator queue',
		nextAction: 'Confirm ladder access and after-hours contact before Tuesday.',
		intakeSummary: 'Commercial repair request already qualified and ready for site walk.'
	}
];

const statusOrder = new Map(quoteRequestStatuses.map((status, index) => [status, index]));

export const createQuoteRequestFromForm = (form: QuoteRequestFormInput): QuoteRequest => {
	const submittedAtUtc = new Date().toISOString();
	const id = crypto.randomUUID();

	return {
		id,
		submittedAtUtc,
		customerName: form.customerName,
		email: form.email,
		phone: form.phone,
		serviceAddress: form.serviceAddress,
		projectType: form.projectType,
		propertyType: form.propertyType,
		preferredTimeline: form.preferredTimeline,
		priority: form.priority,
		message: form.message,
		source: 'public-site',
		status: 'new',
		assignedTo: 'Office intake',
		nextAction: 'Review submission, call customer, and assign inspection owner.',
		intakeSummary: `${form.projectType} · ${form.propertyType} · ${form.preferredTimeline}`
	};
};

export const buildQuoteRequestInbox = (requests: QuoteRequest[]) =>
	[...requests].sort((a, b) => {
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
