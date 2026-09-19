import { peopleRequest } from '$lib/server/people';
import { error } from '@sveltejs/kit';
import { bobVoiceCookie, normalizeBobVoice } from '$lib/bob-voice';

export const load = async (event) => {
 const {locals,cookies}=event;
 const modulePermissions = locals.modulePermissions ?? await peopleRequest<string[]>(event, '/api/my-module-access');
	if (!locals.bdrAdminSession) {
		throw error(403, 'Think Pink Admin access requires owner or office admin privileges.');
	}

	return {
        modulePermissions,
		adminSession: locals.adminSession,
		role: locals.bdrAdminSession.role,
		bobVoice: normalizeBobVoice(cookies.get(bobVoiceCookie))
	};
};
