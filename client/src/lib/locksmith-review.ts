import type { FieldEstimate } from './locksmith-handoff.ts';
import type { FieldDraft } from './locksmith-drafts.ts';

function canonical(value: unknown): string {
	if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`;
	if (value && typeof value === 'object') return `{${Object.entries(value).sort(([a], [b]) => a.localeCompare(b)).map(([key, item]) => `${JSON.stringify(key)}:${canonical(item)}`).join(',')}}`;
	return JSON.stringify(value) ?? 'null';
}

/** Issuance must not accidentally share an earlier scope while the technician sees newer local edits. */
export function fieldScopeMatchesEstimate(draft: FieldDraft, estimate: FieldEstimate): boolean {
	const pricing = estimate.locksmithPricing;
	if (!pricing?.openings || !pricing.lines || pricing.jobType !== draft.jobType) return false;
	const openings = draft.openings.map(({ items: _items, ...opening }) => opening);
	const items = draft.openings.flatMap((opening) => opening.items.map((item) => ({ catalogItemId: item.productId, openingName: opening.name, quantity: item.quantity })));
	const lines = pricing.lines.map((line) => ({ catalogItemId: line.catalogItemId, openingName: line.openingName, quantity: line.quantity }));
	const laborMatches = (draft.laborHours ?? 0) === 0 ? (pricing.standardLaborHours ?? 0) === (pricing.laborHours ?? 0) : draft.laborHours === pricing.laborHours;
	const reasonMatches = (draft.laborHours ?? 0) <= (pricing.standardLaborHours ?? 0) || (draft.laborOverrideReason?.trim() ?? '') === (pricing.laborOverrideReason ?? '');
	return canonical(openings) === canonical(pricing.openings) && canonical(items) === canonical(lines) && laborMatches && reasonMatches && (draft.discountPercent ?? 0) === (pricing.discountPercent ?? 0);
}

/** Only the issued packet's exact tenant review route may be opened or copied. */
export function issuedReviewPath(packet: FieldEstimate, now = Date.now()): string | null {
	if (packet.status !== 'sent' || !packet.delivery || !['sent', 'approved', 'changes-requested'].includes(packet.delivery.status)) return null;
	const expiry = Date.parse(packet.expiresAtUtc ?? '');
	if (!Number.isFinite(expiry) || expiry <= now) return null;
	try {
		const raw = packet.delivery.reviewUrl;
		if (!raw.startsWith('/carlzipf/estimate/')) return null;
		const url = new URL(raw, 'https://review.invalid');
		const token = url.searchParams.get('token');
		if (url.origin !== 'https://review.invalid' || url.pathname !== `/carlzipf/estimate/${packet.quoteRequestId}` || url.hash || url.searchParams.size !== 1 || !token || !/^[0-9a-f]{64}$/i.test(token)) return null;
		return `${url.pathname}?token=${token}`;
	} catch { return null; }
}
