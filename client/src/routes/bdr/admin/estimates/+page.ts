import { resolveMvpScaffold } from '$lib/mvp';

export const load = async ({ fetch }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);

	return {
		source,
		estimates: snapshot.estimates,
		customers: snapshot.customers
	};
};
