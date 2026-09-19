import type { PageLoad } from './$types';
import { getEstimateDefaults } from '$lib/api/estimate-defaults';
import { setStoredAuthToken } from '$lib/api/client';

export const ssr = false;

export const load: PageLoad = async ({ data, fetch, parent }) => {
	const parentData = await parent();
	const accessToken = parentData.apiAccessToken ?? null;
	if (accessToken) setStoredAuthToken(accessToken);
	const estimateDefaults = await getEstimateDefaults(fetch, accessToken);

	return {
		...data,
		estimateDefaults
	};
};
