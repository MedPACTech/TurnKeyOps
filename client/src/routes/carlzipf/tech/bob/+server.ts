import { error, json } from '@sveltejs/kit';
import { carlZipfTenant } from '$lib/config/tenants';
import { readFieldDraft } from '$lib/locksmith-drafts';
import { fieldApi, fieldResponse, requireFieldOrigin } from '../field-api.server';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async (event) => {
	requireFieldOrigin(event);
	const { call, context } = await fieldApi(event);
	const raw = await event.request.text();
	if (raw.length > 250_000) throw error(413, 'This field context is too large for Bob. Shorten the notes.');
	let input: { question?: unknown; draft?: unknown };
	try { input = JSON.parse(raw) as typeof input; }
	catch { throw error(400, 'Ask Bob from a valid field draft.'); }
	const question = typeof input.question === 'string' ? input.question.trim() : '';
	if (!question || question.length > 2000) throw error(400, 'Ask Bob a question under 2,000 characters.');
	const draft = input.draft ? readFieldDraft(JSON.stringify(input.draft), context.capabilities) : null;
	if (input.draft && !draft) throw error(400, 'The field draft could not be used as AI context. Review its job type and measurements.');
	if (!draft?.openings.length && /measure|opening|hardware|fit|summari[sz]e/i.test(question))
		return json({ answer: 'This draft has no opening yet, so I cannot assess its measurements or hardware fit. Add a named opening, record what you can measure, and ask me again. Mark inaccessible dimensions as unknown; confirm the selected product’s requirements before ordering.' }, { headers: { 'Cache-Control': 'private, no-store' } });
	const openingContext = draft?.openings.slice(0, 20).map((opening) => ({
		name: opening.name, service: opening.service, handing: opening.handing, notes: opening.notes,
		commercialNotes: opening.commercialNotes, measurements: opening.measurements,
		selectedCatalogItemIds: opening.items.map((item) => item.productId)
	}));
	const response = await fieldResponse<{ answer: string }>(await call('/api/bob/respond', { method: 'POST', body: JSON.stringify({
		question, voice: 'practical', context: {
			tenant: { id: carlZipfTenant.id, name: carlZipfTenant.name, trade: carlZipfTenant.tradeLabel,
				services: carlZipfTenant.services, instructions: carlZipfTenant.bobContext },
			jobType: draft?.jobType ?? null, openings: openingContext ?? [],
			instructions: 'Use only the supplied draft facts. State explicitly when no opening, measurement, or hardware item was supplied. Help the technician identify missing opening measurements, handing observations, and hardware questions. Treat estimated dimensions as unverified. Never claim an item is selected unless its ID is supplied. Never invent fit, code compliance, labor rate, catalog price, tax, appointment availability, or a customer-ready quote. The shared quote and calendar workflows make those decisions.'
		}
	}) }));
	return json(response, { headers: { 'Cache-Control': 'private, no-store' } });
};
