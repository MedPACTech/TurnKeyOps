import { fail } from '@sveltejs/kit';
import {
	applyBdrContractorPresetSelection,
	loadBdrSiteContent,
	saveBdrServices
} from '$lib/server/bdr-site-content';

export const load = async () => {
	return {
		content: await loadBdrSiteContent()
	};
};

export const actions = {
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
