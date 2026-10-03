import { error } from '@sveltejs/kit';
import { bobVoiceCookie, normalizeBobVoice } from '$lib/bob-voice';
export const load = ({ locals, cookies }) => {
 if (!locals.bdrAdminSession || locals.adminSession?.tenantId !== '88888888-8888-4888-8888-888888888883') throw error(403, 'Carl Zipf admin access is required.');
 return { modulePermissions: locals.modulePermissions ?? [], adminSession: locals.adminSession, role: locals.bdrAdminSession.role, bobVoice: normalizeBobVoice(cookies.get(bobVoiceCookie)) };
};
