import { env } from '$env/dynamic/private';
import { error } from '@sveltejs/kit';
import {
	buildQuoteRequestQualification,
	buildQuoteRequestInbox,
	createQuoteRequestFromForm,
	normalizeQuoteRequestQualification,
	seededQuoteRequests,
	type QuoteRequest,
	type QuoteRequestAttachment,
	type QuoteRequestFormInput,
	type QuoteRequestMissingInfoReasonCode,
	type QuoteRequestPriority,
	type QuoteRequestQualificationReview,
	type QuoteRequestStatus,
	type QuoteRequestSubmittedPayload,
	type QuoteRequestTimelineEvent
} from '$lib/quote-requests';
import type { ApiEnvelope } from '$lib/types/mvp';

const defaultApiBaseUrl = 'http://localhost:5178';
const quoteMarker = 'TKO_BDR_QUOTE_REQUEST::';
const demoTenantId = '7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b';
const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const localStoreDir = `${getCwd()}/.svelte-kit`;
const localStorePath = `${localStoreDir}/local-quote-requests.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	stat: (path: string) => Promise<unknown>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

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
};

const statusToPipelineStage: Record<QuoteRequestStatus, string> = {
	new: 'New',
	'in-review': 'In Review',
	'needs-info': 'Needs Info',
	qualified: 'Qualified',
	contacted: 'Contacted',
	'inspection-scheduled': 'Inspection Scheduled',
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

export const getQuoteRequestTenantId = () => env.TKO_API_TENANT_ID ?? demoTenantId;

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
		timeline: Array.isArray(request.timeline) ? request.timeline : []
	};

	const submittedPayload = isSubmittedPayload(request.submittedPayload)
		? {
				...request.submittedPayload,
				attachments: normalizeAttachments(request.submittedPayload.attachments)
			}
		: buildSubmittedPayload(normalized);
	const timeline = normalized.timeline.length
		? normalized.timeline
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

	return {
		...normalized,
		submittedPayload,
		timeline
	};
};

const readLocalQuoteRequests = async (): Promise<QuoteRequest[]> => {
	try {
		const { readFile } = await getFs();
		const contents = await readFile(localStorePath, 'utf-8');
		const parsed = JSON.parse(contents) as unknown;
		if (!Array.isArray(parsed)) return [];
		return parsed.map((item) => normalizeQuoteRequest(item as QuoteRequest));
	} catch (cause) {
		if (cause && typeof cause === 'object' && 'code' in cause && cause.code === 'ENOENT') {
			return [];
		}
		console.warn('Unable to read local quote request store.', cause);
		return [];
	}
};

const localQuoteRequestStoreExists = async () => {
	try {
		const { stat } = await getFs();
		await stat(localStorePath);
		return true;
	} catch {
		return false;
	}
};

const writeLocalQuoteRequests = async (requests: QuoteRequest[]) => {
	const { mkdir, writeFile } = await getFs();
	await mkdir(localStoreDir, { recursive: true });
	await writeFile(localStorePath, JSON.stringify(requests.map(normalizeQuoteRequest), null, 2));
};

const mergeQuoteRequests = (...groups: QuoteRequest[][]) => {
	const byId = new Map<string, QuoteRequest>();
	for (const request of groups.flat()) {
		const normalized = normalizeQuoteRequest(request);
		if (!byId.has(normalized.id)) {
			byId.set(normalized.id, normalized);
		}
	}
	return buildQuoteRequestInbox([...byId.values()]);
};

const saveLocalQuoteRequest = async (request: QuoteRequest) => {
	const localRequests = await readLocalQuoteRequests();
	await writeLocalQuoteRequests(mergeQuoteRequests([request], localRequests));
};

const updateLocalQuoteRequest = async (updatedRequest: QuoteRequest) => {
	const localRequests = await readLocalQuoteRequests();
	const index = localRequests.findIndex((request) => request.id === updatedRequest.id);
	if (index === -1) return false;
	localRequests[index] = normalizeQuoteRequest(updatedRequest);
	await writeLocalQuoteRequests(localRequests);
	return true;
};

const isQuoteRequestDto = (value: unknown): value is QuoteRequestDto => {
	if (!value || typeof value !== 'object') return false;
	return 'submittedAtUtc' in value && 'serviceAddress' in value && 'preferredTimeline' in value;
};

const toQuoteRequest = (record: QuoteRequestDto | LegacyLeadDto): QuoteRequest | null => {
	if (isQuoteRequestDto(record)) {
		return normalizeQuoteRequest({
			id: record.id,
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
			timeline: record.timeline ?? []
		});
	}

	const metadata = parseLeadMetadata(record.scopeSummary);
	if (!metadata) return null;

	return normalizeQuoteRequest({
		id: record.id,
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
		timeline: metadata.timeline ?? []
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
	tenantId: existing?.tenantId ?? getQuoteRequestTenantId(),
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
		timeline: request.timeline
	};

	return {
		id: existing?.id ?? request.id,
		tenantId: existing?.tenantId ?? getQuoteRequestTenantId(),
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
	fetch: typeof globalThis.fetch
): Promise<{ requests: QuoteRequest[]; source: 'api' | 'fallback' }> => {
	const localRequests = await readLocalQuoteRequests();
	const hasLocalStore = await localQuoteRequestStoreExists();
	try {
		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests`, {
			headers: getApiHeaders()
		});
		const records = await unwrapEnvelope<Array<QuoteRequestDto | LegacyLeadDto>>(response);
		const apiRequests = records.map(toQuoteRequest).filter((request): request is QuoteRequest => Boolean(request));
		const requests = mergeQuoteRequests(localRequests, apiRequests);
		return { requests, source: 'api' };
	} catch (cause) {
		console.warn('Falling back to local quote requests.', cause);
		return {
			requests: hasLocalStore ? buildQuoteRequestInbox(localRequests) : mergeQuoteRequests(localRequests, seededQuoteRequests),
			source: 'fallback'
		};
	}
};

export const submitQuoteRequest = async (fetch: typeof globalThis.fetch, input: QuoteRequestFormInput) => {
	const request = createQuoteRequestFromForm(input);
	try {
		const response = await fetch(`${getApiBaseUrl()}/api/quote-requests`, {
			method: 'POST',
			headers: getApiHeaders(),
			body: JSON.stringify(toQuoteRequestDto(request))
		});

		await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
	} catch (cause) {
		console.warn('Quote request API unavailable; saving request locally for operator triage.', cause);
		await saveLocalQuoteRequest(request);
	}
	return request;
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
		const nextAssignedTo = params.assignedTo || 'Office intake';
		const selectedMissingInfoReasonCodes = normalizeQuoteRequestQualification({
			missingInfoReasonCodes: params.missingInfoReasonCodes
		}).missingInfoReasonCodes;
		const existingMissingInfoReasonCodes = normalizeQuoteRequestQualification(existingRequest.qualification).missingInfoReasonCodes;
		const changes = [
			existingRequest.status !== params.status ? `stage ${statusToPipelineStage[existingRequest.status]} to ${statusToPipelineStage[params.status]}` : '',
			existingRequest.assignedTo !== nextAssignedTo ? `owner ${existingRequest.assignedTo || 'Unassigned'} to ${nextAssignedTo}` : '',
			existingMissingInfoReasonCodes.join('|') !== selectedMissingInfoReasonCodes.join('|')
				? `missing-info reasons ${selectedMissingInfoReasonCodes.length ? 'updated' : 'cleared'}`
				: ''
		].filter(Boolean);

		const draftRequest = {
			...existingRequest,
			status: params.status,
			assignedTo: nextAssignedTo,
			nextAction: params.nextAction || 'Review request and decide next office step.',
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

		return {
			...draftRequest,
			qualification: {
				missingInfoReasonCodes,
				reviewedAtUtc: new Date().toISOString(),
				reviewedBy: 'External Admin'
			},
			timeline: [
				...existingRequest.timeline,
				{
					id: crypto.randomUUID(),
					occurredAtUtc: new Date().toISOString(),
					type: 'operator-updated',
					actor: 'External Admin',
					label: changes.length ? `Request updated: ${changes.join(', ')}` : 'Request details updated'
				}
			]
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

		await unwrapEnvelope<QuoteRequestDto | LegacyLeadDto>(response);
		return updatedRequest;
	} catch (cause) {
		console.warn('Quote request API unavailable; trying local quote request update.', cause);
		const localRequests = await readLocalQuoteRequests();
		const existingRequest = localRequests.find((request) => request.id === params.id);
		if (!existingRequest) {
			throw error(404, 'Quote request record was not found locally');
		}

		const updatedRequest = buildUpdatedRequest(existingRequest);
		await updateLocalQuoteRequest(updatedRequest);
		return updatedRequest;
	}
};
