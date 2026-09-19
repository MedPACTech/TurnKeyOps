import type { Customer, Invoice } from '$lib/types/mvp';

// Legacy invoice rows do not contain these details. Never invent payment,
// ownership, or hold information from sample rows or their position in a list.
export const buildInvoiceViews = (invoices: Invoice[], customers: Customer[]) =>
 invoices.map(invoice => ({
  ...invoice,
  billingPhase: 'Not recorded', paymentMethod: 'Not recorded', checkHold: 'Not recorded',
  owner: 'Not assigned', nextStep: 'Review the invoice record.',
  customer: customers.find(customer => customer.id === invoice.customerId)
 }));
