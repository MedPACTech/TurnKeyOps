import { parseQuoteSignatureInput } from '$lib/quote-signatures';
import { error, fail } from '@sveltejs/kit';
import { decidePublicQuoteEstimate, getPublicQuoteEstimate, type QuoteEstimate } from '$lib/server/quote-estimates';
import type { Actions, PageServerLoad } from './$types';

type LocksmithPricing = {
 jobType: string; subtotal: number; discountAmount: number; discountPercent: number; taxAmount: number; taxPercent: number; total: number; laborHours: number; laborRatePerHour: number;
 lines: { name: string; openingName: string; quantity: number; unitPrice: number; total: number }[];
 openings: { id: string; name: string; service: string; handing: string; notes: string; commercialNotes: string }[];
};
export const load: PageServerLoad = async ({ fetch, params, url, setHeaders }) => {
 setHeaders({ 'cache-control': 'private, no-store', 'referrer-policy': 'no-referrer', 'x-robots-tag': 'noindex, nofollow' });
 const token = url.searchParams.get('token')?.trim() ?? '';
 if (!token || !/^[0-9a-f-]{36}$/i.test(params.requestId)) throw error(404, 'This quote link is unavailable. Ask Carl Zipf Lock Shop for a current link.');
 try {
  const draft = await getPublicQuoteEstimate(fetch, 'carlzipf', params.requestId, token) as QuoteEstimate & { locksmithPricing?: LocksmithPricing };
  if (!draft.delivery || draft.quoteRequestId !== params.requestId) throw new Error('Unavailable quote');
  return { draft, accessToken: token };
 } catch { throw error(404, 'This quote link is unavailable or has expired. Call 614-299-7303 for a current link.'); }
};

export const actions: Actions = {
 approve: async ({ fetch, request, params }) => {
  const data = await request.formData();
  const parsed = parseQuoteSignatureInput(data);
  if (!parsed.signature) return fail(400, { decisionError: parsed.error, signerPrintedName: parsed.signerPrintedName });
  const token = String(data.get('accessToken') ?? '').trim();
  if (!token) return fail(400, { decisionError: 'Open the current quote link before approving.' });
  try {
   const saved = await decidePublicQuoteEstimate(fetch, 'carlzipf', params.requestId, token, 'approve', undefined, parsed.signature);
   if (saved.delivery?.status !== 'approved' || !saved.approvalSignature) throw new Error('Unconfirmed response');
   return { approved: true, approvalSignature: saved.approvalSignature };
  } catch { return fail(409, { signerPrintedName: parsed.signerPrintedName, decisionError: 'Approval was not confirmed. Reload this quote to check its latest status before retrying, or call 614-299-7303.' }); }
 },
 requestChanges: async ({ fetch, request, params }) => {
  const data = await request.formData();
  const token = String(data.get('accessToken') ?? '').trim();
  const responseNote = String(data.get('responseNote') ?? '').trim();
  if (!token || !responseNote || responseNote.length > 2000) return fail(400, { decisionError: 'Add a change request between 1 and 2,000 characters.', responseNote });
  try {
   const saved = await decidePublicQuoteEstimate(fetch, 'carlzipf', params.requestId, token, 'request-changes', responseNote);
   if (saved.delivery?.status !== 'changes-requested') throw new Error('Unconfirmed response');
   return { changesRequested: true };
  } catch { return fail(409, { decisionError: 'Your changes were not confirmed. Reload to check the latest status, or call 614-299-7303.', responseNote }); }
 }
};
