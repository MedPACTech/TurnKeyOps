export type FieldJobType = 'residential' | 'commercial';
export const measurementFields = [
	['slabWidth', 'Door slab width'], ['slabHeight', 'Door slab height'],
	['thickness', 'Door thickness'], ['frameWidth', 'Frame / unit width'],
	['frameHeight', 'Frame / unit height'], ['roughWidth', 'Rough opening width'],
	['roughHeight', 'Rough opening height'], ['jambDepth', 'Jamb depth'],
	['backset', 'Backset'], ['boreDiameter', 'Bore diameter'], ['boreSpacing', 'Bore spacing']
] as const;
export type MeasurementKey = typeof measurementFields[number][0];
export type FieldOpening = {
	id: string;
	name: string;
	service: string;
	measurements: Record<MeasurementKey, { value: string; certainty: 'unknown' | 'measured' | 'estimated' }>;
	handing: string;
	notes: string;
	commercialNotes: string;
	items: { productId: string; quantity: number }[];
};
export type FieldRequestSource = {
	id: string; contactName: string; companyName: string; email: string; phone: string;
	siteName: string; serviceAddress: string; serviceType: string; propertyType: FieldJobType;
	requestedTimeline: string; priority: 'standard'; need: string; attachments: never[];
};
export type FieldDraft = {
	version: 1; jobType: FieldJobType; customer: string; site: string; openings: FieldOpening[];
	contactEmail?: string; contactPhone?: string; laborHours?: number; laborOverrideReason?: string; discountPercent?: number;
	handoff?: { requestId: string; source: FieldRequestSource; estimateVersion?: string; requestSavedAt?: string; estimateSavedAt?: string };
};
export function newOpening(): FieldOpening {
	return {
		id: crypto.randomUUID(), name: '', service: 'Door and frame replacement',
		measurements: Object.fromEntries(measurementFields.map(([key]) => [key, { value: '', certainty: 'unknown' }])) as FieldOpening['measurements'],
		handing: '', notes: '', commercialNotes: '', items: []
	};
}
export function draftStorageKey(tenantId: string, userId: string): string {
	return `turnkey:locksmith:draft:v1:${encodeURIComponent(tenantId)}:${encodeURIComponent(userId)}`;
}
export function fieldDraftErrors(draft: FieldDraft): string[] {
	return draft.openings.flatMap((opening, index) => {
		const label = opening.name.trim() || `Opening ${index + 1}`;
		const errors: string[] = [];
		for (const [key, title] of measurementFields) {
			const value = opening.measurements[key].value.trim();
			if (value && (!/^\d+(\.\d+)?$/.test(value) || !Number.isFinite(Number(value)) || Number(value) <= 0)) errors.push(`${label}: ${title} must be a positive measurement in decimal inches.`);
		}
		if (opening.items.some((item) => !Number.isInteger(item.quantity) || item.quantity < 1 || item.quantity > 999)) errors.push(`${label}: item quantities must be whole numbers from 1 to 999.`);
		return errors;
	});
}
export function readFieldDraft(raw: string, capabilities: FieldJobType[]): FieldDraft | null {
	try {
		const value = JSON.parse(raw);
		if (value.version !== 1 || !capabilities.includes(value.jobType) || typeof value.customer !== 'string' || typeof value.site !== 'string' || !Array.isArray(value.openings) || value.openings.length > 100) return null;
		if (['contactEmail', 'contactPhone'].some((key) => value[key] !== undefined && typeof value[key] !== 'string')) return null;
		if (['laborHours', 'discountPercent'].some((key) => value[key] !== undefined && (typeof value[key] !== 'number' || !Number.isFinite(value[key]) || value[key] < 0))) return null;
		if (value.laborOverrideReason !== undefined && (typeof value.laborOverrideReason !== 'string' || value.laborOverrideReason.length > 500)) return null;
		if (value.handoff !== undefined) {
			const handoff = value.handoff;
			if (!handoff || typeof handoff !== 'object' || typeof handoff.requestId !== 'string' || !handoff.source || typeof handoff.source !== 'object') return null;
			if (!['id', 'contactName', 'companyName', 'email', 'phone', 'siteName', 'serviceAddress', 'serviceType', 'requestedTimeline', 'need'].every((key) => typeof handoff.source[key] === 'string')) return null;
			if (handoff.source.id !== handoff.requestId || handoff.source.propertyType !== value.jobType || handoff.source.priority !== 'standard' || !Array.isArray(handoff.source.attachments) || handoff.source.attachments.length) return null;
			if (['estimateVersion', 'requestSavedAt', 'estimateSavedAt'].some((key) => handoff[key] !== undefined && typeof handoff[key] !== 'string')) return null;
		}
		for (const opening of value.openings) {
			if (!opening || !['id', 'name', 'service', 'handing', 'notes', 'commercialNotes'].every((key) => typeof opening[key] === 'string') || !Array.isArray(opening.items)) return null;
			if (!measurementFields.every(([key]) => typeof opening.measurements?.[key]?.value === 'string' && ['unknown', 'measured', 'estimated'].includes(opening.measurements[key].certainty))) return null;
			if (!opening.items.every((item: { productId?: unknown; quantity?: unknown }) => item && typeof item.productId === 'string' && Number.isInteger(item.quantity) && Number(item.quantity) > 0 && Number(item.quantity) <= 999)) return null;
		}
		return fieldDraftErrors(value).length ? null : value as FieldDraft;
	} catch { return null; }
}
