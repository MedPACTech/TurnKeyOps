import { adminSettingsGroups } from '$lib/admin-settings';

export const load = () => {
	return {
		groups: adminSettingsGroups
	};
};
