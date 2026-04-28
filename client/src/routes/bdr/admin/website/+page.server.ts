import { fail } from '@sveltejs/kit';
import { loadBdrSiteContent, saveBdrServices } from '$lib/server/bdr-site-content';

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
	}
};
