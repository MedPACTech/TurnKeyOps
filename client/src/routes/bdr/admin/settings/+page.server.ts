import { fail } from '@sveltejs/kit';
import {
	defaultBdrEstimateDefaults,
	loadBdrEstimateDefaults,
	saveBdrEstimateDefaults
} from '$lib/server/bdr-estimate-defaults';

const parseDefaultsForm = (formData: FormData) =>
	Object.fromEntries(
		Object.keys(defaultBdrEstimateDefaults)
			.filter((key) => formData.has(key))
			.map((key) => [key, Number(formData.get(key))])
	);

export const load = async () => {
	return {
		estimateDefaults: await loadBdrEstimateDefaults()
	};
};

export const actions = {
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
