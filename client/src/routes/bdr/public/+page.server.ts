import { fail, redirect } from '@sveltejs/kit';
import { loadBdrSiteContent } from '$lib/server/bdr-site-content';
import { uploadQuoteRequestAttachments } from '$lib/server/quote-request-attachments';
import { getQuoteRequestTenantId, submitQuoteRequest } from '$lib/server/quote-requests';
import type { QuoteRequestPriority } from '$lib/quote-requests';

const priorities = new Set<QuoteRequestPriority>(['standard', 'priority', 'emergency']);

const getValue = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

const getAttachmentFiles = (formData: FormData): File[] =>
	formData
		.getAll('attachments')
		.filter((value): value is File => value instanceof File && value.size > 0);

export const load = async ({ url }) => {
	return {
		content: await loadBdrSiteContent(),
		submitted: url.searchParams.get('submitted') === '1'
	};
};

export const actions = {
	submitQuoteRequest: async ({ fetch, request }) => {
		const formData = await request.formData();
		const contactName = getValue(formData, 'contactName');
		const companyName = getValue(formData, 'companyName') || contactName;
		const email = getValue(formData, 'email');
		const phone = getValue(formData, 'phone');
		const siteName = getValue(formData, 'siteName');
		const serviceAddress = getValue(formData, 'serviceAddress');
		const serviceType = getValue(formData, 'serviceType');
		const propertyType = getValue(formData, 'propertyType');
		const requestedTimeline = getValue(formData, 'requestedTimeline');
		const need = getValue(formData, 'need');
		const priorityValue = getValue(formData, 'priority') as QuoteRequestPriority;
		const attachmentFiles = getAttachmentFiles(formData);

		const errors = {
			contactName: contactName ? '' : 'Contact name is required.',
			email: email ? '' : 'Email is required.',
			phone: phone ? '' : 'Phone is required.',
			siteName: siteName ? '' : 'Site name is required.',
			serviceAddress: serviceAddress ? '' : 'Service address is required.',
			serviceType: serviceType ? '' : 'Service type is required.',
			propertyType: propertyType ? '' : 'Property type is required.',
			requestedTimeline: requestedTimeline ? '' : 'Requested timeline is required.',
			need: need ? '' : 'A short description of the need is required.',
			priority: priorities.has(priorityValue) ? '' : 'Priority is required.'
		};

		if (Object.values(errors).some(Boolean)) {
			return fail(400, {
				errors,
				values: {
					companyName,
					contactName,
					email,
					phone,
					siteName,
					serviceAddress,
					serviceType,
					propertyType,
					requestedTimeline,
					need,
					priority: priorityValue
				}
			});
		}

		try {
			const quoteRequestId = crypto.randomUUID();
			const attachments = await uploadQuoteRequestAttachments(
				getQuoteRequestTenantId(),
				quoteRequestId,
				attachmentFiles
			);

			await submitQuoteRequest(fetch, {
				id: quoteRequestId,
				companyName,
				contactName,
				email,
				phone,
				siteName,
				serviceAddress,
				serviceType,
				propertyType,
				requestedTimeline,
				priority: priorityValue,
				need,
				attachments
			});
		} catch (cause) {
			console.error('Failed to submit quote request through API.', cause);
			return fail(502, {
				errors: {
					form: 'BDR could not submit your quote request right now. Please try again in a moment.'
				},
				values: {
					companyName,
					contactName,
					email,
					phone,
					siteName,
					serviceAddress,
					serviceType,
					propertyType,
					requestedTimeline,
					need,
					priority: priorityValue
				}
			});
		}

		throw redirect(303, '/bdr/public?submitted=1#quote-request');
	}
};
