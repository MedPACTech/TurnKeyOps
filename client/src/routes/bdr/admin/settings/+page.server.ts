import { fail } from '@sveltejs/kit';
import {
	loadBdrBillingSettings,
	saveBdrBillingSettings
} from '$lib/server/bdr-billing-settings';
import { authTokenCookie } from '$lib/server/auth-session';

export const load = async ({ fetch, cookies }) => {
	return {
		billingSettings: await loadBdrBillingSettings(fetch, cookies.get(authTokenCookie))
	};
};

export const actions = {
	saveBillingSettings: async ({ request, fetch, cookies }) => {
		const formData = await request.formData();
		try {
			return {
				billingSettings: await saveBdrBillingSettings(
					{ depositPercentRequired: Number(formData.get('depositPercentRequired')) },
					fetch,
					cookies.get(authTokenCookie)
				),
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
