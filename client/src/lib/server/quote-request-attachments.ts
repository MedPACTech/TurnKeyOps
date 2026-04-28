import type { QuoteRequestAttachment } from '$lib/quote-requests';

const blobContainer = 'quote-request-attachments';
const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const localBlobRoot = `${getCwd()}/.svelte-kit/blob-storage`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string) => Promise<Uint8Array>;
	writeFile: (path: string, data: Uint8Array) => Promise<unknown>;
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const toSafeSegment = (value: string) =>
	value
		.trim()
		.replace(/[^a-zA-Z0-9._=-]+/g, '-')
		.replace(/^-+|-+$/g, '')
		.slice(0, 120) || 'file';

const buildBlobName = (tenantId: string, quoteRequestId: string, attachmentId: string, fileName: string) =>
	`TENANT=${toSafeSegment(tenantId)}/QUOTE=${toSafeSegment(quoteRequestId)}/${toSafeSegment(attachmentId)}-${toSafeSegment(fileName)}`;

export const uploadQuoteRequestAttachments = async (
	tenantId: string,
	quoteRequestId: string,
	files: File[]
): Promise<QuoteRequestAttachment[]> => {
	if (!files.length) return [];

	const fs = await getFs();
	const uploadedAtUtc = new Date().toISOString();
	const attachments: QuoteRequestAttachment[] = [];

	for (const file of files) {
		const id = crypto.randomUUID();
		const blobName = buildBlobName(tenantId, quoteRequestId, id, file.name);
		const bytes = new Uint8Array(await file.arrayBuffer());

		await fs.mkdir(`${localBlobRoot}/${blobContainer}/${blobName.split('/').slice(0, -1).join('/')}`, {
			recursive: true
		});
		await fs.writeFile(`${localBlobRoot}/${blobContainer}/${blobName}`, bytes);

		attachments.push({
			id,
			fileName: file.name,
			contentType: file.type || 'application/octet-stream',
			sizeBytes: file.size,
			uploadedAtUtc,
			tenantId,
			blobContainer,
			blobName,
			blobUrl: `local-blob://${blobContainer}/${blobName}`
		});
	}

	return attachments;
};

export const readQuoteRequestAttachment = async (attachment: QuoteRequestAttachment) => {
	if (!attachment.blobName || attachment.blobContainer !== blobContainer) {
		return null;
	}

	const segments = attachment.blobName.split('/');
	if (!segments.length || segments.some((segment) => !segment || segment === '..' || segment.includes('\\'))) {
		return null;
	}

	const fs = await getFs();
	return fs.readFile(`${localBlobRoot}/${blobContainer}/${segments.join('/')}`);
};
