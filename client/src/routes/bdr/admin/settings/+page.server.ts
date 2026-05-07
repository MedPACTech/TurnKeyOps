import { fail } from '@sveltejs/kit';
import {
	defaultBdrEstimateDefaults,
	loadBdrEstimateDefaults,
	saveBdrEstimateDefaults
} from '$lib/server/bdr-estimate-defaults';
import {
	loadBdrBillingSettings,
	saveBdrBillingSettings
} from '$lib/server/bdr-billing-settings';

const parseDefaultsForm = (formData: FormData) =>
	Object.fromEntries(
		Object.keys(defaultBdrEstimateDefaults)
			.filter((key) => formData.has(key))
			.map((key) => [key, Number(formData.get(key))])
	);

export const load = async () => {
	return {
		estimateDefaults: await loadBdrEstimateDefaults(),
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
	},
	saveDefaults: async ({ request }) => {
		const formData = await request.formData();
		try {
			return {
				estimateDefaults: await saveBdrEstimateDefaults(parseDefaultsForm(formData)),
				defaultsSaved: true
			};
		} catch (cause) {
			console.error('Unable to save BDR estimate defaults.', cause);
			return fail(500, {
				message: 'Could not save estimate defaults.'
			});
		}
	}
};
