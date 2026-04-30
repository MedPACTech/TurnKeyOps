import { bdrSiteContent, type BdrAsset, type BdrServiceCategory, type BdrSiteContent, type BdrThemeSettings } from '$lib/bdr-site-content';

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

const normalizeAssetLibrary = (items: unknown): BdrAsset[] | null => {
	if (!Array.isArray(items)) return null;

	const assets = items
		.map((item) => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<BdrAsset>;
			if (!candidate.key || !candidate.name || !candidate.type || !candidate.file) return null;

			return {
				key: String(candidate.key),
				name: String(candidate.name),
				type: candidate.type,
				file: String(candidate.file),
				altText: String(candidate.altText ?? ''),
				contractorCategory: String(candidate.contractorCategory ?? 'shared'),
				tags: Array.isArray(candidate.tags) ? candidate.tags.map((tag) => String(tag)).filter(Boolean) : [],
				sortOrder: Number.isFinite(candidate.sortOrder) ? Number(candidate.sortOrder) : 0
			} satisfies BdrAsset;
		})
		.filter((asset): asset is BdrAsset => Boolean(asset));

	return assets;
};

const normalizeServiceCategories = (items: unknown): BdrServiceCategory[] | null => {
	if (!Array.isArray(items)) return null;

	const categories = items
		.map((item) => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<BdrServiceCategory>;
			if (!candidate.name || !candidate.slug || !candidate.description || !candidate.iconAssetKey) return null;

			return {
				name: String(candidate.name),
				slug: String(candidate.slug),
				description: String(candidate.description),
				iconAssetKey: String(candidate.iconAssetKey),
				imageAssetKey: candidate.imageAssetKey ? String(candidate.imageAssetKey) : undefined,
				contractorType: String(candidate.contractorType ?? 'shared'),
				featured: Boolean(candidate.featured),
				sortOrder: Number.isFinite(candidate.sortOrder) ? Number(candidate.sortOrder) : 0
			} satisfies BdrServiceCategory;
		})
		.filter(Boolean) as BdrServiceCategory[];

	return categories;
};

const normalizeThemeSettings = (value: unknown, fallback: BdrThemeSettings): BdrThemeSettings => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrThemeSettings>;
	return {
		mode:
			candidate.mode === 'Light' || candidate.mode === 'Dark' || candidate.mode === 'System'
				? candidate.mode
				: fallback.mode,
		preset:
			candidate.preset === 'Clean' ||
			candidate.preset === 'Industrial' ||
			candidate.preset === 'Premium' ||
			candidate.preset === 'Minimal' ||
			candidate.preset === 'Bold'
				? candidate.preset
				: fallback.preset,
		colors: {
			primary: String(candidate.colors?.primary ?? fallback.colors.primary),
			secondary: String(candidate.colors?.secondary ?? fallback.colors.secondary),
			accent: String(candidate.colors?.accent ?? fallback.colors.accent),
			background: String(candidate.colors?.background ?? fallback.colors.background),
			surface: String(candidate.colors?.surface ?? fallback.colors.surface),
			text: String(candidate.colors?.text ?? fallback.colors.text),
			border: String(candidate.colors?.border ?? fallback.colors.border)
		},
		typography: {
			headingFont: String(candidate.typography?.headingFont ?? fallback.typography.headingFont),
			bodyFont: String(candidate.typography?.bodyFont ?? fallback.typography.bodyFont)
		},
		sizing: {
			buttonRadius: String(candidate.sizing?.buttonRadius ?? fallback.sizing.buttonRadius),
			cardRadius: String(candidate.sizing?.cardRadius ?? fallback.sizing.cardRadius),
			logoSize: String(candidate.sizing?.logoSize ?? fallback.sizing.logoSize)
		},
		iconStyle: String(candidate.iconStyle ?? fallback.iconStyle),
		brandAssets: {
			logoAssetKey: String(candidate.brandAssets?.logoAssetKey ?? fallback.brandAssets.logoAssetKey),
			faviconAssetKey: String(candidate.brandAssets?.faviconAssetKey ?? fallback.brandAssets.faviconAssetKey)
		}
	};
};

const normalizeContent = (value: unknown): BdrSiteContent => {
	const content = cloneDefaultContent();

	if (!value || typeof value !== 'object') {
		return content;
	}

	const candidate = value as Partial<BdrSiteContent>;
	const services = normalizeServices(candidate.services?.items);
	const assetLibrary = normalizeAssetLibrary(candidate.assetLibrary);
	const serviceCategories = normalizeServiceCategories(candidate.serviceCategories);
	const themeSettings = normalizeThemeSettings(candidate.themeSettings, content.themeSettings);

	if (services) {
		content.services.items = services;
	}

	if (assetLibrary) {
		content.assetLibrary = assetLibrary;
	}

	if (serviceCategories) {
		content.serviceCategories = serviceCategories;
	}

	content.themeSettings = themeSettings;

	return content;
};

const writeBdrSiteContent = async (content: BdrSiteContent): Promise<BdrSiteContent> => {
	const fs = await getFs();
	await fs.mkdir(localStoreDir, { recursive: true });
	await fs.writeFile(localStorePath, JSON.stringify(content, null, 2));

	return content;
};

type BdrThemeSettingsPatch = {
	mode?: BdrThemeSettings['mode'];
	preset?: BdrThemeSettings['preset'];
	colors?: Partial<BdrThemeSettings['colors']>;
	typography?: Partial<BdrThemeSettings['typography']>;
	sizing?: Partial<BdrThemeSettings['sizing']>;
	iconStyle?: string;
	brandAssets?: Partial<BdrThemeSettings['brandAssets']>;
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

	return writeBdrSiteContent(content);
};

export const saveBdrThemeSettings = async (
	value: BdrThemeSettingsPatch
): Promise<BdrSiteContent> => {
	const content = await loadBdrSiteContent();
	content.themeSettings = normalizeThemeSettings(
		{
			...content.themeSettings,
			...value,
			colors: {
				...content.themeSettings.colors,
				...value.colors
			},
			typography: {
				...content.themeSettings.typography,
				...value.typography
			},
			sizing: {
				...content.themeSettings.sizing,
				...value.sizing
			},
			brandAssets: {
				...content.themeSettings.brandAssets,
				...value.brandAssets
			}
		},
		content.themeSettings
	);

	return writeBdrSiteContent(content);
};
