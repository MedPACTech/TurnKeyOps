import {
	applyBdrContractorPresetToContent,
	bdrSiteContent,
	type BdrAsset,
	type BdrContractorPreset,
	type ContentLink,
	type BdrServiceCategory,
	type BdrSiteContent,
	type BdrThemeSettings
} from '$lib/bdr-site-content';

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

const normalizeLinks = (items: unknown): ContentLink[] | null => {
	if (!Array.isArray(items)) return null;

	return items
		.map((item): ContentLink | null => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<ContentLink>;
			if (!candidate.label || !candidate.href) return null;

			return {
				label: String(candidate.label),
				href: String(candidate.href),
				openInNewTab: Boolean(candidate.openInNewTab)
			} satisfies ContentLink;
		})
		.filter((link): link is ContentLink => link !== null);
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

const normalizeContractorPresets = (items: unknown): BdrContractorPreset[] | null => {
	if (!Array.isArray(items)) return null;

	const presets = items
		.map((item): BdrContractorPreset | null => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<BdrContractorPreset>;
			if (
				!candidate.id ||
				!candidate.label ||
				!candidate.contractorType ||
				!candidate.defaultHeroHeadline ||
				!Array.isArray(candidate.defaultServices)
			) {
				return null;
			}

			const defaultServices = candidate.defaultServices
				.map((service): BdrContractorPreset['defaultServices'][number] | null => {
					if (!service || typeof service !== 'object') return null;
					const entry = service as BdrContractorPreset['defaultServices'][number];
					if (!entry.name || !entry.slug || !entry.description || !entry.iconAssetKey) {
						return null;
					}

					return {
						name: String(entry.name),
						slug: String(entry.slug),
						description: String(entry.description),
						iconAssetKey: String(entry.iconAssetKey),
						imageAssetKey: entry.imageAssetKey ? String(entry.imageAssetKey) : undefined
					};
				})
				.filter(
					(
						service
					): service is BdrContractorPreset['defaultServices'][number] => service !== null
				);

			return {
				id: String(candidate.id),
				label: String(candidate.label),
				contractorType: String(candidate.contractorType),
				defaultHeroHeadline: String(candidate.defaultHeroHeadline),
				defaultServices,
				defaultIconAssetKeys: Array.isArray(candidate.defaultIconAssetKeys)
					? candidate.defaultIconAssetKeys.map((icon) => String(icon)).filter(Boolean)
					: []
			};
		})
		.filter((preset): preset is BdrContractorPreset => preset !== null);

	return presets;
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

const normalizeNavigation = (
	value: unknown,
	fallback: BdrSiteContent['navigation']
): BdrSiteContent['navigation'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['navigation']>;
	return {
		announcement: String(candidate.announcement ?? fallback.announcement),
		brandName: String(candidate.brandName ?? fallback.brandName),
		logoAssetKey: String(candidate.logoAssetKey ?? fallback.logoAssetKey),
		faviconAssetKey: String(candidate.faviconAssetKey ?? fallback.faviconAssetKey),
		links: normalizeLinks(candidate.links) ?? fallback.links,
		primaryCtaLabel: String(candidate.primaryCtaLabel ?? fallback.primaryCtaLabel),
		primaryCtaHref: String(candidate.primaryCtaHref ?? fallback.primaryCtaHref),
		phoneNumber: String(candidate.phoneNumber ?? fallback.phoneNumber),
		showPhoneButton:
			typeof candidate.showPhoneButton === 'boolean'
				? candidate.showPhoneButton
				: fallback.showPhoneButton,
		showThemeControl:
			typeof candidate.showThemeControl === 'boolean'
				? candidate.showThemeControl
				: fallback.showThemeControl,
		stickyHeader:
			typeof candidate.stickyHeader === 'boolean'
				? candidate.stickyHeader
				: fallback.stickyHeader,
		layout:
			candidate.layout === 'centered' ||
			candidate.layout === 'right-aligned' ||
			candidate.layout === 'logo-left'
				? candidate.layout
				: fallback.layout
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
	const contractorPresets = normalizeContractorPresets(candidate.contractorPresets);
	const themeSettings = normalizeThemeSettings(candidate.themeSettings, content.themeSettings);
	const navigation = normalizeNavigation(candidate.navigation, content.navigation);

	if (services) {
		content.services.items = services;
	}

	if (assetLibrary) {
		content.assetLibrary = assetLibrary;
	}

	if (serviceCategories) {
		content.serviceCategories = serviceCategories;
	}

	if (contractorPresets?.length) {
		content.contractorPresets = contractorPresets;
	}

	if (
		typeof candidate.activeContractorPresetId === 'string' &&
		content.contractorPresets.some((preset) => preset.id === candidate.activeContractorPresetId)
	) {
		content.activeContractorPresetId = candidate.activeContractorPresetId;
	}

	content.themeSettings = themeSettings;
	content.navigation = navigation;

	return content;
};

const writeBdrSiteContent = async (content: BdrSiteContent): Promise<BdrSiteContent> => {
	const fs = await getFs();
	await fs.mkdir(localStoreDir, { recursive: true });
	await fs.writeFile(localStorePath, JSON.stringify(content, null, 2));

	return content;
};

export const updateBdrSiteContent = async (
	updater: (content: BdrSiteContent) => void
): Promise<BdrSiteContent> => {
	const content = await loadBdrSiteContent();
	updater(content);
	return writeBdrSiteContent(content);
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
	return updateBdrSiteContent((content) => {
		content.services.items = services;
	});
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

export const applyBdrContractorPresetSelection = async (
	presetId: string
): Promise<BdrSiteContent> => {
	return updateBdrSiteContent((content) => {
		const appliedPreset = applyBdrContractorPresetToContent(content, presetId);

		if (!appliedPreset) {
			throw new Error(`Unknown contractor preset: ${presetId}`);
		}
	});
};
