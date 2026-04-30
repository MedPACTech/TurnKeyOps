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

const parseCtaType = (value: string, fallback: BdrCtaType): BdrCtaType =>
	value === 'anchor' || value === 'link' || value === 'phone' ? value : fallback;

export const load = async () => {
	return {
		content: await loadBdrSiteContent()
	};
};

export const actions = {
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
