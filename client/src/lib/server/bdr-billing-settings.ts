const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const localStoreDir = `${getCwd()}/.svelte-kit`;
const localStorePath = `${localStoreDir}/local-bdr-billing-settings.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

export type BdrBillingSettings = {
	depositPercentRequired: number;
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

export const defaultBdrBillingSettings: BdrBillingSettings = {
	depositPercentRequired: 50
};

const normalizeBillingSettings = (value: unknown): BdrBillingSettings => {
	if (!value || typeof value !== 'object') return { ...defaultBdrBillingSettings };
	const rawPercent = Number((value as Partial<BdrBillingSettings>).depositPercentRequired);
	return {
		depositPercentRequired: Number.isFinite(rawPercent) ? Math.min(100, Math.max(0, rawPercent)) : defaultBdrBillingSettings.depositPercentRequired
	};
};

const writeBdrBillingSettings = async (settings: BdrBillingSettings): Promise<BdrBillingSettings> => {
	const normalized = normalizeBillingSettings(settings);
	const fs = await getFs();
	await fs.mkdir(localStoreDir, { recursive: true });
	await fs.writeFile(localStorePath, JSON.stringify(normalized, null, 2));
	return normalized;
};

export const loadBdrBillingSettings = async (): Promise<BdrBillingSettings> => {
	try {
		const fs = await getFs();
		const contents = await fs.readFile(localStorePath, 'utf-8');
		return normalizeBillingSettings(JSON.parse(contents) as unknown);
	} catch (cause) {
		if (cause && typeof cause === 'object' && 'code' in cause && cause.code !== 'ENOENT') {
			console.warn('Unable to read BDR billing settings store.', cause);
		}

		return normalizeBillingSettings(defaultBdrBillingSettings);
	}
};

export const saveBdrBillingSettings = async (value: unknown): Promise<BdrBillingSettings> =>
	writeBdrBillingSettings(normalizeBillingSettings(value));
