import { fail } from '@sveltejs/kit';
import {
	applyBdrContractorPresetSelection,
	loadBdrSiteContent,
	saveBdrServices,
	updateBdrSiteContent
} from '$lib/server/bdr-site-content';
import type { ContentLink } from '$lib/bdr-site-content';

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

export const load = async () => {
	return {
		content: await loadBdrSiteContent()
	};
};

export const actions = {
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
