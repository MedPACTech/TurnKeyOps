import { fail } from '@sveltejs/kit';
import {
	applyBdrContractorPresetSelection,
	loadBdrSiteContent,
	saveBdrServices,
	updateBdrSiteContent
} from '$lib/server/bdr-site-content';
import type {
	BdrCtaType,
	BdrHeroMediaOverride,
	BdrHeroTrustBadge,
	BdrServiceCategory,
	BdrSocialLink,
	ContentLink
} from '$lib/bdr-site-content';

const getValue = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();
const getBoolean = (formData: FormData, key: string) => getValue(formData, key) === 'true';

const parseNavigationLinks = (value: string): ContentLink[] =>
	value
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line): ContentLink | null => {
			const [label = '', href = '', openInNewTab = 'false'] = line.split('|').map((part) => part.trim());
			if (!label || !href) return null;

			return {
				label,
				href,
				openInNewTab: openInNewTab === 'true'
			};
		})
		.filter((link): link is ContentLink => link !== null);

const parseHeroTrustBadges = (value: string): BdrHeroTrustBadge[] =>
	value
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line): BdrHeroTrustBadge | null => {
			const [iconAssetKey = '', title = '', description = ''] = line
				.split('|')
				.map((part) => part.trim());
			if (!iconAssetKey || !title || !description) return null;

			return {
				iconAssetKey,
				title,
				description
			};
		})
		.filter((badge): badge is BdrHeroTrustBadge => badge !== null);

const parseHeroMediaOverrides = (value: string): BdrHeroMediaOverride[] =>
	value
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line): BdrHeroMediaOverride | null => {
			const [
				contractorType = '',
				heroImageAssetKey = '',
				backgroundImageAssetKey = '',
				backgroundTextureAssetKey = '',
				heroImageAltText = ''
			] = line.split('|').map((part) => part.trim());

			if (!contractorType || !heroImageAssetKey) return null;

			return {
				contractorType,
				heroImageAssetKey,
				backgroundImageAssetKey: backgroundImageAssetKey || undefined,
				backgroundTextureAssetKey: backgroundTextureAssetKey || undefined,
				heroImageAltText: heroImageAltText || undefined
			};
		})
		.filter((override): override is BdrHeroMediaOverride => override !== null);

const parseSocialLinks = (value: string): BdrSocialLink[] =>
	value
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line): BdrSocialLink | null => {
			const [platform = '', url = '', iconAssetKey = ''] = line.split('|').map((part) => part.trim());
			if (!platform || !url || !iconAssetKey) return null;

			return {
				platform,
				url,
				iconAssetKey
			};
		})
		.filter((link): link is BdrSocialLink => link !== null);

const parseServiceCards = (value: string): BdrServiceCategory[] =>
	value
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line, index): BdrServiceCategory | null => {
			const [
				name = '',
				slug = '',
				description = '',
				iconAssetKey = '',
				imageAssetKey = '',
				detailPageUrl = '',
				featured = 'true',
				sortOrder = String(index + 1)
			] = line.split('|').map((part) => part.trim());

			if (!name || !slug || !description || !iconAssetKey) return null;

			return {
				name,
				slug,
				description,
				iconAssetKey,
				imageAssetKey: imageAssetKey || undefined,
				detailPageUrl: detailPageUrl || undefined,
				contractorType: 'shared',
				featured: featured === 'true',
				sortOrder: Number.isFinite(Number(sortOrder)) ? Number(sortOrder) : index + 1
			};
		})
		.filter((card): card is BdrServiceCategory => card !== null);

const parseProcessSteps = (
	value: string
): Array<{
	step: string;
	title: string;
	copy: string;
	iconAssetKey: string;
	timeframe?: string;
}> =>
	value
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line): {
			step: string;
			title: string;
			copy: string;
			iconAssetKey: string;
			timeframe?: string;
		} | null => {
			const [step = '', title = '', copy = '', iconAssetKey = '', timeframe = ''] = line
				.split('|')
				.map((part) => part.trim());
			if (!step || !title || !copy || !iconAssetKey) return null;

			return {
				step,
				title,
				copy,
				iconAssetKey,
				timeframe: timeframe || undefined
			};
		})
		.filter(
			(step): step is {
				step: string;
				title: string;
				copy: string;
				iconAssetKey: string;
				timeframe?: string;
			} => step !== null
		);

const parseCtaType = (value: string, fallback: BdrCtaType): BdrCtaType =>
	value === 'anchor' || value === 'link' || value === 'phone' ? value : fallback;

export const load = async () => {
	return {
		content: await loadBdrSiteContent()
	};
};

export const actions = {
	updateCtaBanner: async ({ request }) => {
		const formData = await request.formData();
		const overlayOpacity = Number(getValue(formData, 'ctaBannerOverlayOpacity'));

		if (
			!getValue(formData, 'ctaBannerTitle') ||
			!getValue(formData, 'ctaBannerDescription') ||
			!getValue(formData, 'ctaBannerBackgroundImageAssetKey') ||
			!getValue(formData, 'ctaBannerPrimaryCtaLabel') ||
			!getValue(formData, 'ctaBannerPrimaryCtaHref') ||
			!getValue(formData, 'ctaBannerSecondaryCtaLabel') ||
			!getValue(formData, 'ctaBannerSecondaryCtaHref')
		) {
			return fail(400, {
				savedSectionId: 'cta-banner',
				message: 'CTA banner copy, image, and CTA fields are required.'
			});
		}

		try {
			return {
				content: await updateBdrSiteContent((content) => {
					content.ctaBanner = {
						eyebrow: getValue(formData, 'ctaBannerEyebrow') || content.ctaBanner.eyebrow,
						title: getValue(formData, 'ctaBannerTitle'),
						description: getValue(formData, 'ctaBannerDescription'),
						backgroundImageAssetKey: getValue(formData, 'ctaBannerBackgroundImageAssetKey'),
						backgroundImageAltText:
							getValue(formData, 'ctaBannerBackgroundImageAltText') ||
							content.ctaBanner.backgroundImageAltText,
						overlayOpacity:
							Number.isFinite(overlayOpacity) && overlayOpacity >= 0 && overlayOpacity <= 1
								? overlayOpacity
								: content.ctaBanner.overlayOpacity,
						primaryCtaLabel: getValue(formData, 'ctaBannerPrimaryCtaLabel'),
						primaryCtaHref: getValue(formData, 'ctaBannerPrimaryCtaHref'),
						secondaryCtaLabel: getValue(formData, 'ctaBannerSecondaryCtaLabel'),
						secondaryCtaType: parseCtaType(
							getValue(formData, 'ctaBannerSecondaryCtaType'),
							content.ctaBanner.secondaryCtaType
						),
						secondaryCtaHref: getValue(formData, 'ctaBannerSecondaryCtaHref')
					};
				}),
				savedSectionId: 'cta-banner',
				savedMessage: 'CTA banner settings saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save CTA banner content.', cause);
			return fail(500, {
				savedSectionId: 'cta-banner',
				message: 'Could not save CTA banner settings.'
			});
		}
	},
	updateProcessSection: async ({ request }) => {
		const formData = await request.formData();
		const steps = parseProcessSteps(getValue(formData, 'processSteps'));
		if (steps.length < 3 || steps.length > 5) {
			return fail(400, {
				savedSectionId: 'process',
				message: 'Configure between 3 and 5 process steps.'
			});
		}

		try {
			return {
				content: await updateBdrSiteContent((content) => {
					content.process = {
						eyebrow: getValue(formData, 'processEyebrow') || content.process.eyebrow,
						title: getValue(formData, 'processTitle') || content.process.title,
						description:
							getValue(formData, 'processDescription') || content.process.description,
						steps
					};
				}),
				savedSectionId: 'process',
				savedMessage: 'Process section saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save process section.', cause);
			return fail(500, {
				savedSectionId: 'process',
				message: 'Could not save process section.'
			});
		}
	},
	updateServicesSection: async ({ request }) => {
		const formData = await request.formData();
		const cards = parseServiceCards(getValue(formData, 'serviceCards'));
		if (cards.length < 3 || cards.length > 8) {
			return fail(400, {
				savedSectionId: 'services',
				message: 'Configure between 3 and 8 service cards.'
			});
		}

		try {
			return {
				content: await updateBdrSiteContent((content) => {
					const activeContractorType =
						content.contractorPresets.find((preset) => preset.id === content.activeContractorPresetId)
							?.contractorType ?? 'shared';
					content.services = {
						eyebrow: getValue(formData, 'servicesEyebrow') || content.services.eyebrow,
						title: getValue(formData, 'servicesTitle') || content.services.title,
						copy: getValue(formData, 'servicesCopy') || content.services.copy,
						items: cards.map((card) => card.name),
						ctaLabel: getValue(formData, 'servicesCtaLabel'),
						ctaHref: getValue(formData, 'servicesCtaHref')
					};
					content.serviceCategories = cards.map((card, index) => ({
						...card,
						contractorType: activeContractorType,
						sortOrder: index + 1
					}));
				}),
				savedSectionId: 'services',
				savedMessage: 'Services section saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save services section.', cause);
			return fail(500, {
				savedSectionId: 'services',
				message: 'Could not save services section.'
			});
		}
	},
	updateFooter: async ({ request }) => {
		const formData = await request.formData();
		const navigationLinks = parseNavigationLinks(getValue(formData, 'footerNavigationLinks'));
		const servicesLinks = parseNavigationLinks(getValue(formData, 'footerServicesLinks'));
		const legalLinks = parseNavigationLinks(getValue(formData, 'footerLegalLinks'));
		const socialLinks = parseSocialLinks(getValue(formData, 'footerSocialLinks'));
		const brandName = getValue(formData, 'footerBrandName');
		const body = getValue(formData, 'footerBody');

		if (!brandName || !body) {
			return fail(400, {
				savedSectionId: 'footer',
				message: 'Footer brand name and company description are required.'
			});
		}

		try {
			return {
				content: await updateBdrSiteContent((content) => {
					content.footer = {
						eyebrow: getValue(formData, 'footerEyebrow') || content.footer.eyebrow,
						logoAssetKey: getValue(formData, 'footerLogoAssetKey') || content.footer.logoAssetKey,
						brandName,
						body,
						serviceAreaText: getValue(formData, 'footerServiceAreaText'),
						navigationEyebrow:
							getValue(formData, 'footerNavigationEyebrow') || content.footer.navigationEyebrow,
						navigationLinks,
						servicesEyebrow:
							getValue(formData, 'footerServicesEyebrow') || content.footer.servicesEyebrow,
						servicesLinks,
						contactEyebrow:
							getValue(formData, 'footerContactEyebrow') || content.footer.contactEyebrow,
						phone: getValue(formData, 'footerPhone'),
						email: getValue(formData, 'footerEmail'),
						address: getValue(formData, 'footerAddress'),
						socialLinks
					};
					content.postFooter = {
						legalLinksEyebrow:
							getValue(formData, 'footerLegalEyebrow') || content.postFooter.legalLinksEyebrow,
						legalLinks,
						copyright:
							getValue(formData, 'footerCopyright') || content.postFooter.copyright
					};
				}),
				savedSectionId: 'footer',
				savedMessage: 'Footer settings saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save footer content.', cause);
			return fail(500, {
				savedSectionId: 'footer',
				message: 'Could not save footer settings.'
			});
		}
	},
	updateHero: async ({ request }) => {
		const formData = await request.formData();
		const eyebrow = getValue(formData, 'eyebrow');
		const headline = getValue(formData, 'headline');
		const subheadline = getValue(formData, 'subheadline');
		const primaryCtaLabel = getValue(formData, 'primaryCtaLabel');
		const primaryCtaHref = getValue(formData, 'primaryCtaHref');
		const secondaryCtaLabel = getValue(formData, 'secondaryCtaLabel');
		const secondaryCtaHref = getValue(formData, 'secondaryCtaHref');
		const heroImageAssetKey = getValue(formData, 'heroImageAssetKey');
		const heroImageAltText = getValue(formData, 'heroImageAltText');
		const badges = parseHeroTrustBadges(getValue(formData, 'trustBadges'));
		const mediaByContractorType = parseHeroMediaOverrides(getValue(formData, 'mediaByContractorType'));

		if (
			!eyebrow ||
			!headline ||
			!subheadline ||
			!primaryCtaLabel ||
			!primaryCtaHref ||
			!secondaryCtaLabel ||
			!secondaryCtaHref ||
			!heroImageAssetKey
		) {
			return fail(400, {
				savedSectionId: 'hero',
				message: 'Hero copy, CTA labels/targets, and the primary hero image are required.'
			});
		}

		try {
			return {
				content: await updateBdrSiteContent((content) => {
					content.hero = {
						eyebrow,
						headline,
						subheadline,
						primaryCtaLabel,
						primaryCtaHref,
						primaryCtaType: parseCtaType(
							getValue(formData, 'primaryCtaType'),
							content.hero.primaryCtaType
						),
						secondaryCtaLabel,
						secondaryCtaHref,
						secondaryCtaType: parseCtaType(
							getValue(formData, 'secondaryCtaType'),
							content.hero.secondaryCtaType
						),
						heroImageAssetKey,
						heroImageAltText,
						backgroundImageAssetKey: getValue(formData, 'backgroundImageAssetKey'),
						backgroundTextureAssetKey: getValue(formData, 'backgroundTextureAssetKey'),
						trustBadgeEyebrow:
							getValue(formData, 'trustBadgeEyebrow') || content.hero.trustBadgeEyebrow,
						trustBadges: badges,
						mediaByContractorType
					};
				}),
				savedSectionId: 'hero',
				savedMessage: 'Hero settings saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save hero content.', cause);
			return fail(500, {
				savedSectionId: 'hero',
				message: 'Could not save hero settings.'
			});
		}
	},
	updateNavigation: async ({ request }) => {
		const formData = await request.formData();
		const navigationLinks = parseNavigationLinks(getValue(formData, 'navigationLinks'));

		if (!navigationLinks.length) {
			return fail(400, {
				savedSectionId: 'navigation',
				message: 'Add at least one navigation link.'
			});
		}

		const layoutValue = getValue(formData, 'layout');

		try {
			return {
				content: await updateBdrSiteContent((content) => {
					content.navigation = {
						announcement: getValue(formData, 'announcement') || content.navigation.announcement,
						brandName: getValue(formData, 'brandName') || content.navigation.brandName,
						logoAssetKey: getValue(formData, 'logoAssetKey') || content.navigation.logoAssetKey,
						faviconAssetKey:
							getValue(formData, 'faviconAssetKey') || content.navigation.faviconAssetKey,
						links: navigationLinks,
						primaryCtaLabel:
							getValue(formData, 'primaryCtaLabel') || content.navigation.primaryCtaLabel,
						primaryCtaHref:
							getValue(formData, 'primaryCtaHref') || content.navigation.primaryCtaHref,
						phoneNumber: getValue(formData, 'phoneNumber') || content.navigation.phoneNumber,
						showPhoneButton: getBoolean(formData, 'showPhoneButton'),
						showThemeControl: getBoolean(formData, 'showThemeControl'),
						stickyHeader: getBoolean(formData, 'stickyHeader'),
						layout:
							layoutValue === 'centered' ||
							layoutValue === 'right-aligned' ||
							layoutValue === 'logo-left'
								? layoutValue
								: content.navigation.layout
					};
				}),
				savedSectionId: 'navigation',
				savedMessage: 'Navigation settings saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save navigation content.', cause);
			return fail(500, {
				savedSectionId: 'navigation',
				message: 'Could not save navigation settings.'
			});
		}
	},
	updateServices: async ({ request }) => {
		const formData = await request.formData();
		const services = formData
			.getAll('services')
			.map((value) => String(value ?? '').trim())
			.filter(Boolean);

		try {
			return {
				content: await saveBdrServices(services)
			};
		} catch (cause) {
			console.error('Unable to save BDR services content.', cause);
			return fail(500, {
				message: 'Could not save services.'
			});
		}
	},
	applyContractorPreset: async ({ request }) => {
		const formData = await request.formData();
		const presetId = String(formData.get('presetId') ?? '').trim();

		if (!presetId) {
			return fail(400, {
				message: 'Preset id is required.'
			});
		}

		try {
			return {
				content: await applyBdrContractorPresetSelection(presetId),
				savedSectionId: 'contractor-presets',
				savedMessage: `Applied contractor preset ${presetId}.`
			};
		} catch (cause) {
			console.error('Unable to apply contractor preset.', cause);
			return fail(500, {
				savedSectionId: 'contractor-presets',
				message: 'Could not apply contractor preset.'
			});
		}
	}
};
