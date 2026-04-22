import { env } from '$env/dynamic/private';
import { error } from '@sveltejs/kit';
import {
	buildQuoteRequestInbox,
	createQuoteRequestFromForm,
	seededQuoteRequests,
	type QuoteRequest,
	type QuoteRequestFormInput,
	type QuoteRequestPriority,
	type QuoteRequestStatus
} from '$lib/quote-requests';
import type { ApiEnvelope } from '$lib/types/mvp';

const defaultApiBaseUrl = 'http://localhost:5178';
const quoteMarker = 'TKO_BDR_QUOTE_REQUEST::';
const demoTenantId = '7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b';

type QuoteRequestDto = {
	id: string;
	tenantId: string;
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
	source: QuoteRequest['source'];
	status: QuoteRequestStatus;
	assignedTo: string;
	nextAction: string;
	intakeSummary: string;
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
	serviceAddress: string;
	propertyType: string;
	preferredTimeline: string;
	priority: QuoteRequestPriority;
	message: string;
	status: QuoteRequestStatus;
	nextAction: string;
	intakeSummary: string;
	source: QuoteRequest['source'];
};

const statusToPipelineStage: Record<QuoteRequestStatus, string> = {
	new: 'New',
	contacted: 'Contacted',
	'inspection-scheduled': 'Inspection Scheduled',
	'estimate-drafted': 'Estimate Drafted',
	'estimate-sent': 'Quoted',
	won: 'Won',
	closed: 'Closed'
};

const pipelineStageToStatus = (value: string | null | undefined): QuoteRequestStatus => {
	const normalized = String(value ?? '').trim().toLowerCase();
	if (normalized === 'contacted') return 'contacted';
	if (normalized === 'inspection scheduled') return 'inspection-scheduled';
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

const getApiBaseUrl = () =>
	(env.PUBLIC_TKO_API_BASE_URL || env.TKO_API_BASE_URL || defaultApiBaseUrl).replace(/\/$/, '');

const getApiHeaders = () => {
	const headers: Record<string, string> = {
		Accept: 'application/json',
		'Content-Type': 'application/json'
	};

	const bearerToken = env.TKO_API_BEARER_TOKEN || env.TKO_API_TOKEN || env.TKO_API_AUTH_TOKEN;
	if (bearerToken) {
		headers.Authorization = `Bearer ${bearerToken}`;
	}

	return headers;
};

const unwrapEnvelope = async <T>(response: Response): Promise<T> => {
	if (!response.ok) {
		throw error(response.status, `Quote request API call failed with ${response.status}`);
	}

	const payload = (await response.json()) as ApiEnvelope<T> | T;
	if (payload && typeof payload === 'object' && 'success' in payload && 'data' in payload) {
		if (!payload.success) {
			throw error(502, 'Quote request API response was not successful');
		}

		return payload.data;
	}

	return payload as T;
};

const isQuoteRequestDto = (value: unknown): value is QuoteRequestDto => {
	if (!value || typeof value !== 'object') return false;
	return 'submittedAtUtc' in value && 'serviceAddress' in value && 'preferredTimeline' in value;
};

const toQuoteRequest = (record: QuoteRequestDto | LegacyLeadDto): QuoteRequest | null => {
	if (isQuoteRequestDto(record)) {
		return {
			id: record.id,
			submittedAtUtc: record.submittedAtUtc,
			customerName: record.customerName,
			email: record.email,
			phone: record.phone,
			serviceAddress: record.serviceAddress,
			projectType: record.projectType,
			propertyType: record.propertyType,
			preferredTimeline: record.preferredTimeline,
			priority: record.priority,
			message: record.message,
			source: record.source,
			status: record.status,
			assignedTo: record.assignedTo,
			nextAction: record.nextAction,
			intakeSummary: record.intakeSummary
		};
	}

	const metadata = parseLeadMetadata(record.scopeSummary);
	if (!metadata) return null;

	return {
		id: record.id,
		submittedAtUtc: metadata.submittedAtUtc,
		customerName: record.contactName || record.companyName,
		email: record.contactEmail ?? '',
		phone: record.contactPhone ?? '',
		serviceAddress: metadata.serviceAddress,
		projectType: record.projectType ?? 'Quote request',
		propertyType: metadata.propertyType,
		preferredTimeline: metadata.preferredTimeline,
		priority: metadata.priority,
		message: metadata.message,
		source: metadata.source ?? normalizeSource(record.source),
		status: metadata.status ?? pipelineStageToStatus(record.pipelineStage),
		assignedTo: record.assignedEstimator?.trim() || 'Office intake',
		nextAction: metadata.nextAction,
		intakeSummary: metadata.intakeSummary
	};
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
	tenantId: existing?.tenantId ?? env.TKO_API_TENANT_ID ?? demoTenantId,
	submittedAtUtc: existing?.submittedAtUtc ?? request.submittedAtUtc,
	customerName: request.customerName,
	email: request.email,
	phone: request.phone,
	serviceAddress: request.serviceAddress,
	projectType: request.projectType,
	propertyType: request.propertyType,
	preferredTimeline: request.preferredTimeline,
	priority: request.priority,
	message: request.message,
	source: request.source,
	status: request.status,
	assignedTo: request.assignedTo,
	nextAction: request.nextAction,
	intakeSummary: request.intakeSummary,
	updatedAtUtc: existing?.updatedAtUtc ?? null
});

const toLegacyLeadDto = (request: QuoteRequest, existing?: LegacyLeadDto): LegacyLeadDto => {
	const { city, state } = parseCityState(request.serviceAddress);
	const metadata: QuoteLeadMetadata = {
		submittedAtUtc: request.submittedAtUtc,
		serviceAddress: request.serviceAddress,
		propertyType: request.propertyType,
		preferredTimeline: request.preferredTimeline,
		priority: request.priority,
		message: request.message,
		status: request.status,
		nextAction: request.nextAction,
		intakeSummary: request.intakeSummary,
		source: request.source
	};

	return {
		id: existing?.id ?? request.id,
		tenantId: existing?.tenantId ?? env.TKO_API_TENANT_ID ?? demoTenantId,
		leadNumber: existing?.leadNumber ?? `BDR-LEAD-${Date.now()}`,
		companyName: existing?.companyName?.trim() || request.customerName,
		contactName: request.customerName,
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
	fetch: typeof globalThis.fetch
): Promise<{ requests: QuoteRequest[]; source: 'api' | 'fallback' }> => {
	try {
		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests`, {
			headers: getApiHeaders()
		});
		const records = await unwrapEnvelope<Array<QuoteRequestDto | LegacyLeadDto>>(response);
		const requests = buildQuoteRequestInbox(records.map(toQuoteRequest).filter((request): request is QuoteRequest => Boolean(request)));
		return { requests, source: 'api' };
	} catch (cause) {
		console.warn('Falling back to seeded quote requests.', cause);
		return { requests: buildQuoteRequestInbox(seededQuoteRequests), source: 'fallback' };
	}
};

export const submitQuoteRequest = async (fetch: typeof globalThis.fetch, input: QuoteRequestFormInput) => {
	const request = createQuoteRequestFromForm(input);
	const response = await fetch(`${getApiBaseUrl()}/api/quote-requests`, {
		method: 'POST',
		headers: getApiHeaders(),
		body: JSON.stringify(toQuoteRequestDto(request))
	});

	await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
	return request;
};

export const updateQuoteRequest = async (
	fetch: typeof globalThis.fetch,
	params: { id: string; status: QuoteRequestStatus; assignedTo: string; nextAction: string }
) => {
	const existingResponse = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
		headers: getApiHeaders()
	});
	const existingRecord = await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(existingResponse);
	const existingRequest = toQuoteRequest(existingRecord);

	if (!existingRequest) {
		throw error(404, 'Quote request record was not found in the API');
	}

	const updatedRequest: QuoteRequest = {
		...existingRequest,
		status: params.status,
		assignedTo: params.assignedTo || 'Office intake',
		nextAction: params.nextAction || 'Review request and decide next office step.'
	};

	const body = isQuoteRequestDto(existingRecord)
		? toQuoteRequestDto(updatedRequest, existingRecord)
		: toLegacyLeadDto(updatedRequest, existingRecord);

	const response = await fetch(`${getApiBaseUrl()}/api/quote-requests/${params.id}`, {
		method: 'PUT',
		headers: getApiHeaders(),
		body: JSON.stringify(body)
	});

	await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
	return updatedRequest;
};
