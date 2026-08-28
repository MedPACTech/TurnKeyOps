import { env } from '$env/dynamic/private';
import { error } from '@sveltejs/kit';
import {
	buildQuoteRequestQualification,
	buildQuoteRequestInbox,
	createQuoteRequestFromForm,
	isQuoteRequestSiteVisitCancellationReasonCode,
	normalizeQuoteRequestQualification,
	normalizeQuoteRequestSiteVisitSchedule,
	quoteRequestMissingInfoReasonMeta,
	quoteRequestSiteVisitCancellationReasonMeta,
	quoteRequestStatusMeta,
	type QuoteRequest,
	type QuoteRequestAttachment,
	type QuoteRequestFormInput,
	type QuoteRequestMissingInfoReasonCode,
	type QuoteRequestPriority,
	type QuoteRequestQualificationReview,
	type QuoteRequestSiteVisitCancellationReasonCode,
	type QuoteRequestSiteVisitSchedule,
	type QuoteRequestStatus,
	type QuoteRequestSubmittedPayload,
	type QuoteRequestTimelineEvent
} from '$lib/quote-requests';
import { bdrTenant, getTenantById } from '$lib/config/tenants';
import {
	getTurnKeyApiBaseUrl as getApiBaseUrl,
	getTurnKeyApiHeaders as getApiHeaders,
	unwrapTurnKeyApiEnvelope as unwrapEnvelope
} from '$lib/server/turnkey-api';

const quoteMarker = 'TKO_BDR_QUOTE_REQUEST::';
const internalAdminActor = 'Internal Admin';
const officeQueueOwner = 'Office intake';

type QuoteRequestDto = {
	id: string;
	tenantId: string;
	submittedAtUtc: string;
	companyName?: string | null;
	contactName?: string | null;
	customerName: string;
	email: string;
	phone: string;
	siteName?: string | null;
	serviceAddress: string;
	serviceType?: string | null;
	projectType: string;
	propertyType: string;
	requestedTimeline?: string | null;
	preferredTimeline: string;
	priority: QuoteRequestPriority;
	need?: string | null;
	message: string;
	attachments?: QuoteRequestAttachment[] | null;
	source: QuoteRequest['source'];
	status: QuoteRequestStatus;
	assignedTo: string;
	nextAction: string;
	intakeSummary: string;
	qualification?: QuoteRequestQualificationReview | null;
	submittedPayload?: QuoteRequestSubmittedPayload | null;
	timeline?: QuoteRequestTimelineEvent[] | null;
	siteVisitSchedule?: QuoteRequestSiteVisitSchedule | null;
	updatedAtUtc?: string | null;
};

type LegacyLeadDto = {
	id: string;
	tenantId: string;
	leadNumber: string;
	companyName: string;
	contactName: string;
	contactEmail?: string | null;
	contactPhone?: string | null;
	pipelineStage: string;
	estimatedValue?: number | null;
	source: string;
	projectType?: string | null;
	serviceAreaCity?: string | null;
	serviceAreaState?: string | null;
	scopeSummary?: string | null;
	requestedStartUtc?: string | null;
	nextFollowUpUtc?: string | null;
	assignedEstimator?: string | null;
};

type QuoteLeadMetadata = {
	submittedAtUtc: string;
	companyName?: string;
	contactName?: string;
	siteName?: string;
	serviceAddress: string;
	serviceType?: string;
	propertyType: string;
	requestedTimeline?: string;
	preferredTimeline: string;
	priority: QuoteRequestPriority;
	need?: string;
	message: string;
	attachments?: QuoteRequestAttachment[];
	status: QuoteRequestStatus;
	nextAction: string;
	intakeSummary: string;
	source: QuoteRequest['source'];
	qualification?: QuoteRequestQualificationReview;
	submittedPayload?: QuoteRequestSubmittedPayload;
	timeline?: QuoteRequestTimelineEvent[];
	siteVisitSchedule?: QuoteRequestSiteVisitSchedule | null;
};

const statusToPipelineStage: Record<QuoteRequestStatus, string> = {
	new: 'New',
	'in-review': 'In Review',
	'needs-info': 'Needs Info',
	qualified: 'Qualified',
	contacted: 'Contacted',
	'inspection-scheduled': 'Site Visit Scheduled',
	'estimate-drafted': 'Estimate Drafted',
	'estimate-sent': 'Quoted',
	won: 'Won',
	closed: 'Closed'
};

const pipelineStageToStatus = (value: string | null | undefined): QuoteRequestStatus => {
	const normalized = String(value ?? '').trim().toLowerCase();
	if (normalized === 'in review') return 'in-review';
	if (normalized === 'needs info') return 'needs-info';
	if (normalized === 'qualified') return 'qualified';
	if (normalized === 'contacted') return 'contacted';
	if (normalized === 'inspection scheduled' || normalized === 'site visit scheduled') return 'inspection-scheduled';
	if (normalized === 'estimate drafted') return 'estimate-drafted';
	if (normalized === 'quoted' || normalized === 'estimate sent') return 'estimate-sent';
	if (normalized === 'won') return 'won';
	if (normalized === 'closed') return 'closed';
	return 'new';
};

const normalizeSource = (value: string | null | undefined): QuoteRequest['source'] => {
	const normalized = String(value ?? '').trim().toLowerCase();
	if (normalized.includes('referral')) return 'referral';
	if (normalized.includes('office') || normalized.includes('manual')) return 'office';
	return 'public-site';
};

const parseLeadMetadata = (scopeSummary: string | null | undefined): QuoteLeadMetadata | null => {
	if (!scopeSummary?.startsWith(quoteMarker)) return null;
	try {
		return JSON.parse(scopeSummary.slice(quoteMarker.length)) as QuoteLeadMetadata;
	} catch {
		return null;
	}
};

const serializeLeadMetadata = (metadata: QuoteLeadMetadata) => `${quoteMarker}${JSON.stringify(metadata)}`;

export const getQuoteRequestTenantId = () => env.TKO_API_TENANT_ID ?? bdrTenant.id;

const normalizeAttachments = (value: unknown): QuoteRequestAttachment[] => {
	if (!Array.isArray(value)) return [];
	return value
		.filter((item): item is QuoteRequestAttachment => Boolean(item && typeof item === 'object' && 'fileName' in item))
		.map((item) => ({
			id: item.id || crypto.randomUUID(),
			fileName: item.fileName,
			contentType: item.contentType || 'application/octet-stream',
			sizeBytes: Number(item.sizeBytes) || 0,
			uploadedAtUtc: item.uploadedAtUtc || new Date().toISOString(),
			tenantId: item.tenantId,
			blobContainer: item.blobContainer,
			blobName: item.blobName,
			blobUrl: item.blobUrl
		}));
};

const buildSubmittedPayload = (request: QuoteRequest): QuoteRequestSubmittedPayload => ({
	companyName: request.companyName || request.customerName,
	contactName: request.contactName || request.customerName,
	email: request.email,
	phone: request.phone,
	siteName: request.siteName || request.serviceAddress,
	serviceAddress: request.serviceAddress,
	serviceType: request.serviceType || request.projectType,
	propertyType: request.propertyType,
	requestedTimeline: request.requestedTimeline || request.preferredTimeline,
	priority: request.priority,
	need: request.need || request.message,
	attachments: normalizeAttachments(request.attachments)
});

const isSubmittedPayload = (value: unknown): value is QuoteRequestSubmittedPayload =>
	Boolean(value && typeof value === 'object' && 'companyName' in value && 'need' in value);

const normalizeQuoteRequest = (request: QuoteRequest): QuoteRequest => {
	const attachments = normalizeAttachments(request.attachments);
	const normalized: QuoteRequest = {
		...request,
		companyName: request.companyName || request.customerName,
		contactName: request.contactName || request.customerName,
		customerName: request.customerName || request.contactName,
		siteName: request.siteName || request.serviceAddress,
		serviceType: request.serviceType || request.projectType,
		projectType: request.projectType || request.serviceType,
		requestedTimeline: request.requestedTimeline || request.preferredTimeline,
		preferredTimeline: request.preferredTimeline || request.requestedTimeline,
		need: request.need || request.message,
		message: request.message || request.need,
		attachments,
		submittedPayload: request.submittedPayload ?? ({} as QuoteRequestSubmittedPayload),
		qualification: normalizeQuoteRequestQualification(request.qualification),
		timeline: Array.isArray(request.timeline) ? request.timeline : [],
		siteVisitSchedule: normalizeQuoteRequestSiteVisitSchedule(request.siteVisitSchedule)
	};

	const submittedPayload = isSubmittedPayload(request.submittedPayload)
		? {
				...request.submittedPayload,
				attachments: normalizeAttachments(request.submittedPayload.attachments)
			}
		: buildSubmittedPayload(normalized);
	const timeline = normalized.timeline.length
		? [...normalized.timeline]
		: [
				{
					id: crypto.randomUUID(),
					occurredAtUtc: normalized.submittedAtUtc,
					type: 'submitted' as const,
					actor: 'Customer Admin',
					label: 'Quote request submitted',
					payload: submittedPayload
				}
			];

	if (
		normalized.siteVisitSchedule &&
		!timeline.some(
			(event) =>
				event.siteVisitSchedule?.scheduledAtUtc === normalized.siteVisitSchedule?.scheduledAtUtc &&
				event.siteVisitSchedule?.visitDate === normalized.siteVisitSchedule?.visitDate
		)
	) {
		const visitDateLabel = formatSiteVisitDateLabel(normalized.siteVisitSchedule.visitDate);
		const visitWindowLabel = buildSiteVisitWindowLabel(normalized.siteVisitSchedule);
		timeline.push(
			createActivityEvent({
				occurredAtUtc: normalized.siteVisitSchedule.scheduledAtUtc,
				label: `Site visit scheduled · ${visitDateLabel} · ${visitWindowLabel}`,
				note: normalized.siteVisitSchedule.notes,
				siteVisitSchedule: normalized.siteVisitSchedule
			})
		);
	}

	return {
		...normalized,
		submittedPayload,
		timeline: timeline.sort((left, right) => new Date(left.occurredAtUtc).getTime() - new Date(right.occurredAtUtc).getTime())
	};
};

const formatSiteVisitDateLabel = (value: string) =>
	new Date(`${value}T12:00:00`).toLocaleDateString('en-US', {
		month: 'short',
		day: 'numeric'
	});

const formatSiteVisitTimeLabel = (value: string) => {
	const [hoursText = '0', minutesText = '0'] = value.split(':');
	const hours = Number(hoursText);
	const minutes = Number(minutesText);
	if (Number.isNaN(hours) || Number.isNaN(minutes)) return value;
	return new Date(2026, 0, 1, hours, minutes).toLocaleTimeString('en-US', {
		hour: 'numeric',
		minute: '2-digit'
	});
};

const buildSiteVisitWindowLabel = (schedule: Pick<QuoteRequestSiteVisitSchedule, 'windowStart' | 'windowEnd'>) =>
	`${formatSiteVisitTimeLabel(schedule.windowStart)} – ${formatSiteVisitTimeLabel(schedule.windowEnd)}`;

const createActivityEvent = ({
	occurredAtUtc,
	label,
	note,
	siteVisitSchedule,
	type,
	actor
}: {
	occurredAtUtc: string;
	label: string;
	note?: string;
	siteVisitSchedule?: QuoteRequestSiteVisitSchedule;
	type?: QuoteRequestTimelineEvent['type'];
	actor?: string;
}): QuoteRequestTimelineEvent => ({
	id: crypto.randomUUID(),
	occurredAtUtc,
	type: type ?? (siteVisitSchedule ? 'site-visit-scheduled' : 'operator-updated'),
	actor: actor ?? internalAdminActor,
	label,
	note,
	siteVisitSchedule
});

const formatMissingInfoReasonSummary = (codes: QuoteRequestMissingInfoReasonCode[]) =>
	codes.map((code) => quoteRequestMissingInfoReasonMeta[code].label).join(' · ');

const isQuoteRequestDto = (value: unknown): value is QuoteRequestDto => {
	if (!value || typeof value !== 'object') return false;
	return 'submittedAtUtc' in value && 'serviceAddress' in value && 'preferredTimeline' in value;
};

const toQuoteRequest = (record: QuoteRequestDto | LegacyLeadDto): QuoteRequest | null => {
	if (isQuoteRequestDto(record)) {
		return normalizeQuoteRequest({
			id: record.id,
			tenantId: record.tenantId,
			submittedAtUtc: record.submittedAtUtc,
			companyName: record.companyName ?? record.customerName,
			contactName: record.contactName ?? record.customerName,
			customerName: record.customerName || record.contactName || record.companyName || 'Unknown contact',
			email: record.email,
			phone: record.phone,
			siteName: record.siteName ?? record.serviceAddress,
			serviceAddress: record.serviceAddress,
			serviceType: record.serviceType ?? record.projectType,
			projectType: record.projectType || record.serviceType || 'Quote request',
			propertyType: record.propertyType,
			requestedTimeline: record.requestedTimeline ?? record.preferredTimeline,
			preferredTimeline: record.preferredTimeline || record.requestedTimeline || 'Needs review',
			priority: record.priority,
			need: record.need ?? record.message,
			message: record.message || record.need || '',
			attachments: record.attachments ?? [],
			source: record.source,
			status: record.status,
			assignedTo: record.assignedTo,
			nextAction: record.nextAction,
			intakeSummary: record.intakeSummary,
			qualification: normalizeQuoteRequestQualification(record.qualification),
			submittedPayload: record.submittedPayload ?? ({} as QuoteRequestSubmittedPayload),
			timeline: record.timeline ?? [],
			siteVisitSchedule: normalizeQuoteRequestSiteVisitSchedule(record.siteVisitSchedule)
		});
	}

	const metadata = parseLeadMetadata(record.scopeSummary);
	if (!metadata) return null;

	return normalizeQuoteRequest({
		id: record.id,
		tenantId: record.tenantId,
		submittedAtUtc: metadata.submittedAtUtc,
		companyName: metadata.companyName ?? record.companyName,
		contactName: metadata.contactName ?? record.contactName ?? record.companyName,
		customerName: record.contactName || metadata.contactName || record.companyName,
		email: record.contactEmail ?? '',
		phone: record.contactPhone ?? '',
		siteName: metadata.siteName ?? metadata.serviceAddress,
		serviceAddress: metadata.serviceAddress,
		serviceType: metadata.serviceType ?? record.projectType ?? 'Quote request',
		projectType: record.projectType ?? metadata.serviceType ?? 'Quote request',
		propertyType: metadata.propertyType,
		requestedTimeline: metadata.requestedTimeline ?? metadata.preferredTimeline,
		preferredTimeline: metadata.preferredTimeline,
		priority: metadata.priority,
		need: metadata.need ?? metadata.message,
		message: metadata.message,
		attachments: metadata.attachments ?? [],
		source: metadata.source ?? normalizeSource(record.source),
		status: metadata.status ?? pipelineStageToStatus(record.pipelineStage),
		assignedTo: record.assignedEstimator?.trim() || 'Office intake',
		nextAction: metadata.nextAction,
		intakeSummary: metadata.intakeSummary,
		qualification: normalizeQuoteRequestQualification(metadata.qualification),
		submittedPayload: metadata.submittedPayload ?? ({} as QuoteRequestSubmittedPayload),
		timeline: metadata.timeline ?? [],
		siteVisitSchedule: normalizeQuoteRequestSiteVisitSchedule(metadata.siteVisitSchedule)
	});
};

const parseCityState = (serviceAddress: string) => {
	const segments = serviceAddress.split(',').map((segment) => segment.trim()).filter(Boolean);
	if (segments.length >= 2) {
		const stateSegment = segments.at(-1) ?? '';
		const state = stateSegment.split(/\s+/)[0] ?? '';
		return {
			city: segments.at(-2) ?? '',
			state
		};
	}

	return { city: '', state: '' };
};

const toQuoteRequestDto = (request: QuoteRequest, existing?: QuoteRequestDto): QuoteRequestDto => ({
	id: existing?.id ?? request.id,
	tenantId: existing?.tenantId ?? request.tenantId ?? getQuoteRequestTenantId(),
	submittedAtUtc: existing?.submittedAtUtc ?? request.submittedAtUtc,
	companyName: request.companyName,
	contactName: request.contactName,
	customerName: request.customerName,
	email: request.email,
	phone: request.phone,
	siteName: request.siteName,
	serviceAddress: request.serviceAddress,
	serviceType: request.serviceType,
	projectType: request.projectType,
	propertyType: request.propertyType,
	requestedTimeline: request.requestedTimeline,
	preferredTimeline: request.preferredTimeline,
	priority: request.priority,
	need: request.need,
	message: request.message,
	attachments: request.attachments,
	source: request.source,
	status: request.status,
	assignedTo: request.assignedTo,
	nextAction: request.nextAction,
	intakeSummary: request.intakeSummary,
	qualification: request.qualification,
	submittedPayload: request.submittedPayload,
	timeline: request.timeline,
	siteVisitSchedule: request.siteVisitSchedule,
	updatedAtUtc: existing?.updatedAtUtc ?? null
});

const toLegacyLeadDto = (request: QuoteRequest, existing?: LegacyLeadDto): LegacyLeadDto => {
	const { city, state } = parseCityState(request.serviceAddress);
	const metadata: QuoteLeadMetadata = {
		submittedAtUtc: request.submittedAtUtc,
		companyName: request.companyName,
		contactName: request.contactName,
		siteName: request.siteName,
		serviceAddress: request.serviceAddress,
		serviceType: request.serviceType,
		propertyType: request.propertyType,
		requestedTimeline: request.requestedTimeline,
		preferredTimeline: request.preferredTimeline,
		priority: request.priority,
		need: request.need,
		message: request.message,
		attachments: request.attachments,
		status: request.status,
		nextAction: request.nextAction,
		intakeSummary: request.intakeSummary,
		source: request.source,
		qualification: request.qualification,
		submittedPayload: request.submittedPayload,
		timeline: request.timeline,
		siteVisitSchedule: request.siteVisitSchedule
	};

	return {
		id: existing?.id ?? request.id,
		tenantId: existing?.tenantId ?? request.tenantId ?? getQuoteRequestTenantId(),
		leadNumber: existing?.leadNumber ?? `BDR-LEAD-${Date.now()}`,
		companyName: existing?.companyName?.trim() || request.companyName,
		contactName: request.contactName,
		contactEmail: request.email,
		contactPhone: request.phone,
		pipelineStage: statusToPipelineStage[request.status],
		estimatedValue: existing?.estimatedValue ?? null,
		source:
			request.source === 'referral'
				? 'Referral'
				: request.source === 'office'
					? 'Office Intake'
					: 'Website Quote Request',
		projectType: request.projectType,
		serviceAreaCity: city || existing?.serviceAreaCity || null,
		serviceAreaState: state || existing?.serviceAreaState || null,
		scopeSummary: serializeLeadMetadata(metadata),
		requestedStartUtc: existing?.requestedStartUtc ?? request.submittedAtUtc,
		nextFollowUpUtc: existing?.nextFollowUpUtc ?? request.submittedAtUtc,
		assignedEstimator: request.assignedTo
	};
};

export const loadQuoteRequests = async (
	fetch: typeof globalThis.fetch,
	tenantId = getQuoteRequestTenantId()
): Promise<{ requests: QuoteRequest[]; source: 'api' | 'fallback' }> => {
	const response = await fetch(`${getApiBaseUrl()}/api/quote-requests`, {
		headers: getApiHeaders()
	});
	const records = (await unwrapEnvelope<Array<QuoteRequestDto | LegacyLeadDto>>(response)).filter(
		(record) => record.tenantId === tenantId
	);
	const requests = records
		.map(toQuoteRequest)
		.filter((request): request is QuoteRequest => Boolean(request));
	return { requests: buildQuoteRequestInbox(requests), source: 'api' };
};

export const submitQuoteRequest = async (fetch: typeof globalThis.fetch, input: QuoteRequestFormInput) => {
	const request = createQuoteRequestFromForm(input);
	const tenantId = input.tenantId ?? getQuoteRequestTenantId();
	const tenantSlug = getTenantById(tenantId)?.slug;
	if (!tenantSlug) {
		throw error(400, 'Quote request tenant is not configured.');
	}

	const response = await fetch(`${getApiBaseUrl()}/api/public/quote-requests/${tenantSlug}`, {
		method: 'POST',
		headers: getApiHeaders(),
		signal: AbortSignal.timeout(15_000),
		body: JSON.stringify({
			id: request.id,
			website: input.website ?? '',
			companyName: request.companyName,
			contactName: request.contactName,
			email: request.email,
			phone: request.phone,
			siteName: request.siteName,
			serviceAddress: request.serviceAddress,
			serviceType: request.serviceType,
			propertyType: request.propertyType,
			requestedTimeline: request.requestedTimeline,
			priority: request.priority,
			need: request.need,
			attachments: request.attachments
		})
	});
	const saved = await unwrapEnvelope<QuoteRequestDto>(response);
	return toQuoteRequest(saved) ?? request;
};

export const updateQuoteRequest = async (
	fetch: typeof globalThis.fetch,
	params: {
		id: string;
		status: QuoteRequestStatus;
		assignedTo: string;
		nextAction: string;
		missingInfoReasonCodes: QuoteRequestMissingInfoReasonCode[];
		contactName?: string;
		email?: string;
		phone?: string;
		siteName?: string;
		serviceAddress?: string;
		requestedTimeline?: string;
	}
) => {
	const buildUpdatedRequest = (existingRequest: QuoteRequest): QuoteRequest => {
		const occurredAtUtc = new Date().toISOString();
		const nextAssignedTo = params.assignedTo || 'Office intake';
		const nextAction = params.nextAction || 'Review request and decide next office step.';
		const selectedMissingInfoReasonCodes = normalizeQuoteRequestQualification({
			missingInfoReasonCodes: params.missingInfoReasonCodes
		}).missingInfoReasonCodes;
		const existingMissingInfoReasonCodes = normalizeQuoteRequestQualification(existingRequest.qualification).missingInfoReasonCodes;
		const draftRequest = {
			...existingRequest,
			status: params.status,
			assignedTo: nextAssignedTo,
			nextAction,
			contactName: params.contactName || existingRequest.contactName,
			customerName: params.contactName || existingRequest.customerName,
			email: params.email || existingRequest.email,
			phone: params.phone || existingRequest.phone,
			siteName: params.siteName || existingRequest.siteName,
			serviceAddress: params.serviceAddress || existingRequest.serviceAddress,
			requestedTimeline: params.requestedTimeline || existingRequest.requestedTimeline,
			preferredTimeline: params.requestedTimeline || existingRequest.preferredTimeline
		};
		const suggestedMissingInfoReasonCodes = buildQuoteRequestQualification({
			...draftRequest,
			qualification: { missingInfoReasonCodes: [] }
		}).suggestedMissingInfoReasonCodes;
		const missingInfoReasonCodes =
			params.status === 'needs-info'
				? selectedMissingInfoReasonCodes.length
					? selectedMissingInfoReasonCodes
					: suggestedMissingInfoReasonCodes
				: [];
		const activityEvents: QuoteRequestTimelineEvent[] = [];
		const previousStatusLabel = quoteRequestStatusMeta[existingRequest.status].label;
		const nextStatusLabel = quoteRequestStatusMeta[params.status].label;
		const previousOwner = existingRequest.assignedTo || 'Unassigned';
		const nextOwner = nextAssignedTo || 'Unassigned';

		if (existingRequest.status !== params.status) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: `Status changed · ${previousStatusLabel} → ${nextStatusLabel}`,
					note: `Request moved from ${previousStatusLabel} to ${nextStatusLabel}.`
				})
			);
		}

		if (existingRequest.assignedTo !== nextAssignedTo) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: `Owner reassigned · ${previousOwner} → ${nextOwner}`,
					note: `Ownership changed from ${previousOwner} to ${nextOwner}.`
				})
			);
		}

		if (existingRequest.status === 'inspection-scheduled' && params.status !== 'inspection-scheduled') {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: 'Site visit completed',
					note: `Site work was marked complete and the request advanced to ${nextStatusLabel}.`
				})
			);
		}

		if (params.status === 'estimate-sent' && existingRequest.status !== 'estimate-sent') {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: 'Estimate sent to customer',
					note: 'Estimate packet was marked sent from the main request workspace.'
				})
			);
		}

		if (existingMissingInfoReasonCodes.join('|') !== missingInfoReasonCodes.join('|')) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: missingInfoReasonCodes.length ? 'Needs info reasons updated' : 'Needs info reasons cleared',
					note: missingInfoReasonCodes.length ? formatMissingInfoReasonSummary(missingInfoReasonCodes) : 'All qualification blockers were cleared.'
				})
			);
		}

		const editedFieldLabels = [
			existingRequest.nextAction !== nextAction ? 'next action' : '',
			existingRequest.contactName !== draftRequest.contactName ? 'contact name' : '',
			existingRequest.email !== draftRequest.email ? 'email' : '',
			existingRequest.phone !== draftRequest.phone ? 'phone' : '',
			existingRequest.siteName !== draftRequest.siteName ? 'site name' : '',
			existingRequest.serviceAddress !== draftRequest.serviceAddress ? 'service address' : '',
			existingRequest.requestedTimeline !== draftRequest.requestedTimeline ? 'requested timeline' : ''
		].filter(Boolean);

		if (editedFieldLabels.length) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: 'Request details updated',
					note: `Updated ${editedFieldLabels.join(', ')}.`
				})
			);
		}

		if (!activityEvents.length) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc,
					label: 'Request details reviewed',
					note: 'Internal Admin reviewed the request workspace with no visible field changes.'
				})
			);
		}

		return {
			...draftRequest,
			qualification: {
				missingInfoReasonCodes,
				reviewedAtUtc: occurredAtUtc,
				reviewedBy: internalAdminActor
			},
			timeline: [...existingRequest.timeline, ...activityEvents]
		};
	};

	try {
		const existingResponse = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			headers: getApiHeaders()
		});
		const existingRecord = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(existingResponse);
		const existingRequest = toQuoteRequest(existingRecord);

		if (!existingRequest) {
			throw error(404, 'Quote request record was not found in the API');
		}

		const updatedRequest = buildUpdatedRequest(existingRequest);
		const body = isQuoteRequestDto(existingRecord)
			? toQuoteRequestDto(updatedRequest, existingRecord)
			: toLegacyLeadDto(updatedRequest, existingRecord);

		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			method: 'PUT',
			headers: getApiHeaders(),
			body: JSON.stringify(body)
		});

		const saved = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
		return toQuoteRequest(saved) ?? updatedRequest;
	} catch (cause) {
		console.error('Quote request update failed.', cause);
		throw cause;
	}
};

export const recordQuoteRequestActivity = async (
	fetch: typeof globalThis.fetch,
	params: {
		id: string;
		label: string;
		note?: string;
		type?: QuoteRequestTimelineEvent['type'];
		nextAction?: string;
	}
) => {
	const occurredAtUtc = new Date().toISOString();

	const buildUpdatedRequest = (existingRequest: QuoteRequest): QuoteRequest => ({
		...existingRequest,
		nextAction: params.nextAction ?? existingRequest.nextAction,
		timeline: [
			...existingRequest.timeline,
			createActivityEvent({
				occurredAtUtc,
				label: params.label,
				note: params.note,
				type: params.type
			})
		]
	});

	try {
		const existingResponse = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			headers: getApiHeaders()
		});
		const existingRecord = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(existingResponse);
		const existingRequest = toQuoteRequest(existingRecord);

		if (!existingRequest) {
			throw error(404, 'Quote request record was not found in the API');
		}

		const updatedRequest = buildUpdatedRequest(existingRequest);
		const body = isQuoteRequestDto(existingRecord)
			? toQuoteRequestDto(updatedRequest, existingRecord)
			: toLegacyLeadDto(updatedRequest, existingRecord);

		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			method: 'PUT',
			headers: getApiHeaders(),
			body: JSON.stringify(body)
		});

		const saved = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
		return toQuoteRequest(saved) ?? updatedRequest;
	} catch (cause) {
		console.error('Quote request activity update failed.', cause);
		throw cause;
	}
};

export const scheduleQuoteRequestSiteVisit = async (
	fetch: typeof globalThis.fetch,
	params: {
		id: string;
		visitDate: string;
		windowStart: string;
		windowEnd: string;
		siteContact: string;
		siteContactPhone?: string;
		assignedFieldResource: string;
		notes?: string;
	}
) => {
	const scheduledAtUtc = new Date().toISOString();
	const normalizedSchedule = normalizeQuoteRequestSiteVisitSchedule({
		visitDate: params.visitDate,
		windowStart: params.windowStart,
		windowEnd: params.windowEnd,
		siteContact: params.siteContact,
		siteContactPhone: params.siteContactPhone ?? '',
		assignedFieldResource: params.assignedFieldResource,
		notes: params.notes ?? '',
		scheduledAtUtc,
		scheduledBy: internalAdminActor
	});

	if (!normalizedSchedule) {
		throw error(400, 'Site visit schedule details were incomplete.');
	}

	const buildUpdatedRequest = (existingRequest: QuoteRequest): QuoteRequest => {
		const existingSchedule = existingRequest.siteVisitSchedule;
		const visitWindowLabel = buildSiteVisitWindowLabel(normalizedSchedule);
		const visitDateLabel = formatSiteVisitDateLabel(normalizedSchedule.visitDate);
		const isReschedule = Boolean(
			existingSchedule &&
				(existingSchedule.visitDate !== normalizedSchedule.visitDate ||
					existingSchedule.windowStart !== normalizedSchedule.windowStart ||
					existingSchedule.windowEnd !== normalizedSchedule.windowEnd ||
					existingSchedule.siteContact !== normalizedSchedule.siteContact ||
					existingSchedule.siteContactPhone !== normalizedSchedule.siteContactPhone ||
					existingSchedule.assignedFieldResource !== normalizedSchedule.assignedFieldResource ||
					(existingSchedule.notes ?? '') !== (normalizedSchedule.notes ?? ''))
		);
		const previousVisitLabel = existingSchedule
			? `${formatSiteVisitDateLabel(existingSchedule.visitDate)} · ${buildSiteVisitWindowLabel(existingSchedule)}`
			: '';
		const nextAction = [
			`${isReschedule ? 'Site visit rescheduled' : 'Site visit scheduled'} for ${visitDateLabel} (${visitWindowLabel}).`,
			`Field resource: ${normalizedSchedule.assignedFieldResource}.`,
			`Site contact: ${normalizedSchedule.siteContact}${normalizedSchedule.siteContactPhone ? ` · ${normalizedSchedule.siteContactPhone}` : ''}.`,
			normalizedSchedule.notes ? `Notes: ${normalizedSchedule.notes}` : ''
		]
			.filter(Boolean)
			.join(' ');
		const activityEvents: QuoteRequestTimelineEvent[] = [];

		if (existingRequest.status !== 'inspection-scheduled') {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc: scheduledAtUtc,
					label: `Status changed · ${quoteRequestStatusMeta[existingRequest.status].label} → ${quoteRequestStatusMeta['inspection-scheduled'].label}`,
					note: 'Request moved into the site visit lane.'
				})
			);
		}

		if (existingRequest.assignedTo !== normalizedSchedule.assignedFieldResource) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc: scheduledAtUtc,
					label: `Owner reassigned · ${existingRequest.assignedTo || 'Unassigned'} → ${normalizedSchedule.assignedFieldResource}`,
					note: `Field ownership moved to ${normalizedSchedule.assignedFieldResource}.`
				})
			);
		}

		activityEvents.push(
			createActivityEvent({
				occurredAtUtc: scheduledAtUtc,
				type: isReschedule ? 'site-visit-rescheduled' : 'site-visit-scheduled',
				label: `${isReschedule ? 'Site visit rescheduled' : 'Site visit scheduled'} · ${visitDateLabel} · ${visitWindowLabel}`,
				note: isReschedule
					? [`Previous visit: ${previousVisitLabel}.`, normalizedSchedule.notes].filter(Boolean).join(' ')
					: normalizedSchedule.notes,
				siteVisitSchedule: normalizedSchedule
			})
		);

		return {
			...existingRequest,
			status: 'inspection-scheduled',
			assignedTo: normalizedSchedule.assignedFieldResource,
			nextAction,
			qualification: {
				missingInfoReasonCodes: [],
				reviewedAtUtc: scheduledAtUtc,
				reviewedBy: internalAdminActor
			},
			siteVisitSchedule: normalizedSchedule,
			timeline: [...existingRequest.timeline, ...activityEvents]
		};
	};

	try {
		const existingResponse = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			headers: getApiHeaders()
		});
		const existingRecord = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(existingResponse);
		const existingRequest = toQuoteRequest(existingRecord);

		if (!existingRequest) {
			throw error(404, 'Quote request record was not found in the API');
		}

		const updatedRequest = buildUpdatedRequest(existingRequest);
		const body = isQuoteRequestDto(existingRecord)
			? toQuoteRequestDto(updatedRequest, existingRecord)
			: toLegacyLeadDto(updatedRequest, existingRecord);

		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			method: 'PUT',
			headers: getApiHeaders(),
			body: JSON.stringify(body)
		});

		const saved = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
		return toQuoteRequest(saved) ?? updatedRequest;
	} catch (cause) {
		console.error('Quote request site visit scheduling failed.', cause);
		throw cause;
	}
};

export const cancelQuoteRequestSiteVisit = async (
	fetch: typeof globalThis.fetch,
	params: {
		id: string;
		reasonCode: QuoteRequestSiteVisitCancellationReasonCode;
		notes?: string;
	}
) => {
	if (!isQuoteRequestSiteVisitCancellationReasonCode(params.reasonCode)) {
		throw error(400, 'A valid site visit cancellation reason code is required.');
	}

	const cancelledAtUtc = new Date().toISOString();
	const reasonMeta = quoteRequestSiteVisitCancellationReasonMeta[params.reasonCode];

	const buildUpdatedRequest = (existingRequest: QuoteRequest): QuoteRequest => {
		if (!existingRequest.siteVisitSchedule) {
			throw error(400, 'This request does not have a scheduled site visit to cancel.');
		}

		const previousVisitLabel = `${formatSiteVisitDateLabel(
			existingRequest.siteVisitSchedule.visitDate
		)} · ${buildSiteVisitWindowLabel(existingRequest.siteVisitSchedule)}`;
		const activityEvents: QuoteRequestTimelineEvent[] = [];

		if (existingRequest.status !== 'qualified') {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc: cancelledAtUtc,
					label: `Status changed · ${quoteRequestStatusMeta[existingRequest.status].label} → ${quoteRequestStatusMeta.qualified.label}`,
					note: 'Request moved back into the qualified scheduling lane after the site visit cancellation.'
				})
			);
		}

		if (existingRequest.assignedTo !== officeQueueOwner) {
			activityEvents.push(
				createActivityEvent({
					occurredAtUtc: cancelledAtUtc,
					label: `Owner reassigned · ${existingRequest.assignedTo || 'Unassigned'} → ${officeQueueOwner}`,
					note: 'Office follow-up now owns the request until a new visit is booked.'
				})
			);
		}

		activityEvents.push(
			createActivityEvent({
				occurredAtUtc: cancelledAtUtc,
				type: 'site-visit-cancelled',
				label: `Site visit cancelled · ${reasonMeta.label}`,
				note: [`Previous visit: ${previousVisitLabel}.`, params.notes?.trim()].filter(Boolean).join(' ')
			})
		);

		return {
			...existingRequest,
			status: 'qualified',
			assignedTo: officeQueueOwner,
			nextAction: [
				`Reschedule site visit after ${reasonMeta.label.toLowerCase()}.`,
				params.notes?.trim() ? `Notes: ${params.notes.trim()}` : ''
			]
				.filter(Boolean)
				.join(' '),
			qualification: {
				...existingRequest.qualification,
				reviewedAtUtc: cancelledAtUtc,
				reviewedBy: internalAdminActor
			},
			siteVisitSchedule: null,
			timeline: [...existingRequest.timeline, ...activityEvents]
		};
	};

	try {
		const existingResponse = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			headers: getApiHeaders()
		});
		const existingRecord = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(existingResponse);
		const existingRequest = toQuoteRequest(existingRecord);

		if (!existingRequest) {
			throw error(404, 'Quote request record was not found in the API');
		}

		const updatedRequest = buildUpdatedRequest(existingRequest);
		const body = isQuoteRequestDto(existingRecord)
			? toQuoteRequestDto(updatedRequest, existingRecord)
			: toLegacyLeadDto(updatedRequest, existingRecord);

		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
			method: 'PUT',
			headers: getApiHeaders(),
			body: JSON.stringify(body)
		});

		const saved = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
		return toQuoteRequest(saved) ?? updatedRequest;
	} catch (cause) {
		console.error('Quote request site visit cancellation failed.', cause);
		throw cause;
	}
};
