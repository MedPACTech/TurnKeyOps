import { getTurnKeyApiBaseUrl, getTurnKeyApiHeaders, unwrapTurnKeyApiEnvelope } from './turnkey-api';

export type QuoteEstimateLocation = { id: string; name: string; lengthFeet: number; widthFeet: number; depthInches: number; wastePercent: number; numberOfPours: number; squareFeet?: number; cubicYards?: number; formLinearFeet?: number; rebarLinearFeet?: number; materialCost?: number; laborCost?: number; estimatedTotal?: number };
export type QuoteEstimate = {
	id: string; quoteRequestId: string; revisionNumber: number; customerName: string; siteName: string;
	serviceSummary: string; visitFindings: string; scopeLineItems: string[]; notes: string; assumptions: string[];
	status: 'draft' | 'ready-to-send' | 'sent'; commercialSummary: string; locations: QuoteEstimateLocation[];
	totals: { squareFeet: number; cubicYards: number; formLinearFeet: number; rebarLinearFeet: number; materialCost: number; laborCost: number; estimatedTotal: number };
	savedAtUtc: string; sentAtUtc?: string; sentBy?: string; expiresAtUtc?: string;
	delivery?: { status: 'sent' | 'approved' | 'changes-requested'; method: 'review-link'; reviewUrl: string; email: string; phone: string; sentAtUtc: string; approvedAtUtc?: string; changesRequestedAtUtc?: string; responseNote?: string };
	revisionHistory: unknown[]; version: string;
};

const api = (path: string, init?: RequestInit, fetcher: typeof globalThis.fetch = fetch) => fetcher(`${getTurnKeyApiBaseUrl()}${path}`, {
	...init, headers: { ...getTurnKeyApiHeaders(init?.body !== undefined), ...(init?.headers ?? {}) }
});

export const listQuoteEstimates = async (fetcher: typeof globalThis.fetch) =>
	unwrapTurnKeyApiEnvelope<QuoteEstimate[]>(await api('/api/quote-estimates', undefined, fetcher), 'List estimates');

export const saveQuoteEstimate = async (fetcher: typeof globalThis.fetch, requestId: string, input: {
	customerName: string; siteName: string; serviceSummary: string; visitFindings: string; notes: string;
	status: 'draft' | 'ready-to-send'; locations: QuoteEstimateLocation[]; version?: string;
}) => unwrapTurnKeyApiEnvelope<QuoteEstimate>(await api(`/api/quote-estimates/${requestId}`, {
	method: 'PUT', body: JSON.stringify({ ...input, expectedVersion: input.version || null })
}, fetcher), 'Save estimate');

export const sendQuoteEstimate = async (fetcher: typeof globalThis.fetch, requestId: string, version: string) =>
	unwrapTurnKeyApiEnvelope<QuoteEstimate>(await api(`/api/quote-estimates/${requestId}/send`, {
		method: 'POST', body: JSON.stringify({ expectedVersion: version })
	}, fetcher), 'Send estimate');

export const createQuoteEstimateRevision = async (fetcher: typeof globalThis.fetch, requestId: string, version: string) =>
	unwrapTurnKeyApiEnvelope<QuoteEstimate>(await api(`/api/quote-estimates/${requestId}/revisions`, {
		method: 'POST', body: JSON.stringify({ expectedVersion: version })
	}, fetcher), 'Create estimate revision');

export const getPublicQuoteEstimate = async (fetcher: typeof globalThis.fetch, tenantSlug: string, requestId: string, token: string) =>
	unwrapTurnKeyApiEnvelope<QuoteEstimate>(await fetcher(
		`${getTurnKeyApiBaseUrl()}/api/public/quote-estimates/${tenantSlug}/${requestId}?token=${encodeURIComponent(token)}`,
		{ headers: { Accept: 'application/json' } }
	), 'Load estimate');

export const decidePublicQuoteEstimate = async (fetcher: typeof globalThis.fetch, tenantSlug: string, requestId: string, token: string, decision: 'approve' | 'request-changes', responseNote?: string) =>
	unwrapTurnKeyApiEnvelope<QuoteEstimate>(await fetcher(
		`${getTurnKeyApiBaseUrl()}/api/public/quote-estimates/${tenantSlug}/${requestId}/${decision}`,
		{
			method: 'POST',
			headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
			body: JSON.stringify({ accessToken: token, responseNote })
		}
	), 'Record estimate decision');
