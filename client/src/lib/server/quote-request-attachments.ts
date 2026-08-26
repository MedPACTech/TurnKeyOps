import { error } from '@sveltejs/kit';
import { getTenantById } from '$lib/config/tenants';
import type { QuoteRequestAttachment } from '$lib/quote-requests';
import {
	getTurnKeyApiBaseUrl,
	getTurnKeyApiHeaders,
	unwrapTurnKeyApiEnvelope
} from '$lib/server/turnkey-api';

export const uploadQuoteRequestAttachments = async (
	fetch: typeof globalThis.fetch,
	tenantId: string,
	quoteRequestId: string,
	files: File[]
): Promise<QuoteRequestAttachment[]> => {
	if (!files.length) return [];

	const tenantSlug = getTenantById(tenantId)?.slug;
	if (!tenantSlug) throw error(400, 'Quote request tenant is not configured.');

	const formData = new FormData();
	for (const file of files) formData.append('files', file, file.name);

	const response = await fetch(
		`${getTurnKeyApiBaseUrl()}/api/public/quote-requests/${tenantSlug}/${quoteRequestId}/attachments`,
		{
			method: 'POST',
			headers: getTurnKeyApiHeaders(false),
			body: formData
		}
	);

	return unwrapTurnKeyApiEnvelope<QuoteRequestAttachment[]>(response, 'Quote attachment upload');
};

export const downloadQuoteRequestAttachment = async (
	fetch: typeof globalThis.fetch,
	quoteRequestId: string,
	attachmentId: string
) =>
	fetch(
		`${getTurnKeyApiBaseUrl()}/api/quote-requests/${quoteRequestId}/attachments/${attachmentId}`,
		{ headers: getTurnKeyApiHeaders(false) }
	);
