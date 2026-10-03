import { loadFieldInvoices } from './invoice-api.server';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async (event) => {
	await event.parent();
	return { invoices: await loadFieldInvoices(event) };
};
