import { error } from '@sveltejs/kit';
import { getApiBaseUrl } from '$lib/api/client';
import { fetchIntake, sessionToken } from '../../../../records.server';
export const GET = async (event) => {
 const token = sessionToken(event);
 const record = (await fetchIntake(event.fetch, token)).find(item => item.id === event.params.requestId);
 const attachment = record?.attachments.find(item => item.id === event.params.attachmentId);
 if (!attachment) throw error(404, 'Attachment was not found.');
 const response = await event.fetch(`${getApiBaseUrl()}/api/quote-requests/${encodeURIComponent(record!.id)}/attachments/${encodeURIComponent(attachment.id)}`, { headers: { Authorization: `Bearer ${token}` } });
 if (!response.ok || !response.body) throw error(response.status === 403 ? 403 : 404, 'Attachment could not be downloaded.');
 return new Response(response.body, { headers: { 'Content-Type': attachment.contentType || 'application/octet-stream', 'Content-Disposition': `attachment; filename*=UTF-8''${encodeURIComponent(attachment.fileName)}`, 'Cache-Control': 'private, no-store', 'X-Content-Type-Options': 'nosniff' } });
};
