import { bdrSiteContent, type BdrSiteContent } from '$lib/bdr-site-content';

const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const localStoreDir = `${getCwd()}/.svelte-kit`;
const localStorePath = `${localStoreDir}/local-bdr-site-content.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const cloneDefaultContent = (): BdrSiteContent => structuredClone(bdrSiteContent);

const normalizeServices = (items: unknown): string[] | null => {
	if (!Array.isArray(items)) return null;

	const services = items
		.map((item) => String(item ?? '').trim())
		.filter(Boolean);

	return services;
};

const normalizeContent = (value: unknown): BdrSiteContent => {
	const content = cloneDefaultContent();

	if (!value || typeof value !== 'object') {
		return content;
	}

	const candidate = value as Partial<BdrSiteContent>;
	const services = normalizeServices(candidate.services?.items);

	if (services) {
		content.services.items = services;
	}

	return content;
};

export const loadBdrSiteContent = async (): Promise<BdrSiteContent> => {
	try {
		const fs = await getFs();
		const contents = await fs.readFile(localStorePath, 'utf-8');
		return normalizeContent(JSON.parse(contents) as unknown);
	} catch (cause) {
		if (cause && typeof cause === 'object' && 'code' in cause && cause.code !== 'ENOENT') {
			console.warn('Unable to read local BDR site content store.', cause);
		}

		return cloneDefaultContent();
	}
};

export const saveBdrServices = async (items: string[]): Promise<BdrSiteContent> => {
	const services = normalizeServices(items) ?? [];
	const content = await loadBdrSiteContent();
	content.services.items = services;

	const fs = await getFs();
	await fs.mkdir(localStoreDir, { recursive: true });
	await fs.writeFile(localStorePath, JSON.stringify(content, null, 2));

	return content;
};
