import { error } from '@sveltejs/kit';

export const load = ({ locals }) => {
	if (!locals.bdrAdminSession) {
		throw error(403, 'Admin access requires owner or office admin privileges.');
	}

	return {
		adminSession: locals.bdrAdminSession,
		role: locals.bdrAdminSession.role
	};
};
