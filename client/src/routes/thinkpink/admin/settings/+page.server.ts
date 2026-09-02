import { fail } from '@sveltejs/kit';
import {
	defaultThinkPinkSettings,
	loadThinkPinkSettings,
	saveThinkPinkSettings
} from '$lib/server/thinkpink-settings';
import { authTokenCookie } from '$lib/server/auth-session';

const list = (formData: FormData, key: string) =>
	String(formData.get(key) ?? '')
		.split(/\r?\n/)
		.map((item) => item.trim())
		.filter(Boolean);

export const load = async ({ fetch, cookies }) => ({
	settings: await loadThinkPinkSettings(fetch, cookies.get(authTokenCookie))
});

export const actions = {
	save: async ({ request, fetch, cookies }) => {
		const formData = await request.formData();
		const input: Record<string, unknown> = {};
		for (const [key, fallback] of Object.entries(defaultThinkPinkSettings)) {
			input[key] = Array.isArray(fallback) ? list(formData, key) : Number(formData.get(key));
		}
		try {
			return {
				settings: await saveThinkPinkSettings(input, fetch, cookies.get(authTokenCookie)),
				saved: true
			};
		} catch (cause) {
			console.error('Unable to save Think Pink settings.', cause);
			return fail(500, { message: 'Could not save Think Pink settings.' });
		}
	}
};
