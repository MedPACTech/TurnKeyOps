import {
	applyBdrContractorPresetToContent,
	bdrSiteContent,
	type BdrAsset,
	type BdrCtaType,
	type BdrContractorPreset,
	type BdrHeroMediaOverride,
	type BdrHeroTrustBadge,
	type BdrQuoteFormBenefit,
	type BdrQuoteFormField,
	type BdrQuoteFormFieldType,
	type BdrSocialLink,
	type ContentLink,
	type BdrServiceCategory,
	type BdrSiteContent,
	type BdrThemeSettings
} from '$lib/bdr-site-content';
import { getPublicTenantContent, updateTenantSettings } from '$lib/api/tenant-settings';
import { bdrTenant } from '$lib/config/tenants';

export type TenantSettingsApiContext = {
	fetcher?: typeof globalThis.fetch;
	accessToken?: string | null;
};

const cloneDefaultContent = (): BdrSiteContent => structuredClone(bdrSiteContent);
const legacyPublicHeroHeadline =
	'Roofing and exterior work with a clear path from inspection to quote, schedule, and completion.';

const isLegacyPublicSiteDraft = (value: Partial<BdrSiteContent>): boolean => {
	const headline = value.hero?.headline;
	const announcement = value.navigation?.announcement;

	return headline === legacyPublicHeroHeadline && (!announcement || announcement === 'Navigation');
};

const normalizeServices = (items: unknown): string[] | null => {
	if (!Array.isArray(items)) return null;

	const services = items
		.map((item) => String(item ?? '').trim())
		.filter(Boolean);

	return services;
};

const normalizeCtaType = (value: unknown, fallback: BdrCtaType): BdrCtaType =>
	value === 'phone' || value === 'link' || value === 'anchor' ? value : fallback;

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
				detailPageUrl: candidate.detailPageUrl ? String(candidate.detailPageUrl) : undefined,
				contractorType: String(candidate.contractorType ?? 'shared'),
				featured: Boolean(candidate.featured),
				sortOrder: Number.isFinite(candidate.sortOrder) ? Number(candidate.sortOrder) : 0
			} satisfies BdrServiceCategory;
		})
		.filter(Boolean) as BdrServiceCategory[];

	return categories;
};

const normalizeServicesSection = (
	value: unknown,
	fallback: BdrSiteContent['services']
): BdrSiteContent['services'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['services']>;
	return {
		eyebrow: String(candidate.eyebrow ?? fallback.eyebrow),
		title: String(candidate.title ?? fallback.title),
		copy: String(candidate.copy ?? fallback.copy),
		items: normalizeServices(candidate.items) ?? fallback.items,
		ctaLabel: String(candidate.ctaLabel ?? fallback.ctaLabel),
		ctaHref: String(candidate.ctaHref ?? fallback.ctaHref)
	};
};

const normalizeHeroTrustBadges = (items: unknown): BdrHeroTrustBadge[] | null => {
	if (!Array.isArray(items)) return null;

	const badges = items
		.map((item): BdrHeroTrustBadge | null => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<BdrHeroTrustBadge>;
			if (!candidate.iconAssetKey || !candidate.title || !candidate.description) return null;

			return {
				iconAssetKey: String(candidate.iconAssetKey),
				title: String(candidate.title),
				description: String(candidate.description)
			};
		})
		.filter((badge): badge is BdrHeroTrustBadge => badge !== null);

	return badges;
};

const normalizeHeroMediaOverrides = (items: unknown): BdrHeroMediaOverride[] | null => {
	if (!Array.isArray(items)) return null;

	const overrides = items
		.map((item): BdrHeroMediaOverride | null => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<BdrHeroMediaOverride>;
			if (!candidate.contractorType || !candidate.heroImageAssetKey) return null;

			return {
				contractorType: String(candidate.contractorType),
				heroImageAssetKey: String(candidate.heroImageAssetKey),
				backgroundImageAssetKey: candidate.backgroundImageAssetKey
					? String(candidate.backgroundImageAssetKey)
					: undefined,
				backgroundTextureAssetKey: candidate.backgroundTextureAssetKey
					? String(candidate.backgroundTextureAssetKey)
					: undefined,
				heroImageAltText: candidate.heroImageAltText ? String(candidate.heroImageAltText) : undefined
			};
		})
		.filter((override): override is BdrHeroMediaOverride => override !== null);

	return overrides;
};

const normalizeSocialLinks = (items: unknown): BdrSocialLink[] | null => {
	if (!Array.isArray(items)) return null;

	const links = items
		.map((item): BdrSocialLink | null => {
			if (!item || typeof item !== 'object') return null;
			const candidate = item as Partial<BdrSocialLink>;
			if (!candidate.platform || !candidate.url || !candidate.iconAssetKey) return null;

			return {
				platform: String(candidate.platform),
				url: String(candidate.url),
				iconAssetKey: String(candidate.iconAssetKey)
			};
		})
		.filter((link): link is BdrSocialLink => link !== null);

	return links;
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

const normalizeHero = (
	value: unknown,
	fallback: BdrSiteContent['hero']
): BdrSiteContent['hero'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['hero']> & {
		body?: string;
		proofEyebrow?: string;
	};

	return {
		eyebrow: String(candidate.eyebrow ?? fallback.eyebrow),
		headline: String(candidate.headline ?? fallback.headline),
		subheadline: String(candidate.subheadline ?? candidate.body ?? fallback.subheadline),
		primaryCtaLabel: String(candidate.primaryCtaLabel ?? fallback.primaryCtaLabel),
		primaryCtaHref: String(candidate.primaryCtaHref ?? fallback.primaryCtaHref),
		primaryCtaType: normalizeCtaType(candidate.primaryCtaType, fallback.primaryCtaType),
		secondaryCtaLabel: String(candidate.secondaryCtaLabel ?? fallback.secondaryCtaLabel),
		secondaryCtaHref: String(candidate.secondaryCtaHref ?? fallback.secondaryCtaHref),
		secondaryCtaType: normalizeCtaType(candidate.secondaryCtaType, fallback.secondaryCtaType),
		heroImageAssetKey: String(candidate.heroImageAssetKey ?? fallback.heroImageAssetKey),
		heroImageAltText: String(candidate.heroImageAltText ?? fallback.heroImageAltText),
		backgroundImageAssetKey: String(
			candidate.backgroundImageAssetKey ?? fallback.backgroundImageAssetKey
		),
		backgroundTextureAssetKey: String(
			candidate.backgroundTextureAssetKey ?? fallback.backgroundTextureAssetKey
		),
		trustBadgeEyebrow: String(
			candidate.trustBadgeEyebrow ?? candidate.proofEyebrow ?? fallback.trustBadgeEyebrow
		),
		trustBadges: normalizeHeroTrustBadges(candidate.trustBadges) ?? fallback.trustBadges,
		mediaByContractorType:
			normalizeHeroMediaOverrides(candidate.mediaByContractorType) ?? fallback.mediaByContractorType
	};
};

const normalizeProcess = (
	value: unknown,
	fallback: BdrSiteContent['process']
): BdrSiteContent['process'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['process']>;
	const steps = Array.isArray(candidate.steps)
		? candidate.steps
				.map((item): BdrSiteContent['process']['steps'][number] | null => {
					if (!item || typeof item !== 'object') return null;
					const entry = item as Partial<BdrSiteContent['process']['steps'][number]>;
					if (!entry.step || !entry.title || !entry.copy || !entry.iconAssetKey) return null;

					return {
						step: String(entry.step),
						title: String(entry.title),
						copy: String(entry.copy),
						iconAssetKey: String(entry.iconAssetKey),
						timeframe: entry.timeframe ? String(entry.timeframe) : undefined
					};
				})
				.filter(
					(step): step is BdrSiteContent['process']['steps'][number] => step !== null
				)
		: fallback.steps;

	return {
		eyebrow: String(candidate.eyebrow ?? fallback.eyebrow),
		title: String(candidate.title ?? fallback.title),
		description: String(candidate.description ?? fallback.description),
		steps
	};
};

const normalizeQuoteFormFieldType = (
	value: unknown,
	fallback: BdrQuoteFormFieldType
): BdrQuoteFormFieldType =>
	value === 'text' ||
	value === 'email' ||
	value === 'tel' ||
	value === 'textarea' ||
	value === 'select' ||
	value === 'file'
		? value
		: fallback;

const normalizeQuoteForm = (
	value: unknown,
	fallback: BdrSiteContent['quoteForm']
): BdrSiteContent['quoteForm'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['quoteForm']>;
	const benefits = Array.isArray(candidate.benefits)
		? candidate.benefits
				.map((item): BdrQuoteFormBenefit | null => {
					if (!item || typeof item !== 'object') return null;
					const entry = item as Partial<BdrQuoteFormBenefit>;
					if (!entry.iconAssetKey || !entry.text) return null;

					return {
						iconAssetKey: String(entry.iconAssetKey),
						text: String(entry.text)
					};
				})
				.filter((item): item is BdrQuoteFormBenefit => item !== null)
		: fallback.benefits;
	const fields = Array.isArray(candidate.fields)
		? candidate.fields
				.map((item): BdrQuoteFormField | null => {
					if (!item || typeof item !== 'object') return null;
					const entry = item as Partial<BdrQuoteFormField>;
					if (!entry.key || !entry.label || !entry.type) return null;

					return {
						key: String(entry.key),
						label: String(entry.label),
						type: normalizeQuoteFormFieldType(entry.type, 'text'),
						placeholder: entry.placeholder ? String(entry.placeholder) : undefined,
						required:
							typeof entry.required === 'boolean'
								? entry.required
								: fallback.fields.find((field) => field.key === entry.key)?.required ?? false,
						options: Array.isArray(entry.options)
							? entry.options.map((option) => String(option)).filter(Boolean)
							: []
					};
				})
				.filter((item): item is BdrQuoteFormField => item !== null)
		: fallback.fields;
	const notificationRecipients = Array.isArray(candidate.notificationRecipients)
		? candidate.notificationRecipients.map((item) => String(item).trim()).filter(Boolean)
		: fallback.notificationRecipients;

	return {
		eyebrow: String(candidate.eyebrow ?? fallback.eyebrow),
		title: String(candidate.title ?? fallback.title),
		description: String(candidate.description ?? fallback.description),
		privacyReassurance: String(
			candidate.privacyReassurance ?? fallback.privacyReassurance
		),
		benefits,
		fields,
		submitButtonLabel: String(candidate.submitButtonLabel ?? fallback.submitButtonLabel),
		successMessage: String(candidate.successMessage ?? fallback.successMessage),
		notificationRecipients,
		queueDestination: String(candidate.queueDestination ?? fallback.queueDestination)
	};
};

const normalizeCtaBanner = (
	value: unknown,
	fallback: BdrSiteContent['ctaBanner']
): BdrSiteContent['ctaBanner'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['ctaBanner']>;
	const overlayOpacity = Number(candidate.overlayOpacity);

	return {
		eyebrow: String(candidate.eyebrow ?? fallback.eyebrow),
		title: String(candidate.title ?? fallback.title),
		description: String(candidate.description ?? fallback.description),
		backgroundImageAssetKey: String(
			candidate.backgroundImageAssetKey ?? fallback.backgroundImageAssetKey
		),
		backgroundImageAltText: String(
			candidate.backgroundImageAltText ?? fallback.backgroundImageAltText
		),
		overlayOpacity:
			Number.isFinite(overlayOpacity) && overlayOpacity >= 0 && overlayOpacity <= 1
				? overlayOpacity
				: fallback.overlayOpacity,
		primaryCtaLabel: String(candidate.primaryCtaLabel ?? fallback.primaryCtaLabel),
		primaryCtaHref: String(candidate.primaryCtaHref ?? fallback.primaryCtaHref),
		secondaryCtaLabel: String(candidate.secondaryCtaLabel ?? fallback.secondaryCtaLabel),
		secondaryCtaType: normalizeCtaType(candidate.secondaryCtaType, fallback.secondaryCtaType),
		secondaryCtaHref: String(candidate.secondaryCtaHref ?? fallback.secondaryCtaHref)
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

const normalizeFooter = (
	value: unknown,
	fallback: BdrSiteContent['footer']
): BdrSiteContent['footer'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['footer']> & {
		linksEyebrow?: string;
		links?: ContentLink[];
	};

	return {
		eyebrow: String(candidate.eyebrow ?? fallback.eyebrow),
		logoAssetKey: String(candidate.logoAssetKey ?? fallback.logoAssetKey),
		brandName: String(candidate.brandName ?? fallback.brandName),
		body: String(candidate.body ?? fallback.body),
		serviceAreaText: String(candidate.serviceAreaText ?? fallback.serviceAreaText),
		navigationEyebrow: String(candidate.navigationEyebrow ?? candidate.linksEyebrow ?? fallback.navigationEyebrow),
		navigationLinks:
			normalizeLinks(candidate.navigationLinks ?? candidate.links) ?? fallback.navigationLinks,
		servicesEyebrow: String(candidate.servicesEyebrow ?? fallback.servicesEyebrow),
		servicesLinks: normalizeLinks(candidate.servicesLinks) ?? fallback.servicesLinks,
		contactEyebrow: String(candidate.contactEyebrow ?? fallback.contactEyebrow),
		phone: String(candidate.phone ?? fallback.phone),
		email: String(candidate.email ?? fallback.email),
		address: String(candidate.address ?? fallback.address),
		socialLinks: normalizeSocialLinks(candidate.socialLinks) ?? fallback.socialLinks
	};
};

const normalizePostFooter = (
	value: unknown,
	fallback: BdrSiteContent['postFooter']
): BdrSiteContent['postFooter'] => {
	if (!value || typeof value !== 'object') return fallback;

	const candidate = value as Partial<BdrSiteContent['postFooter']> & {
		utilityLinks?: ContentLink[];
	};

	return {
		legalLinksEyebrow: String(candidate.legalLinksEyebrow ?? fallback.legalLinksEyebrow),
		legalLinks: normalizeLinks(candidate.legalLinks ?? candidate.utilityLinks) ?? fallback.legalLinks,
		copyright: String(candidate.copyright ?? fallback.copyright)
	};
};

const normalizeContent = (value: unknown): BdrSiteContent => {
	const content = cloneDefaultContent();

	if (!value || typeof value !== 'object') {
		return content;
	}

	const candidate = value as Partial<BdrSiteContent>;
	if (isLegacyPublicSiteDraft(candidate)) {
		return content;
	}

	const services = normalizeServicesSection(candidate.services, content.services);
	const assetLibrary = normalizeAssetLibrary(candidate.assetLibrary);
	const serviceCategories = normalizeServiceCategories(candidate.serviceCategories);
	const contractorPresets = normalizeContractorPresets(candidate.contractorPresets);
	const themeSettings = normalizeThemeSettings(candidate.themeSettings, content.themeSettings);
	const navigation = normalizeNavigation(candidate.navigation, content.navigation);
	const hero = normalizeHero(candidate.hero, content.hero);
	const process = normalizeProcess(candidate.process, content.process);
	const ctaBanner = normalizeCtaBanner(candidate.ctaBanner, content.ctaBanner);
	const quoteForm = normalizeQuoteForm(candidate.quoteForm, content.quoteForm);
	const footer = normalizeFooter(candidate.footer, content.footer);
	const postFooter = normalizePostFooter(candidate.postFooter, content.postFooter);

	content.services = services;

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
	content.hero = hero;
	content.process = process;
	content.ctaBanner = ctaBanner;
	content.quoteForm = quoteForm;
	content.footer = footer;
	content.postFooter = postFooter;

	return content;
};

const loadBdrSiteContentDocument = (fetcher: typeof globalThis.fetch = globalThis.fetch) =>
	getPublicTenantContent<BdrSiteContent>(bdrTenant.id, fetcher);

const writeBdrSiteContent = async (
	content: BdrSiteContent,
	expectedVersion: string | null | undefined,
	context: TenantSettingsApiContext = {}
): Promise<BdrSiteContent> => {
	const saved = await updateTenantSettings(
		'public-content',
		normalizeContent(content),
		expectedVersion,
		context.fetcher,
		context.accessToken
	);
	return normalizeContent(saved.values);
};

export const updateBdrSiteContent = async (
	updater: (content: BdrSiteContent) => void,
	context: TenantSettingsApiContext = {}
): Promise<BdrSiteContent> => {
	const document = await loadBdrSiteContentDocument(context.fetcher);
	const content = normalizeContent(document.values);
	updater(content);
	return writeBdrSiteContent(content, document.version, context);
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

export const loadBdrSiteContent = async (
	fetcher: typeof globalThis.fetch = globalThis.fetch
): Promise<BdrSiteContent> => {
	try {
		const document = await loadBdrSiteContentDocument(fetcher);
		return normalizeContent(document.values);
	} catch (cause) {
		console.warn('Unable to read durable BDR site content; using packaged defaults.', cause);
		return cloneDefaultContent();
	}
};

export const saveBdrServices = async (
	items: string[],
	context: TenantSettingsApiContext = {}
): Promise<BdrSiteContent> => {
	const services = normalizeServices(items) ?? [];
	return updateBdrSiteContent((content) => {
		content.services.items = services;
	}, context);
};

export const saveBdrThemeSettings = async (
	value: BdrThemeSettingsPatch,
	context: TenantSettingsApiContext = {}
): Promise<BdrSiteContent> => {
	const document = await loadBdrSiteContentDocument(context.fetcher);
	const content = normalizeContent(document.values);
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

	return writeBdrSiteContent(content, document.version, context);
};

export const applyBdrContractorPresetSelection = async (
	presetId: string,
	context: TenantSettingsApiContext = {}
): Promise<BdrSiteContent> => {
	return updateBdrSiteContent((content) => {
		const appliedPreset = applyBdrContractorPresetToContent(content, presetId);

		if (!appliedPreset) {
			throw new Error(`Unknown contractor preset: ${presetId}`);
		}
	}, context);
};
