import { error, type RequestHandler } from '@sveltejs/kit';
import { downloadQuoteRequestAttachment } from '$lib/server/quote-request-attachments';
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

	const response = await downloadQuoteRequestAttachment(fetch, params.requestId!, params.attachmentId!);
	if (!response.ok || !response.body) {
		throw error(404, 'Attachment content was not found.');
	}

	const encodedFileName = encodeURIComponent(attachment.fileName);

	return new Response(response.body, {
		headers: {
			'Content-Disposition': `inline; filename*=UTF-8''${encodedFileName}`,
			'Content-Length': String(attachment.sizeBytes),
			'Content-Type': attachment.contentType || 'application/octet-stream',
			'X-Content-Type-Options': 'nosniff'
		}
	});
};
