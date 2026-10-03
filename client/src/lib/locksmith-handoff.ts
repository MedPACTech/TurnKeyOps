import { fieldDraftErrors, measurementFields, type FieldDraft, type FieldJobType, type FieldRequestSource } from './locksmith-drafts.ts';

export type FieldPricingContext = {
	jobTypes: FieldJobType[];
	catalog: { id: string; name: string; jobTypes: FieldJobType[]; unitPrice: number; sample: false }[];
	policyVersion: string;
	laborRatePerHour: number | null;
	taxPercent: number | null;
	laborHoursByJobType: Record<FieldJobType, Record<string, number>>;
};
export type FieldEstimate = {
	id: string; quoteRequestId: string; version: string; revisionNumber: number; status: string; savedAtUtc: string;
	customerName?: string; siteName?: string; serviceSummary?: string; expiresAtUtc?: string;
	delivery?: { status: string; method: string; reviewUrl: string; sentAtUtc: string; approvedAtUtc?: string; changesRequestedAtUtc?: string; responseNote?: string };
	locksmithPricing: {
		jobType?: FieldJobType; total: number; subtotal: number; discountAmount: number; taxAmount: number; taxPercent: number | null;
		requiresOfficeApproval: boolean; approvalReasons: string[]; policyVersion: string; officeApprovedAtUtc?: string; officeApprovedBy?: string;
		laborHours?: number; standardLaborHours?: number; laborOverrideReason?: string; laborRatePerHour?: number; discountPercent?: number;
		lines?: { catalogItemId?: string; name: string; openingName: string; quantity: number; unitPrice: number; total: number }[];
		openings?: { id: string; name: string; service: string; handing: string; notes: string; commercialNotes: string; measurements?: Record<string, { value: string; certainty: string }> }[];
	};
};
export type FieldHandoffResult = { requestId: string; requestSaved: boolean; estimate?: FieldEstimate; recovered?: boolean; error?: string };

export function openingObservations(draft: FieldDraft): string {
	return draft.openings.map((opening, index) => [
		`Opening ${index + 1}: ${opening.name} — ${opening.service}`,
		...measurementFields.filter(([key]) => opening.measurements[key].value).map(([key, label]) => `${label}: ${opening.measurements[key].value} in (${opening.measurements[key].certainty})`),
		opening.handing && `Handing/swing: ${opening.handing}`,
		opening.notes && `Hardware/site: ${opening.notes}`,
		draft.jobType === 'commercial' && opening.commercialNotes && `Commercial: ${opening.commercialNotes}`
	].filter(Boolean).join('\n')).join('\n\n');
}

export function handoffErrors(draft: FieldDraft, quote = false): string[] {
	const errors = fieldDraftErrors(draft);
	if (!draft.customer.trim() || draft.customer.trim().length > 200) errors.push('Provide a customer name up to 200 characters.');
	if (!draft.site.trim() || draft.site.trim().length > 300) errors.push('Provide a property address up to 300 characters.');
	if (!draft.contactEmail || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(draft.contactEmail) || draft.contactEmail.length > 200) errors.push('Provide a valid customer email.');
	if (!draft.contactPhone?.trim() || draft.contactPhone.length > 100) errors.push('Provide a customer phone number.');
	if (!draft.openings.length || draft.openings.some((opening) => !opening.name.trim())) errors.push('Add at least one opening and name every opening.');
	if (new TextEncoder().encode(JSON.stringify(draft.openings)).length > 180_000) errors.push('Opening details exceed the shared quote size limit. Shorten notes or split this visit into separate drafts.');
	if (quote && draft.openings.reduce((count, opening) => count + opening.items.length, 0) > 100) errors.push('Use at most 100 catalog lines in one shared quote.');
	if (quote && (!Number.isFinite(draft.laborHours ?? 0) || (draft.laborHours ?? 0) < 0 || (draft.laborHours ?? 0) > 1000)) errors.push('Enter labor hours between 0 and 1,000.');
	if (quote && (!Number.isFinite(draft.discountPercent ?? 0) || (draft.discountPercent ?? 0) < 0 || (draft.discountPercent ?? 0) > 100)) errors.push('Enter a discount between 0 and 100%.');
	if (draft.handoff && draft.handoff.source.propertyType !== draft.jobType) errors.push('This draft is linked to a different job type. Keep its original job type or start a separate field request.');
	return errors;
}

export function standardLaborHours(draft: FieldDraft, pricing: FieldPricingContext): number {
	return draft.openings.reduce((total, opening) => total + (pricing.laborHoursByJobType[draft.jobType]?.[opening.service] ?? 0), 0);
}

export function createFieldSource(draft: FieldDraft, id: string): FieldRequestSource {
	return { id, contactName: draft.customer.trim(), companyName: draft.customer.trim(), email: draft.contactEmail!.trim(), phone: draft.contactPhone!.trim(), siteName: draft.site.trim(), serviceAddress: draft.site.trim(), serviceType: [...new Set(draft.openings.map((opening) => opening.service))].join(', '), propertyType: draft.jobType, requestedTimeline: 'Office to confirm scheduling after scope approval', priority: 'standard', need: `Technician field capture for ${draft.openings.length} ${draft.jobType} opening(s). Full measurements and hardware scope are retained in the linked estimate draft when pricing succeeds. If estimate preparation fails, the technician retains the detailed device draft.`, attachments: [] };
}

export function createFieldEstimate(draft: FieldDraft) {
	return {
		customerName: draft.customer.trim(), siteName: draft.site.trim(), serviceSummary: [...new Set(draft.openings.map((opening) => opening.service))].join(', '),
		visitFindings: `${draft.openings.length} opening(s) captured. Measurements, certainty, handing and site notes are retained in the structured locksmith opening records.`, notes: 'Field measurement capture. Verify estimated dimensions and product-specific fit before ordering. Customer approval and installation scheduling remain separate steps.',
		status: 'draft', locations: [], expectedVersion: draft.handoff?.estimateVersion ?? null,
		locksmith: { jobType: draft.jobType, openings: draft.openings.map(({ items: _items, ...opening }) => opening), items: draft.openings.flatMap((opening) => opening.items.map((item) => ({ catalogItemId: item.productId, quantity: item.quantity, openingName: opening.name }))), laborHours: draft.laborHours ?? 0, laborOverrideReason: draft.laborOverrideReason?.trim() ?? '', discountPercent: draft.discountPercent ?? 0 }
	};
}
