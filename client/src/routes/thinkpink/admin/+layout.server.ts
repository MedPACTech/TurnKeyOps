import { error } from '@sveltejs/kit';
import { bobVoiceCookie, normalizeBobVoice } from '$lib/bob-voice';

export const load = ({ locals, cookies }) => {
	if (!locals.bdrAdminSession) {
		throw error(403, 'Think Pink Admin access requires owner or office admin privileges.');
	}

	return {
		adminSession: locals.adminSession,
		role: locals.bdrAdminSession.role,
		bobVoice: normalizeBobVoice(cookies.get(bobVoiceCookie))
	};
};
