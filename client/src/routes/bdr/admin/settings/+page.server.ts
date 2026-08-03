import { fail } from '@sveltejs/kit';
import {
	loadBdrBillingSettings,
	saveBdrBillingSettings
} from '$lib/server/bdr-billing-settings';

export const load = async () => {
	return {
		billingSettings: await loadBdrBillingSettings()
	};
};

export const actions = {
	saveBillingSettings: async ({ request }) => {
		const formData = await request.formData();
		try {
			return {
				billingSettings: await saveBdrBillingSettings({
					depositPercentRequired: Number(formData.get('depositPercentRequired'))
				}),
				billingSettingsSaved: true
			};
		} catch (cause) {
			console.error('Unable to save BDR billing settings.', cause);
			return fail(500, {
				message: 'Could not save billing settings.'
			});
		}
	}
};
