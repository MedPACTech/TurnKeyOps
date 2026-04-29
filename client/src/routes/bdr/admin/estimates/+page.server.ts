import { fail } from '@sveltejs/kit';
import { loadQuoteRequests } from '$lib/server/quote-requests';

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
	savedAtUtc: string;
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

		const savedAtUtc = new Date().toISOString();
		const nextDraft: EstimateDraftRecord = {
			requestId,
			customerName: String(formData.get('customerName') ?? '').trim(),
			siteName: String(formData.get('siteName') ?? '').trim(),
			serviceSummary: String(formData.get('serviceSummary') ?? '').trim(),
			visitFindings: String(formData.get('visitFindings') ?? '').trim(),
			scopeLineItems: parseLines(formData.get('scopeLineItems')),
			notes: String(formData.get('notes') ?? '').trim(),
			assumptions: parseLines(formData.get('assumptions')),
			savedAtUtc
		};

		const drafts = await readEstimateDrafts();
		drafts[requestId] = nextDraft;
		await writeEstimateDrafts(drafts);

		return {
			draftSaved: true,
			savedRequestId: requestId,
			draftSavedAtUtc: savedAtUtc
		};
	}
};
