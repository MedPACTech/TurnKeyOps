import { fail } from '@sveltejs/kit';
import { thinkPinkTenant } from '$lib/config/tenants';
import { uploadQuoteRequestAttachments } from '$lib/server/quote-request-attachments';
import { submitQuoteRequest } from '$lib/server/quote-requests';
import type { Actions } from './$types';

const MAX_PHOTOS = 10;
const MAX_PHOTO_BYTES = 10 * 1024 * 1024;
const getValue = (data: FormData, key: string) => String(data.get(key) ?? '').trim();

export const load = ({ url }) => ({
	submitted: url.searchParams.get('submitted') === '1'
});

export const actions: Actions = {
	quote: async ({ request, fetch }) => {
		const data = await request.formData();
		const values = {
			name: getValue(data, 'name'),
			phone: getValue(data, 'phone'),
			email: getValue(data, 'email'),
			address: getValue(data, 'address'),
			acreage: getValue(data, 'acreage'),
			service: getValue(data, 'service'),
			timeline: getValue(data, 'timeline')
		};

		if (!values.name || !values.phone || !values.address) {
			return fail(400, {
				success: false,
				error: 'Name, phone, and property address are required.',
				values
			});
		}

		const photos = data.getAll('photos').filter((value): value is File => value instanceof File && value.size > 0);
		if (photos.length > MAX_PHOTOS) {
			return fail(400, {
				success: false,
				error: `Please attach ${MAX_PHOTOS} photos or fewer.`,
				values
			});
		}

		if (photos.some((photo) => photo.size > MAX_PHOTO_BYTES)) {
			return fail(400, {
				success: false,
				error: 'Each photo must be under 10 MB.',
				values
			});
		}

		try {
			const id = crypto.randomUUID();
			await submitQuoteRequest(fetch, {
				id,
				tenantId: thinkPinkTenant.id,
				companyName: values.name,
				contactName: values.name,
				email: values.email,
				phone: values.phone,
				siteName: values.address,
				serviceAddress: values.address,
				serviceType: values.service || 'Land clearing assessment',
				propertyType: values.acreage || 'Acreage needs confirmation',
				requestedTimeline: values.timeline || 'Needs follow-up',
				priority: values.timeline.toLowerCase().includes('urgent') ? 'emergency' : 'standard',
				need: [
					values.service || 'Land clearing assessment',
					values.acreage ? `Approximate acreage: ${values.acreage}` : '',
					photos.length ? `${photos.length} property photo${photos.length === 1 ? '' : 's'} attached.` : ''
				]
					.filter(Boolean)
					.join('. '),
				attachments: [],
				assignedTo: 'Think Pink intake',
				nextAction: 'Call the property owner and schedule an on-site assessment.',
				routingNote: 'Submitted from the Think Pink public website.'
			});
			await uploadQuoteRequestAttachments(fetch, thinkPinkTenant.id, id, photos);
		} catch (cause) {
			console.error('Think Pink quote request submission failed.', cause);
			return fail(502, {
				success: false,
				error: 'We could not send your request right now. Please try again in a moment.',
				values
			});
		}

		return { success: true, error: null, values: null };
	}
};
