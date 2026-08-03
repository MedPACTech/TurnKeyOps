import { error, fail } from '@sveltejs/kit';
import {
	loadQuoteRequests,
	recordQuoteRequestActivity,
	updateQuoteRequest
} from '$lib/server/quote-requests';

const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const draftStorePath = `${getCwd()}/.svelte-kit/local-estimate-drafts.json`;

type FsPromises = {
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

type EstimateLocationRecord = {
	id: string;
	name: string;
	lengthFeet: number;
	widthFeet: number;
	depthInches: number;
	wastePercent: number;
	numberOfPours: number;
};

type EstimateDeliveryRecord = {
	status: 'sent' | 'approved' | 'changes-requested';
	method: 'review-link';
	reviewUrl: string;
	email: string;
	phone: string;
	sentAtUtc: string;
	approvedAtUtc?: string;
	changesRequestedAtUtc?: string;
	responseNote?: string;
};

type EstimateDraftRecord = {
	requestId: string;
	revisionNumber: number;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	visitFindings: string;
	scopeLineItems: string[];
	notes: string;
	assumptions: string[];
	status: 'draft' | 'ready-to-send' | 'sent';
	commercialSummary: string;
	locations: EstimateLocationRecord[];
	savedAtUtc: string;
	sentAtUtc?: string;
	sentBy?: string;
	delivery?: EstimateDeliveryRecord;
	revisionHistory: unknown[];
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const parsePositiveNumber = (value: unknown, fallback: number) => {
	const parsed = Number(value);
	return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
};

const normalizeLocations = (value: unknown): EstimateLocationRecord[] => {
	if (!Array.isArray(value)) return [];
	return value
		.map((location, index) => {
			if (!location || typeof location !== 'object') return null;
			const record = location as Partial<EstimateLocationRecord>;
			const name = String(record.name ?? '').trim();
			if (!name) return null;
			return {
				id: String(record.id ?? `location-${index + 1}`).trim(),
				name,
				lengthFeet: parsePositiveNumber(record.lengthFeet, 0),
				widthFeet: parsePositiveNumber(record.widthFeet, 0),
				depthInches: parsePositiveNumber(record.depthInches, 4),
				wastePercent: parsePositiveNumber(record.wastePercent, 10),
				numberOfPours: Math.max(1, parsePositiveNumber(record.numberOfPours, 1))
			};
		})
		.filter(Boolean) as EstimateLocationRecord[];
};

const normalizeDelivery = (delivery: unknown): EstimateDeliveryRecord | undefined => {
	if (!delivery || typeof delivery !== 'object') return undefined;
	const record = delivery as Partial<EstimateDeliveryRecord>;
	if (
		record.status !== 'sent' &&
		record.status !== 'approved' &&
		record.status !== 'changes-requested'
	) {
		return undefined;
	}

	return {
		status: record.status,
		method: 'review-link',
		reviewUrl: String(record.reviewUrl ?? '').trim(),
		email: String(record.email ?? '').trim(),
		phone: String(record.phone ?? '').trim(),
		sentAtUtc: String(record.sentAtUtc ?? '').trim() || new Date().toISOString(),
		approvedAtUtc: record.approvedAtUtc?.trim() || undefined,
		changesRequestedAtUtc: record.changesRequestedAtUtc?.trim() || undefined,
		responseNote: record.responseNote?.trim() || undefined
	};
};

const normalizeDraft = (requestId: string, draft: Partial<EstimateDraftRecord>): EstimateDraftRecord => ({
	requestId,
	revisionNumber: typeof draft.revisionNumber === 'number' && draft.revisionNumber > 0 ? draft.revisionNumber : 1,
	customerName: draft.customerName?.trim() ?? '',
	siteName: draft.siteName?.trim() ?? '',
	serviceSummary: draft.serviceSummary?.trim() ?? '',
	visitFindings: draft.visitFindings?.trim() ?? '',
	scopeLineItems: Array.isArray(draft.scopeLineItems)
		? draft.scopeLineItems.map((entry) => String(entry).trim()).filter(Boolean)
		: [],
	notes: draft.notes?.trim() ?? '',
	assumptions: Array.isArray(draft.assumptions)
		? draft.assumptions.map((entry) => String(entry).trim()).filter(Boolean)
		: [],
	status: draft.status === 'ready-to-send' || draft.status === 'sent' ? draft.status : 'draft',
	commercialSummary: draft.commercialSummary?.trim() ?? '',
	locations: normalizeLocations(draft.locations),
	savedAtUtc: draft.savedAtUtc?.trim() ?? new Date().toISOString(),
	sentAtUtc: draft.sentAtUtc?.trim() || undefined,
	sentBy: draft.sentBy?.trim() || undefined,
	delivery: normalizeDelivery(draft.delivery),
	revisionHistory: Array.isArray(draft.revisionHistory) ? draft.revisionHistory : []
});

const readEstimateDrafts = async () => {
	try {
		const fs = await getFs();
		const raw = await fs.readFile(draftStorePath, 'utf-8');
		const parsed = JSON.parse(raw) as Record<string, Partial<EstimateDraftRecord>>;
		if (!parsed || typeof parsed !== 'object') return {};
		return Object.fromEntries(
			Object.entries(parsed).map(([requestId, draft]) => [requestId, normalizeDraft(requestId, draft)])
		) as Record<string, EstimateDraftRecord>;
	} catch {
		return {};
	}
};

const writeEstimateDrafts = async (drafts: Record<string, EstimateDraftRecord>) => {
	const fs = await getFs();
	await fs.writeFile(draftStorePath, JSON.stringify(drafts, null, 2));
};

const loadEstimatePacket = async (fetch: typeof globalThis.fetch, requestId: string) => {
	const drafts = await readEstimateDrafts();
	const draft = drafts[requestId];
	const { requests } = await loadQuoteRequests(fetch);
	const quoteRequest = requests.find((request) => request.id === requestId);
	if (!draft || !quoteRequest) throw error(404, 'Estimate packet not found.');
	return { draft, drafts, quoteRequest };
};

export const load = async ({ fetch, params, url }) => {
	const requestId = decodeURIComponent(params.requestId);
	const { draft, quoteRequest } = await loadEstimatePacket(fetch, requestId);
	const returnTo = url.searchParams.get('returnTo') ?? '';
	const safeReturnTo = returnTo.startsWith('/bdr/admin/') ? returnTo : '';
	return { draft, quoteRequest, returnTo: safeReturnTo };
};

export const actions = {
	approve: async ({ fetch, params }) => {
		const requestId = decodeURIComponent(params.requestId);
		const { draft, drafts, quoteRequest } = await loadEstimatePacket(fetch, requestId);
		const approvedAtUtc = new Date().toISOString();

		drafts[requestId] = {
			...draft,
			delivery: {
				status: 'approved',
				method: 'review-link',
				reviewUrl: draft.delivery?.reviewUrl ?? `/bdr/estimate/${encodeURIComponent(requestId)}`,
				email: draft.delivery?.email ?? quoteRequest.email,
				phone: draft.delivery?.phone ?? quoteRequest.phone,
				sentAtUtc: draft.delivery?.sentAtUtc ?? draft.sentAtUtc ?? approvedAtUtc,
				approvedAtUtc,
				responseNote: undefined
			}
		};
		await writeEstimateDrafts(drafts);

		await updateQuoteRequest(fetch, {
			id: quoteRequest.id,
			status: 'won',
			assignedTo: quoteRequest.assignedTo,
			nextAction: 'Customer approved the estimate. Draft invoice is ready for billing review.',
			missingInfoReasonCodes: quoteRequest.qualification.missingInfoReasonCodes,
			contactName: quoteRequest.contactName,
			email: quoteRequest.email,
			phone: quoteRequest.phone,
			siteName: quoteRequest.siteName,
			serviceAddress: quoteRequest.serviceAddress,
			requestedTimeline: quoteRequest.requestedTimeline
		});

		await recordQuoteRequestActivity(fetch, {
			id: quoteRequest.id,
			type: 'operator-updated',
			label: 'Estimate approved by customer',
			note: 'Customer approved the estimate from the review packet.',
			nextAction: 'Review draft invoice and send billing packet.'
		});

		return { approved: true };
	},
	requestChanges: async ({ fetch, request, params }) => {
		const requestId = decodeURIComponent(params.requestId);
		const formData = await request.formData();
		const responseNote = String(formData.get('responseNote') ?? '').trim();

		if (!responseNote) {
			return fail(400, { changeMessage: 'Add a short note so the office knows what to adjust.' });
		}

		const { draft, drafts, quoteRequest } = await loadEstimatePacket(fetch, requestId);
		const changesRequestedAtUtc = new Date().toISOString();

		drafts[requestId] = {
			...draft,
			status: 'ready-to-send',
			delivery: {
				status: 'changes-requested',
				method: 'review-link',
				reviewUrl: draft.delivery?.reviewUrl ?? `/bdr/estimate/${encodeURIComponent(requestId)}`,
				email: draft.delivery?.email ?? quoteRequest.email,
				phone: draft.delivery?.phone ?? quoteRequest.phone,
				sentAtUtc: draft.delivery?.sentAtUtc ?? draft.sentAtUtc ?? changesRequestedAtUtc,
				changesRequestedAtUtc,
				responseNote
			}
		};
		await writeEstimateDrafts(drafts);

		await updateQuoteRequest(fetch, {
			id: quoteRequest.id,
			status: 'estimate-drafted',
			assignedTo: quoteRequest.assignedTo,
			nextAction: `Customer requested estimate changes: ${responseNote}`,
			missingInfoReasonCodes: quoteRequest.qualification.missingInfoReasonCodes,
			contactName: quoteRequest.contactName,
			email: quoteRequest.email,
			phone: quoteRequest.phone,
			siteName: quoteRequest.siteName,
			serviceAddress: quoteRequest.serviceAddress,
			requestedTimeline: quoteRequest.requestedTimeline
		});

		await recordQuoteRequestActivity(fetch, {
			id: quoteRequest.id,
			type: 'operator-updated',
			label: 'Customer requested estimate changes',
			note: responseNote,
			nextAction: 'Review requested changes and create a revision.'
		});

		return { changesRequested: true };
	}
};
