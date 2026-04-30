import { fail } from '@sveltejs/kit';
import type { BdrThemeSettings } from '$lib/bdr-site-content';
import { loadBdrSiteContent, saveBdrThemeSettings } from '$lib/server/bdr-site-content';

export const load = async () => {
	return {
		themeSettings: (await loadBdrSiteContent()).themeSettings
	};
};

const getValue = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

export const actions = {
	updateThemeSettings: async ({ request }) => {
		const formData = await request.formData();
		const selectedGroupId = getValue(formData, 'selectedGroupId');

		try {
			const content = await saveBdrThemeSettings({
				mode: (getValue(formData, 'theme-mode') || undefined) as
					| 'Light'
					| 'Dark'
					| 'System'
					| undefined,
				preset: (getValue(formData, 'theme-preset') || undefined) as
					| 'Clean'
					| 'Industrial'
					| 'Premium'
					| 'Minimal'
					| 'Bold'
					| undefined,
				colors: {
					primary: getValue(formData, 'theme-primary') || undefined,
					secondary: getValue(formData, 'theme-secondary') || undefined,
					accent: getValue(formData, 'theme-accent') || undefined,
					background: getValue(formData, 'theme-background') || undefined,
					surface: getValue(formData, 'theme-surface') || undefined,
					text: getValue(formData, 'theme-text') || undefined,
					border: getValue(formData, 'theme-border') || undefined
				} as Partial<BdrThemeSettings['colors']>,
				typography: {
					headingFont: getValue(formData, 'theme-heading-font') || undefined,
					bodyFont: getValue(formData, 'theme-body-font') || undefined
				} as Partial<BdrThemeSettings['typography']>,
				sizing: {
					buttonRadius: getValue(formData, 'theme-button-radius') || undefined,
					cardRadius: getValue(formData, 'theme-card-radius') || undefined,
					logoSize: getValue(formData, 'theme-logo-size') || undefined
				} as Partial<BdrThemeSettings['sizing']>,
				iconStyle: getValue(formData, 'theme-icon-style') || undefined,
				brandAssets: {
					logoAssetKey: getValue(formData, 'theme-logo-asset') || undefined,
					faviconAssetKey: getValue(formData, 'theme-favicon-asset') || undefined
				} as Partial<BdrThemeSettings['brandAssets']>
			});

			return {
				themeSettings: content.themeSettings,
				savedGroupId: selectedGroupId,
				savedMessage: 'Theme settings saved to the local contractor-site content store.'
			};
		} catch (cause) {
			console.error('Unable to save BDR theme settings.', cause);
			return fail(500, {
				savedGroupId: selectedGroupId,
				saveError: 'Theme settings could not be saved. Please try again.'
			});
		}
	}
};
