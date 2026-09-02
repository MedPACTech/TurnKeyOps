import type { PageLoad } from './$types';
import { getEstimateDefaults } from '$lib/api/estimate-defaults';
import { setStoredAuthToken } from '$lib/api/client';
import { resolveMvpScaffold } from '$lib/mvp';

export const ssr = false;

export const load: PageLoad = async ({ data, fetch, parent }) => {
	const parentData = await parent();
	const accessToken = parentData.apiAccessToken ?? null;
	if (accessToken) setStoredAuthToken(accessToken);
	const [{ snapshot, source }, estimateDefaults] = await Promise.all([
		resolveMvpScaffold(fetch),
		getEstimateDefaults(fetch, accessToken)
	]);

	return {
		...data,
		source,
		estimates: snapshot.estimates,
		customers: snapshot.customers,
		estimateDefaults
	};
};
