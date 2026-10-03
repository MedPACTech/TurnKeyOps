import { error, json } from '@sveltejs/kit';
import { acceptedPhotoTypes, maximumPhotoBytes } from '$lib/locksmith-offline';
import { fieldApi, fieldResponse, requireFieldOrigin } from '../field-api.server';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async (event) => {
	requireFieldOrigin(event);
	const { call, context } = await fieldApi(event);
	const data = await event.request.formData();
	const id = String(data.get('requestId') ?? '');
	if (!/^[0-9a-f-]{36}$/i.test(id)) throw error(400, 'A valid field request reference is required.');
	const record = await fieldResponse<{ propertyType: string }>(await call(`/api/quote-requests/${id}`));
	if (!context.capabilities.some((jobType) => jobType === record.propertyType)) throw error(403, 'This request type is not enabled for your account.');
	const files = data.getAll('files').filter((item): item is File => item instanceof File);
	if (!files.length || files.length > 10 || files.some((file) => !acceptedPhotoTypes.includes(file.type) || file.size > maximumPhotoBytes) || files.reduce((sum, file) => sum + file.size, 0) > 40 * 1024 * 1024) throw error(400, 'Upload up to 10 photos per batch, no more than 10 MB each or 40 MB combined.');
	const payload = new FormData(); files.forEach((file) => payload.append('files', file, file.name));
	const attachments = await fieldResponse<unknown[]>(await call(`/api/quote-requests/${id}/attachments`, { method: 'POST', body: payload }));
	return json({ uploaded: attachments.length }, { headers: { 'Cache-Control': 'private, no-store' } });
};
