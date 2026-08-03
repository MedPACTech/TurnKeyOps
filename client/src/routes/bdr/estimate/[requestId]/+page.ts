import type { PageLoad } from './$types';
import { getEstimateDefaults } from '$lib/api/estimate-defaults';
import { getStoredAuthToken } from '$lib/api/client';

export const ssr = false;

export const load: PageLoad = async ({ data, fetch }) => ({
	...data,
	estimateDefaults: await getEstimateDefaults(fetch, getStoredAuthToken())
});
