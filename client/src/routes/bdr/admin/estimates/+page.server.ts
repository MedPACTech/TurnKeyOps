import { fail } from '@sveltejs/kit';
import { loadQuoteRequests, updateQuoteRequest } from '$lib/server/quote-requests';

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
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const readEstimateDrafts = async (): Promise<Record<string, EstimateDraftRecord>> => {
	try {
		const fs = await getFs();
		const raw = await fs.readFile(draftStorePath, 'utf-8');
		const parsed = JSON.parse(raw) as Record<string, EstimateDraftRecord>;
		return parsed && typeof parsed === 'object' ? parsed : {};
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
	return {
		requestId: String(formData.get('requestId') ?? '').trim(),
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
		sentBy: overrides?.sentBy
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

		const nextDraft = parseDraftRecord(formData);

		const drafts = await readEstimateDrafts();
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
		const nextDraft = parseDraftRecord(formData, {
			status: 'sent',
			savedAtUtc: sentAtUtc,
			sentAtUtc,
			sentBy,
			commercialSummary: buildCommercialSummary(
				parseLines(formData.get('scopeLineItems')),
				parseLines(formData.get('assumptions'))
			)
		});

		const drafts = await readEstimateDrafts();
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
	}
};
