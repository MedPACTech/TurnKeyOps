import { error } from '@sveltejs/kit';
import { fieldApi, fieldResponse } from '../field-api.server';
import type { RequestEvent } from '@sveltejs/kit';

export type FieldInvoice = {
	id: string;
	invoiceNumber: string;
	status: string;
	customerName?: string | null;
	siteName?: string | null;
	serviceSummary?: string | null;
	total: number;
	amountPaid: number;
	balanceDue: number;
	subtotal: number;
	taxAmount: number;
	scopeLineItems?: string[];
	lineItems?: Array<{ id: string; description: string; quantity: number; unitPrice: number; lineTotal: number }>;
	completionSignature?: {
		signerPrintedName: string;
		consentText: string;
		consentVersion: string;
		signedAtUtc: string;
		invoiceNumber: string;
		invoiceTotal: number;
		documentHash: string;
	};
	version: string;
};

export function requireInvoiceId(value: string) {
	if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value))
		throw error(404, 'Invoice not found.');
	return value;
}

export async function loadFieldInvoices(event: RequestEvent) {
	const { call } = await fieldApi(event);
	return fieldResponse<FieldInvoice[]>(await call('/api/locksmith/field-invoices'));
}

export async function loadFieldInvoice(event: RequestEvent, id: string) {
	const { call } = await fieldApi(event);
	const response = await call(`/api/locksmith/field-invoices/${encodeURIComponent(requireInvoiceId(id))}`);
	if (response.status === 404) throw error(404, 'Invoice not found.');
	return fieldResponse<FieldInvoice>(response);
}
