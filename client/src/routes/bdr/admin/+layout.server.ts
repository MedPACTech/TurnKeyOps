import { error } from '@sveltejs/kit';
import { bobVoiceCookie, normalizeBobVoice } from '$lib/bob-voice';
import { authTokenCookie } from '$lib/server/auth-session';

export const load = ({ locals, cookies }) => {
	if (!locals.bdrAdminSession) {
		throw error(403, 'Admin access requires owner or office admin privileges.');
	}

	return {
		adminSession: locals.bdrAdminSession,
		role: locals.bdrAdminSession.role,
		bobVoice: normalizeBobVoice(cookies.get(bobVoiceCookie)),
		apiAccessToken: cookies.get(authTokenCookie) ?? null
	};
};
