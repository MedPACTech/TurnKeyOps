import { error } from '@sveltejs/kit';
import { getBdrInvoice } from '$lib/server/bdr-invoices';

export const load = async ({ params, url }) => {
	const invoiceId = decodeURIComponent(params.invoiceId);
	const invoice = await getBdrInvoice(invoiceId);
	if (!invoice) throw error(404, 'Invoice packet not found.');

	const returnTo = url.searchParams.get('returnTo') ?? '';
	const safeReturnTo = returnTo.startsWith('/bdr/admin/') ? returnTo : '';

	return { invoice, returnTo: safeReturnTo };
};
