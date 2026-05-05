import { error, type RequestHandler } from '@sveltejs/kit';
import { readQuoteRequestAttachment } from '$lib/server/quote-request-attachments';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const GET: RequestHandler = async ({ fetch, locals, params }) => {
	if (!locals.bdrAdminSession) {
		throw error(403, 'Admin attachment access requires owner or office admin privileges.');
	}

	const { requests } = await loadQuoteRequests(fetch);
	const request = requests.find((item) => item.id === params.requestId);
	const attachment = request?.attachments.find((item) => item.id === params.attachmentId);

	if (!request || !attachment) {
		throw error(404, 'Attachment was not found.');
	}

	const bytes = await readQuoteRequestAttachment(attachment);
	if (!bytes) {
		throw error(404, 'Attachment content was not found.');
	}

	const encodedFileName = encodeURIComponent(attachment.fileName);
	const body = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;

	return new Response(body, {
		headers: {
			'Content-Disposition': `inline; filename*=UTF-8''${encodedFileName}`,
			'Content-Length': String(attachment.sizeBytes),
			'Content-Type': attachment.contentType || 'application/octet-stream',
			'X-Content-Type-Options': 'nosniff'
		}
	});
};
