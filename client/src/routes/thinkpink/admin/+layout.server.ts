import { error } from '@sveltejs/kit';

export const load = ({ locals }) => {
	if (!locals.bdrAdminSession) {
		throw error(403, 'Think Pink Admin access requires owner or office admin privileges.');
	}

	return {
		role: locals.bdrAdminSession.role
	};
};
