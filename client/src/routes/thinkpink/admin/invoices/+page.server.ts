import { loadBdrInvoices } from '$lib/server/bdr-invoices';

export const load = async ({ fetch }) => {
	try {
		const invoices = await loadBdrInvoices(fetch);
		return { source: 'TurnKeyOps API', invoices, error: null, loadedAtUtc: new Date().toISOString() };
	} catch {
		return { source: 'TurnKeyOps API', invoices: [], error: 'Live invoices could not be loaded.', loadedAtUtc: new Date().toISOString() };
	}
};
