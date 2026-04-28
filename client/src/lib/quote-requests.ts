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

export type QuoteRequestSiteVisitSchedule = {
	visitDate: string;
	windowStart: string;
	windowEnd: string;
	siteContact: string;
	siteContactPhone: string;
	assignedFieldResource: string;
	notes?: string;
	scheduledAtUtc: string;
	scheduledBy: string;
};

export type QuoteRequestQualificationCheckKey =
	| 'service-fit'
	| 'site-readiness'
	| 'required-attachments'
	| 'contact-readiness'
	| 'scheduling-readiness';

export type QuoteRequestMissingInfoReasonCode =
	| 'service-fit-unconfirmed'
	| 'site-readiness-unconfirmed'
	| 'required-attachments-missing'
	| 'contact-readiness-missing'
	| 'scheduling-readiness-missing';

export type QuoteRequestQualificationReview = {
	missingInfoReasonCodes: QuoteRequestMissingInfoReasonCode[];
	reviewedAtUtc?: string;
	reviewedBy?: string;
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
	type: 'submitted' | 'operator-updated' | 'site-visit-scheduled';
	actor: string;
	label: string;
	payload?: QuoteRequestSubmittedPayload;
	note?: string;
	siteVisitSchedule?: QuoteRequestSiteVisitSchedule;
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
	qualification: QuoteRequestQualificationReview;
	submittedPayload: QuoteRequestSubmittedPayload;
	timeline: QuoteRequestTimelineEvent[];
	siteVisitSchedule?: QuoteRequestSiteVisitSchedule | null;
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
		label: 'Site Visit Scheduled',
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

export const quoteRequestMissingInfoReasonCodes: QuoteRequestMissingInfoReasonCode[] = [
	'service-fit-unconfirmed',
	'site-readiness-unconfirmed',
	'required-attachments-missing',
	'contact-readiness-missing',
	'scheduling-readiness-missing'
];

export const quoteRequestMissingInfoReasonMeta: Record<
	QuoteRequestMissingInfoReasonCode,
	{ label: string; detail: string; checkKey: QuoteRequestQualificationCheckKey }
> = {
	'service-fit-unconfirmed': {
		label: 'Service fit not confirmed',
		detail: 'Scope, trade fit, or project type needs office review before BDR accepts it.',
		checkKey: 'service-fit'
	},
	'site-readiness-unconfirmed': {
		label: 'Site readiness missing',
		detail: 'Site name, address, access, or readiness details are incomplete.',
		checkKey: 'site-readiness'
	},
	'required-attachments-missing': {
		label: 'Required attachments missing',
		detail: 'Photos, scope files, board material, or damage documentation still needs to be attached.',
		checkKey: 'required-attachments'
	},
	'contact-readiness-missing': {
		label: 'Contact readiness missing',
		detail: 'A reachable contact name, email, and phone are required for follow-up.',
		checkKey: 'contact-readiness'
	},
	'scheduling-readiness-missing': {
		label: 'Scheduling readiness missing',
		detail: 'Requested timing or availability is not clear enough to book a site visit.',
		checkKey: 'scheduling-readiness'
	}
};

export const quoteRequestMissingInfoReasonOptions = quoteRequestMissingInfoReasonCodes.map((code) => ({
	value: code,
	...quoteRequestMissingInfoReasonMeta[code]
}));

export const isQuoteRequestMissingInfoReasonCode = (value: string): value is QuoteRequestMissingInfoReasonCode =>
	quoteRequestMissingInfoReasonCodes.includes(value as QuoteRequestMissingInfoReasonCode);

export const normalizeQuoteRequestSiteVisitSchedule = (value: unknown): QuoteRequestSiteVisitSchedule | null => {
	if (!value || typeof value !== 'object') {
		return null;
	}

	const schedule = value as Partial<QuoteRequestSiteVisitSchedule>;
	const visitDate = String(schedule.visitDate ?? '').trim();
	const windowStart = String(schedule.windowStart ?? '').trim();
	const windowEnd = String(schedule.windowEnd ?? '').trim();
	const siteContact = String(schedule.siteContact ?? '').trim();
	const siteContactPhone = String(schedule.siteContactPhone ?? '').trim();
	const assignedFieldResource = String(schedule.assignedFieldResource ?? '').trim();
	const scheduledAtUtc = String(schedule.scheduledAtUtc ?? '').trim();
	const scheduledBy = String(schedule.scheduledBy ?? '').trim();
	const notes = String(schedule.notes ?? '').trim();

	if (!visitDate || !windowStart || !windowEnd || !siteContact || !assignedFieldResource || !scheduledAtUtc || !scheduledBy) {
		return null;
	}

	return {
		visitDate,
		windowStart,
		windowEnd,
		siteContact,
		siteContactPhone,
		assignedFieldResource,
		notes: notes || undefined,
		scheduledAtUtc,
		scheduledBy
	};
};

export const normalizeQuoteRequestQualification = (value: unknown): QuoteRequestQualificationReview => {
	if (!value || typeof value !== 'object') {
		return { missingInfoReasonCodes: [] };
	}

	const qualification = value as Partial<QuoteRequestQualificationReview>;
	const missingInfoReasonCodes = Array.isArray(qualification.missingInfoReasonCodes)
		? qualification.missingInfoReasonCodes.filter((code): code is QuoteRequestMissingInfoReasonCode =>
				typeof code === 'string' && isQuoteRequestMissingInfoReasonCode(code)
			)
		: [];

	return {
		missingInfoReasonCodes: [...new Set(missingInfoReasonCodes)],
		reviewedAtUtc: qualification.reviewedAtUtc,
		reviewedBy: qualification.reviewedBy
	};
};

export type QuoteRequestQualificationCheck = {
	key: QuoteRequestQualificationCheckKey;
	label: string;
	detail: string;
	complete: boolean;
	missingInfoReasonCode: QuoteRequestMissingInfoReasonCode;
};

export type QuoteRequestQualificationSummary = {
	checks: QuoteRequestQualificationCheck[];
	suggestedMissingInfoReasonCodes: QuoteRequestMissingInfoReasonCode[];
	missingInfoReasonCodes: QuoteRequestMissingInfoReasonCode[];
	blockerLabels: string[];
	isQualified: boolean;
	scheduleEligible: boolean;
};

const hasText = (value: string | null | undefined) => Boolean(value?.trim());
const hasEmail = (value: string | null | undefined) => Boolean(value?.includes('@') && value.includes('.'));
const hasPhone = (value: string | null | undefined) => (value?.replace(/\D/g, '').length ?? 0) >= 7;
const hasDetailedScope = (value: string | null | undefined) => (value?.trim().length ?? 0) >= 18;

const hasUsableAddress = (value: string | null | undefined) => {
	const parts = String(value ?? '')
		.split(',')
		.map((part) => part.trim())
		.filter(Boolean);
	return parts.length >= 2 && parts.join('').length >= 12;
};

const needsAttachmentReview = (request: Pick<QuoteRequest, 'serviceType' | 'projectType' | 'propertyType' | 'need' | 'message' | 'priority'>) => {
	const text = [
		request.serviceType,
		request.projectType,
		request.propertyType,
		request.need,
		request.message,
		request.priority
	]
		.join(' ')
		.toLowerCase();
	return /storm|leak|damage|insurance|hoa|board|multi-building|multi-family|photo|attachment|attached/.test(text);
};

export const buildQuoteRequestQualification = (request: QuoteRequest): QuoteRequestQualificationSummary => {
	const attachmentReviewRequired = needsAttachmentReview(request);
	const checks: QuoteRequestQualificationCheck[] = [
		{
			key: 'service-fit',
			label: 'Service fit',
			detail: 'Service type and scope describe work BDR can evaluate.',
			complete: hasText(request.serviceType) && hasDetailedScope(request.need || request.message),
			missingInfoReasonCode: 'service-fit-unconfirmed'
		},
		{
			key: 'site-readiness',
			label: 'Site readiness',
			detail: 'Site name and service address are clear enough for field planning.',
			complete: hasText(request.siteName) && hasUsableAddress(request.serviceAddress),
			missingInfoReasonCode: 'site-readiness-unconfirmed'
		},
		{
			key: 'required-attachments',
			label: 'Required attachments',
			detail: attachmentReviewRequired
				? 'This request type needs photos or supporting files before qualification.'
				: 'No required attachment blocker detected for this request.',
			complete: !attachmentReviewRequired || request.attachments.length > 0,
			missingInfoReasonCode: 'required-attachments-missing'
		},
		{
			key: 'contact-readiness',
			label: 'Contact readiness',
			detail: 'Contact name, email, and phone are ready for office follow-up.',
			complete: hasText(request.contactName) && hasEmail(request.email) && hasPhone(request.phone),
			missingInfoReasonCode: 'contact-readiness-missing'
		},
		{
			key: 'scheduling-readiness',
			label: 'Scheduling readiness',
			detail: 'Requested timing is specific enough to move into site visit scheduling.',
			complete: hasText(request.requestedTimeline) && !/^needs review$/i.test(request.requestedTimeline.trim()),
			missingInfoReasonCode: 'scheduling-readiness-missing'
		}
	];
	const suggestedMissingInfoReasonCodes = checks
		.filter((check) => !check.complete)
		.map((check) => check.missingInfoReasonCode);
	const reviewedMissingInfoReasonCodes = normalizeQuoteRequestQualification(request.qualification).missingInfoReasonCodes;
	const missingInfoReasonCodes = [...new Set([...suggestedMissingInfoReasonCodes, ...reviewedMissingInfoReasonCodes])];
	const blockerLabels = missingInfoReasonCodes.map((code) => quoteRequestMissingInfoReasonMeta[code].label);
	const isQualified = checks.every((check) => check.complete) && reviewedMissingInfoReasonCodes.length === 0;

	return {
		checks,
		suggestedMissingInfoReasonCodes,
		missingInfoReasonCodes,
		blockerLabels,
		isQualified,
		scheduleEligible: request.status === 'qualified' && isQualified
	};
};

export const isQuoteRequestReadyForScheduling = (request: QuoteRequest) =>
	buildQuoteRequestQualification(request).scheduleEligible;

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
		qualification: {
			missingInfoReasonCodes: []
		},
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
		qualification: {
			missingInfoReasonCodes: []
		},
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
		id: 'qr-maple-garage',
		submittedAtUtc: '2026-03-29T14:15:00.000Z',
		companyName: 'Maple Street Properties',
		contactName: 'Ariana Brooks',
		customerName: 'Ariana Brooks',
		email: 'ariana@maplestreetproperties.com',
		phone: '(704) 555-0168',
		siteName: 'Maple detached garage',
		serviceAddress: '148 Maple Street, Concord, NC',
		serviceType: 'Detached garage slab replacement',
		projectType: 'Detached garage slab replacement',
		propertyType: 'Residential',
		requestedTimeline: 'Next Tuesday morning',
		preferredTimeline: 'Next Tuesday morning',
		priority: 'standard',
		need: 'Need a quote for replacing a cracked detached garage slab and apron before listing the house.',
		message: 'Need a quote for replacing a cracked detached garage slab and apron before listing the house.',
		attachments: [],
		source: 'office',
		status: 'qualified',
		assignedTo: 'Ella - office admin',
		nextAction: 'Book a site visit and line up the estimator handoff.',
		intakeSummary: 'Qualified residential slab request ready for site-visit booking.',
		qualification: {
			missingInfoReasonCodes: []
		},
		submittedPayload: {
			companyName: 'Maple Street Properties',
			contactName: 'Ariana Brooks',
			email: 'ariana@maplestreetproperties.com',
			phone: '(704) 555-0168',
			siteName: 'Maple detached garage',
			serviceAddress: '148 Maple Street, Concord, NC',
			serviceType: 'Detached garage slab replacement',
			propertyType: 'Residential',
			requestedTimeline: 'Next Tuesday morning',
			priority: 'standard',
			need: 'Need a quote for replacing a cracked detached garage slab and apron before listing the house.',
			attachments: []
		},
		timeline: [
			{
				id: 'qr-maple-garage-submitted',
				occurredAtUtc: '2026-03-29T14:15:00.000Z',
				type: 'submitted',
				actor: 'Customer Admin',
				label: 'Quote request submitted',
				payload: {
					companyName: 'Maple Street Properties',
					contactName: 'Ariana Brooks',
					email: 'ariana@maplestreetproperties.com',
					phone: '(704) 555-0168',
					siteName: 'Maple detached garage',
					serviceAddress: '148 Maple Street, Concord, NC',
					serviceType: 'Detached garage slab replacement',
					propertyType: 'Residential',
					requestedTimeline: 'Next Tuesday morning',
					priority: 'standard',
					need: 'Need a quote for replacing a cracked detached garage slab and apron before listing the house.',
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
		qualification: {
			missingInfoReasonCodes: []
		},
		siteVisitSchedule: {
			visitDate: '2026-04-02',
			windowStart: '18:00',
			windowEnd: '19:30',
			siteContact: 'Marcus Wynn',
			siteContactPhone: '(704) 555-0187',
			assignedFieldResource: 'Estimator - Maya',
			notes: 'Capture roof access details and after-hours entry protocol before leaving site.',
			scheduledAtUtc: '2026-03-30T14:10:00.000Z',
			scheduledBy: 'External Admin'
		},
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
			},
			{
				id: 'qr-riverside-retail-site-visit',
				occurredAtUtc: '2026-03-30T14:10:00.000Z',
				type: 'site-visit-scheduled',
				actor: 'External Admin',
				label: 'Site visit scheduled · Apr 2 · 6:00 PM – 7:30 PM',
				note: 'Estimator - Maya confirmed for the after-hours roof walk.',
				siteVisitSchedule: {
					visitDate: '2026-04-02',
					windowStart: '18:00',
					windowEnd: '19:30',
					siteContact: 'Marcus Wynn',
					siteContactPhone: '(704) 555-0187',
					assignedFieldResource: 'Estimator - Maya',
					notes: 'Capture roof access details and after-hours entry protocol before leaving site.',
					scheduledAtUtc: '2026-03-30T14:10:00.000Z',
					scheduledBy: 'External Admin'
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
		qualification: {
			missingInfoReasonCodes: []
		},
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
		],
		siteVisitSchedule: null
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
