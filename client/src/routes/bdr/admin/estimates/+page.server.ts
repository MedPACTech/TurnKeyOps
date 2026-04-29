import { fail } from '@sveltejs/kit';
import { loadQuoteRequests, recordQuoteRequestActivity, updateQuoteRequest } from '$lib/server/quote-requests';

const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const draftStoreDir = `${getCwd()}/.svelte-kit`;
const draftStorePath = `${draftStoreDir}/local-estimate-drafts.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
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
	savedAtUtc: string;
	sentAtUtc?: string;
	sentBy?: string;
	revisionHistory: EstimateRevisionRecord[];
};

type EstimateRevisionRecord = {
	revisionNumber: number;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	visitFindings: string;
	scopeLineItems: string[];
	notes: string;
	assumptions: string[];
	status: EstimateDraftRecord['status'];
	commercialSummary: string;
	savedAtUtc: string;
	sentAtUtc?: string;
	sentBy?: string;
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const parseRevisionNumber = (value: FormDataEntryValue | null) => {
	const parsed = Number.parseInt(String(value ?? '').trim(), 10);
	return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
};

const normalizeRevisionRecord = (
	revision: Partial<EstimateRevisionRecord> | undefined,
	fallbackRevisionNumber: number
): EstimateRevisionRecord => ({
	revisionNumber:
		typeof revision?.revisionNumber === 'number' && revision.revisionNumber > 0
			? revision.revisionNumber
			: fallbackRevisionNumber,
	customerName: revision?.customerName?.trim() ?? '',
	siteName: revision?.siteName?.trim() ?? '',
	serviceSummary: revision?.serviceSummary?.trim() ?? '',
	visitFindings: revision?.visitFindings?.trim() ?? '',
	scopeLineItems: Array.isArray(revision?.scopeLineItems)
		? revision.scopeLineItems.map((entry) => String(entry).trim()).filter(Boolean)
		: [],
	notes: revision?.notes?.trim() ?? '',
	assumptions: Array.isArray(revision?.assumptions)
		? revision.assumptions.map((entry) => String(entry).trim()).filter(Boolean)
		: [],
	status:
		revision?.status === 'ready-to-send' || revision?.status === 'sent' ? revision.status : 'draft',
	commercialSummary: revision?.commercialSummary?.trim() ?? '',
	savedAtUtc: revision?.savedAtUtc?.trim() ?? new Date().toISOString(),
	sentAtUtc: revision?.sentAtUtc?.trim() || undefined,
	sentBy: revision?.sentBy?.trim() || undefined
});

const normalizeDraftRecord = (
	requestId: string,
	record: Partial<EstimateDraftRecord> | undefined
): EstimateDraftRecord => {
	const revisionHistory = Array.isArray(record?.revisionHistory)
		? record.revisionHistory.map((entry, index) =>
				normalizeRevisionRecord(entry, Number(index) + 1)
			)
		: [];
	const revisionNumber =
		typeof record?.revisionNumber === 'number' && record.revisionNumber > 0
			? record.revisionNumber
			: revisionHistory.reduce((max, entry) => Math.max(max, entry.revisionNumber), 0) + 1;
	const scopeLineItems = Array.isArray(record?.scopeLineItems)
		? record.scopeLineItems.map((entry) => String(entry).trim()).filter(Boolean)
		: [];
	const assumptions = Array.isArray(record?.assumptions)
		? record.assumptions.map((entry) => String(entry).trim()).filter(Boolean)
		: [];
	return {
		requestId,
		revisionNumber,
		customerName: record?.customerName?.trim() ?? '',
		siteName: record?.siteName?.trim() ?? '',
		serviceSummary: record?.serviceSummary?.trim() ?? '',
		visitFindings: record?.visitFindings?.trim() ?? '',
		scopeLineItems,
		notes: record?.notes?.trim() ?? '',
		assumptions,
		status: record?.status === 'ready-to-send' || record?.status === 'sent' ? record.status : 'draft',
		commercialSummary:
			record?.commercialSummary?.trim() ?? buildCommercialSummary(scopeLineItems, assumptions),
		savedAtUtc: record?.savedAtUtc?.trim() ?? new Date().toISOString(),
		sentAtUtc: record?.sentAtUtc?.trim() || undefined,
		sentBy: record?.sentBy?.trim() || undefined,
		revisionHistory
	};
};

const toRevisionRecord = (draft: EstimateDraftRecord): EstimateRevisionRecord => ({
	revisionNumber: draft.revisionNumber,
	customerName: draft.customerName,
	siteName: draft.siteName,
	serviceSummary: draft.serviceSummary,
	visitFindings: draft.visitFindings,
	scopeLineItems: [...draft.scopeLineItems],
	notes: draft.notes,
	assumptions: [...draft.assumptions],
	status: draft.status,
	commercialSummary: draft.commercialSummary,
	savedAtUtc: draft.savedAtUtc,
	sentAtUtc: draft.sentAtUtc,
	sentBy: draft.sentBy
});

const readEstimateDrafts = async (): Promise<Record<string, EstimateDraftRecord>> => {
	try {
		const fs = await getFs();
		const raw = await fs.readFile(draftStorePath, 'utf-8');
		const parsed = JSON.parse(raw) as Record<string, EstimateDraftRecord>;
		if (!parsed || typeof parsed !== 'object') return {};
		return Object.fromEntries(
			Object.entries(parsed).map(([requestId, record]) => [requestId, normalizeDraftRecord(requestId, record)])
		);
	} catch {
		return {};
	}
};

const writeEstimateDrafts = async (drafts: Record<string, EstimateDraftRecord>) => {
	const fs = await getFs();
	await fs.mkdir(draftStoreDir, { recursive: true });
	await fs.writeFile(draftStorePath, JSON.stringify(drafts, null, 2));
};

const parseLines = (value: FormDataEntryValue | null) =>
	String(value ?? '')
		.split('\n')
		.map((entry) => entry.trim())
		.filter(Boolean);

const parseDraftStatus = (value: FormDataEntryValue | null): EstimateDraftRecord['status'] => {
	const normalized = String(value ?? '').trim();
	if (normalized === 'ready-to-send' || normalized === 'sent') return normalized;
	return 'draft';
};

const buildCommercialSummary = (scopeLineItems: string[], assumptions: string[]) =>
	[
		scopeLineItems.length ? `${scopeLineItems.length} scope line item(s)` : 'No scope line items yet',
		assumptions.length ? `${assumptions.length} assumption(s)` : 'No assumptions recorded',
		'Estimate draft prepared for review before send'
	].join(' · ');

const parseDraftRecord = (formData: FormData, overrides?: Partial<EstimateDraftRecord>): EstimateDraftRecord => {
	const scopeLineItems = parseLines(formData.get('scopeLineItems'));
	const assumptions = parseLines(formData.get('assumptions'));
	const savedAtUtc = overrides?.savedAtUtc ?? new Date().toISOString();
	const revisionHistory = Array.isArray(overrides?.revisionHistory) ? overrides.revisionHistory : [];
	return {
		requestId: String(formData.get('requestId') ?? '').trim(),
		revisionNumber: overrides?.revisionNumber ?? parseRevisionNumber(formData.get('revisionNumber')),
		customerName: String(formData.get('customerName') ?? '').trim(),
		siteName: String(formData.get('siteName') ?? '').trim(),
		serviceSummary: String(formData.get('serviceSummary') ?? '').trim(),
		visitFindings: String(formData.get('visitFindings') ?? '').trim(),
		scopeLineItems,
		notes: String(formData.get('notes') ?? '').trim(),
		assumptions,
		status: overrides?.status ?? parseDraftStatus(formData.get('draftStatus')),
		commercialSummary: overrides?.commercialSummary ?? buildCommercialSummary(scopeLineItems, assumptions),
		savedAtUtc,
		sentAtUtc: overrides?.sentAtUtc,
		sentBy: overrides?.sentBy,
		revisionHistory
	};
};

export const load = async ({ fetch }) => {
	const { requests } = await loadQuoteRequests(fetch);
	return {
		quoteRequests: requests,
		estimateDrafts: await readEstimateDrafts()
	};
};

export const actions = {
	saveDraft: async ({ request }) => {
		const formData = await request.formData();
		const requestId = String(formData.get('requestId') ?? '').trim();

		if (!requestId) {
			return fail(400, {
				draftMessage: 'Choose a source request before saving the estimate draft.',
				savedRequestId: requestId
			});
		}

		const drafts = await readEstimateDrafts();
		const existingDraft = drafts[requestId];
		const nextDraft = parseDraftRecord(formData, {
			revisionNumber: existingDraft?.revisionNumber ?? parseRevisionNumber(formData.get('revisionNumber')),
			revisionHistory: existingDraft?.revisionHistory ?? []
		});
		drafts[requestId] = nextDraft;
		await writeEstimateDrafts(drafts);

		return {
			draftSaved: true,
			savedRequestId: requestId,
			draftSavedAtUtc: nextDraft.savedAtUtc
		};
	},
	sendDraft: async ({ fetch, request }) => {
		const formData = await request.formData();
		const requestId = String(formData.get('requestId') ?? '').trim();
		const draftStatus = parseDraftStatus(formData.get('draftStatus'));

		if (!requestId) {
			return fail(400, {
				draftMessage: 'Choose a source request before sending the estimate.',
				savedRequestId: requestId
			});
		}

		if (draftStatus !== 'ready-to-send') {
			return fail(400, {
				draftMessage: 'Move the estimate draft to Ready to Send before sending it to the customer.',
				savedRequestId: requestId
			});
		}

		const { requests } = await loadQuoteRequests(fetch);
		const sourceRequest = requests.find((entry) => entry.id === requestId);
		if (!sourceRequest) {
			return fail(404, {
				draftMessage: 'The source request could not be found for this estimate send.',
				savedRequestId: requestId
			});
		}

		const sentAtUtc = new Date().toISOString();
		const sentBy = 'Internal Admin';
		const drafts = await readEstimateDrafts();
		const existingDraft = drafts[requestId];
		const nextDraft = parseDraftRecord(formData, {
			status: 'sent',
			revisionNumber: existingDraft?.revisionNumber ?? parseRevisionNumber(formData.get('revisionNumber')),
			revisionHistory: existingDraft?.revisionHistory ?? [],
			savedAtUtc: sentAtUtc,
			sentAtUtc,
			sentBy,
			commercialSummary: buildCommercialSummary(
				parseLines(formData.get('scopeLineItems')),
				parseLines(formData.get('assumptions'))
			)
		});

		drafts[requestId] = nextDraft;
		await writeEstimateDrafts(drafts);

		await updateQuoteRequest(fetch, {
			id: sourceRequest.id,
			status: 'estimate-sent',
			assignedTo: sourceRequest.assignedTo,
			nextAction: `Estimate sent by ${sentBy} on ${new Date(sentAtUtc).toLocaleString('en-US', {
				month: 'short',
				day: 'numeric',
				hour: 'numeric',
				minute: '2-digit'
			})}.`,
			missingInfoReasonCodes: sourceRequest.qualification.missingInfoReasonCodes,
			contactName: sourceRequest.contactName,
			email: sourceRequest.email,
			phone: sourceRequest.phone,
			siteName: sourceRequest.siteName,
			serviceAddress: sourceRequest.serviceAddress,
			requestedTimeline: sourceRequest.requestedTimeline
		});

		return {
			draftSent: true,
			savedRequestId: requestId,
			draftSentAtUtc: sentAtUtc
		};
	},
	createRevision: async ({ fetch, request }) => {
		const formData = await request.formData();
		const requestId = String(formData.get('requestId') ?? '').trim();

		if (!requestId) {
			return fail(400, {
				draftMessage: 'Choose a source request before creating a revision.',
				savedRequestId: requestId
			});
		}

		const { requests } = await loadQuoteRequests(fetch);
		const sourceRequest = requests.find((entry) => entry.id === requestId);
		if (!sourceRequest) {
			return fail(404, {
				draftMessage: 'The source request could not be found for this estimate revision.',
				savedRequestId: requestId
			});
		}

		const drafts = await readEstimateDrafts();
		const currentDraft = parseDraftRecord(formData, {
			revisionNumber: drafts[requestId]?.revisionNumber ?? parseRevisionNumber(formData.get('revisionNumber')),
			revisionHistory: drafts[requestId]?.revisionHistory ?? [],
			savedAtUtc: drafts[requestId]?.savedAtUtc ?? new Date().toISOString(),
			sentAtUtc: drafts[requestId]?.sentAtUtc,
			sentBy: drafts[requestId]?.sentBy,
			commercialSummary: buildCommercialSummary(
				parseLines(formData.get('scopeLineItems')),
				parseLines(formData.get('assumptions'))
			)
		});
		const nextRevisionNumber = currentDraft.revisionNumber + 1;
		const createdAtUtc = new Date().toISOString();
		const nextDraft: EstimateDraftRecord = {
			...currentDraft,
			revisionNumber: nextRevisionNumber,
			status: 'draft',
			savedAtUtc: createdAtUtc,
			sentAtUtc: undefined,
			sentBy: undefined,
			revisionHistory: [...currentDraft.revisionHistory, toRevisionRecord(currentDraft)]
		};

		drafts[requestId] = nextDraft;
		await writeEstimateDrafts(drafts);

		await recordQuoteRequestActivity(fetch, {
			id: sourceRequest.id,
			type: 'estimate-revised',
			label: `Estimate revision v${nextRevisionNumber} created`,
			note: `Internal Admin created revision v${nextRevisionNumber} from v${currentDraft.revisionNumber}.`,
			nextAction: `Estimate revision v${nextRevisionNumber} opened for review.`
		});

		return {
			revisionCreated: true,
			savedRequestId: requestId,
			revisionNumber: nextRevisionNumber
		};
	}
};
