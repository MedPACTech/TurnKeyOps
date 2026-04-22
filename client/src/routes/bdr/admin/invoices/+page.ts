import { resolveMvpScaffold } from '$lib/mvp';

export const load = async ({ fetch }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);

	return {
		source,
		invoices: snapshot.invoices,
		customers: snapshot.customers
	};
};
