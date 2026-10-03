import { fail, redirect } from '@sveltejs/kit';
import { carlZipfTenant } from '$lib/config/tenants';
import { uploadQuoteRequestAttachments } from '$lib/server/quote-request-attachments';
import { submitQuoteRequest } from '$lib/server/quote-requests';
import type { Actions } from './$types';

const value = (data: FormData, key: string) => String(data.get(key) ?? '').trim();
const services = ['replacement', 'hardware', 'repair', 'rekey'];
export const load = ({ url }) => ({
 submissionId: crypto.randomUUID(),
 submitted: url.searchParams.get('submitted') === '1',
 reference: (url.searchParams.get('reference') ?? '').replace(/[^A-F0-9]/gi, '').slice(0, 8)
});

export const actions: Actions = {
 quote: async ({ request, fetch }) => {
  const data = await request.formData();
  const candidate = value(data, 'submissionId');
  const submissionId = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(candidate) ? candidate : crypto.randomUUID();
  const values = Object.fromEntries(['name', 'company', 'email', 'phone', 'address', 'jobType', 'service', 'openings', 'details', 'preferredDate', 'preferredTime', 'requestMode'].map((key) => [key, value(data, key)]));
  // Older forms default to the existing assessment request workflow.
  values.requestMode ||= 'assessment';
  const invalid = (error: string) => fail(400, { success: false, error, values, submissionId });
  if (!['assessment', 'callback'].includes(values.requestMode)) return invalid('Choose an assessment request or a callback.');
  if (values.requestMode === 'callback') { values.preferredDate = ''; values.preferredTime = ''; }
  if (value(data, 'website')) return invalid('Please refresh the page and try again.');
  if (!values.name || !values.phone || !values.email || !values.address) return invalid('Please provide your name, email, phone, and service address.');
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email)) return invalid('Please enter a valid email address.');
  if (!['residential', 'commercial'].includes(values.jobType) || !services.includes(values.service)) return invalid('Please select a property type and service.');
  if (values.name.length > 200 || values.company.length > 200 || values.address.length > 500) return invalid('Keep names under 200 characters and the service address under 500 characters.');
  if (values.details.length > 3500) return invalid('Please keep your project details under 3,500 characters.');
  if (Object.values(values).some((entry) => entry.length > 4000)) return invalid('Please keep each field under 4,000 characters.');
  if (values.openings && (!/^\d+$/.test(values.openings) || Number(values.openings) < 1 || Number(values.openings) > 1000)) return invalid('Enter a number of openings between 1 and 1,000, or leave it blank.');
  if (values.preferredDate && (!/^\d{4}-\d{2}-\d{2}$/.test(values.preferredDate) || !Number.isFinite(Date.parse(values.preferredDate)) || new Date(values.preferredDate).toISOString().slice(0, 10) !== values.preferredDate)) return invalid('Please enter a valid preferred date.');
  if (!['', 'morning', 'afternoon', 'flexible'].includes(values.preferredTime)) return invalid('Please select a valid time preference.');
  const photos = data.getAll('photos').filter((entry): entry is File => entry instanceof File && entry.size > 0);
  if (photos.length > 10 || photos.some((photo) => photo.size > 10 * 1024 * 1024)) return invalid('Attach up to 10 photos, each no larger than 10 MB.');
  if (photos.reduce((total, photo) => total + photo.size, 0) > 45 * 1024 * 1024) return invalid('Keep the combined photo size under 45 MB.');
  if (photos.some((photo) => !['image/jpeg', 'image/png', 'image/webp'].includes(photo.type))) return invalid('Please use JPG, PNG, or WebP photos.');
  let durableRequestCreated = false;
  try {
   await submitQuoteRequest(fetch, {
    id: submissionId, tenantId: carlZipfTenant.id, companyName: values.company || values.name,
    contactName: values.name, email: values.email, phone: values.phone, siteName: values.address,
    serviceAddress: values.address, propertyType: values.jobType, serviceType: values.service,
    requestedTimeline: values.requestMode === 'callback' ? 'Callback requested before arranging an assessment' : [values.preferredDate, values.preferredTime].filter(Boolean).join(' / ') || 'Please call to arrange an assessment',
    priority: 'standard', attachments: [],
    need: [`Job type: ${values.jobType}`, `Service: ${values.service}`, values.openings ? `Openings: ${values.openings}` : 'Opening count to be assessed', values.details, values.requestMode === 'callback' ? 'Customer requests a callback before arranging an assessment. No appointment requested.' : 'Customer requests an assessment visit. Appointment preference only; office confirmation required.'].filter(Boolean).join('\n'),
    assignedTo: 'Office intake', nextAction: values.requestMode === 'callback' ? 'Call the customer to discuss their service request before arranging an assessment.' : 'Review the request and confirm an assessment appointment with an eligible technician.',
    routingNote: 'Carl Zipf public request. No appointment has been booked.'
   });
   durableRequestCreated = true;
   await uploadQuoteRequestAttachments(fetch, carlZipfTenant.id, submissionId, photos);
  } catch (cause) {
   console.error('Carl Zipf public intake failed', cause);
   const status = typeof cause === 'object' && cause !== null && 'status' in cause ? Number(cause.status) : 0;
   return fail(502, { success: false, values, submissionId, error: durableRequestCreated
    ? `Your request was saved as ${submissionId.slice(0, 8).toUpperCase()}, but photo upload was not confirmed. Reselect your photos and retry; the same request reference will be used.`
    : status === 429 ? 'Too many requests were sent from this network. Wait one minute and retry; your request reference will be reused.'
    : 'We could not confirm your request. Check your connection and retry, or call 614-299-7303. Your request reference will be reused to prevent duplicates.' });
  }
  throw redirect(303, `/carlzipf/public?submitted=1&reference=${submissionId.slice(0, 8).toUpperCase()}#request`);
 }
};
