import { fail, redirect } from '@sveltejs/kit';
import { submitQuoteRequest } from '$lib/server/quote-requests';
import type { QuoteRequestPriority } from '$lib/quote-requests';

const priorities = new Set<QuoteRequestPriority>(['standard', 'priority', 'emergency']);

const getValue = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

export const load = async ({ url }) => {
	return {
		submitted: url.searchParams.get('submitted') === '1'
	};
};

export const actions = {
	submitQuoteRequest: async ({ fetch, request }) => {
		const formData = await request.formData();
		const customerName = getValue(formData, 'customerName');
		const email = getValue(formData, 'email');
		const phone = getValue(formData, 'phone');
		const serviceAddress = getValue(formData, 'serviceAddress');
		const projectType = getValue(formData, 'projectType');
		const propertyType = getValue(formData, 'propertyType');
		const preferredTimeline = getValue(formData, 'preferredTimeline');
		const message = getValue(formData, 'message');
		const priorityValue = getValue(formData, 'priority') as QuoteRequestPriority;

		const errors = {
			customerName: customerName ? '' : 'Name is required.',
			email: email ? '' : 'Email is required.',
			phone: phone ? '' : 'Phone is required.',
			serviceAddress: serviceAddress ? '' : 'Service address is required.',
			projectType: projectType ? '' : 'Project type is required.',
			propertyType: propertyType ? '' : 'Property type is required.',
			preferredTimeline: preferredTimeline ? '' : 'Preferred timing is required.',
			message: message ? '' : 'A short project description is required.',
			priority: priorities.has(priorityValue) ? '' : 'Priority is required.'
		};

		if (Object.values(errors).some(Boolean)) {
			return fail(400, {
				errors,
				values: {
					customerName,
					email,
					phone,
					serviceAddress,
					projectType,
					propertyType,
					preferredTimeline,
					message,
					priority: priorityValue
				}
			});
		}

		try {
			await submitQuoteRequest(fetch, {
				customerName,
				email,
				phone,
				serviceAddress,
				projectType,
				propertyType,
				preferredTimeline,
				priority: priorityValue,
				message
			});
		} catch (cause) {
			console.error('Failed to submit quote request through API.', cause);
			return fail(502, {
				errors: {
					form: 'BDR could not submit your quote request right now. Please try again in a moment.'
				},
				values: {
					customerName,
					email,
					phone,
					serviceAddress,
					projectType,
					propertyType,
					preferredTimeline,
					message,
					priority: priorityValue
				}
			});
		}

		throw redirect(303, '/bdr/public?submitted=1#quote-request');
	}
};
