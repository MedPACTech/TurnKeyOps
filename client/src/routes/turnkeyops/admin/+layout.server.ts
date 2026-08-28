import { error } from '@sveltejs/kit';
import { authTokenCookie } from '$lib/server/auth-session';

export const load = ({ locals, cookies }) => {
	if (locals.adminSession?.surface !== 'internal-admin') {
		throw error(403, 'Internal Admin access is required.');
	}

	return {
		adminSession: locals.adminSession,
		apiAccessToken: cookies.get(authTokenCookie) ?? null
	};
};
