import { resolveMvpScaffold } from '$lib/mvp';

export const load = async ({ fetch, data }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);

	return {
		...data,
		source,
		estimates: snapshot.estimates,
		customers: snapshot.customers
	};
};
