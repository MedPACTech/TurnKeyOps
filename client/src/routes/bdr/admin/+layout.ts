import { normalizeBdrAdminRole } from '$lib/config/platform';

export const load = ({ url }) => {
	return {
		role: normalizeBdrAdminRole(url.searchParams.get('role'))
	};
};
