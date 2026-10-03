import { fail } from '@sveltejs/kit';
import { fieldApi, fieldResponse, requireFieldOrigin } from '../../field-api.server';
import { loadFieldInvoice, requireInvoiceId, type FieldInvoice } from '../invoice-api.server';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async (event) => {
	await event.parent();
	return { invoice: await loadFieldInvoice(event, event.params.invoiceId) };
};

export const actions: Actions = {
	sign: async (event) => {
		requireFieldOrigin(event);
		const id = requireInvoiceId(event.params.invoiceId);
		const formData = await event.request.formData();
		const signerPrintedName = String(formData.get('signerPrintedName') ?? '').trim().replace(/\s+/g, ' ');
		const expectedVersion = String(formData.get('expectedVersion') ?? '').trim();
		const intentToSign = formData.get('intentToSign') === 'on';
		if (signerPrintedName.length < 2 || signerPrintedName.length > 200)
			return fail(400, { message: 'Enter the customer’s printed name (2–200 characters).' });
		if (!intentToSign) return fail(400, { message: 'The customer must check the acknowledgement before signing.' });
		if (!expectedVersion) return fail(400, { message: 'Reload the invoice before signing.' });
		const { call } = await fieldApi(event);
		try {
			const invoice = await fieldResponse<FieldInvoice>(await call(`/api/locksmith/field-invoices/${encodeURIComponent(id)}/completion-signature`, {
				method: 'POST', body: JSON.stringify({ signerPrintedName, intentToSign, expectedVersion })
			}));
			return { message: `${invoice.invoiceNumber} completion acknowledgement recorded.`, signedInvoice: invoice };
		} catch (cause) {
			const message = cause && typeof cause === 'object' && 'body' in cause && typeof cause.body === 'object' && cause.body && 'message' in cause.body
				? String(cause.body.message) : 'Could not record the acknowledgement. Reload and try again.';
			return fail(400, { message });
		}
	}
};
