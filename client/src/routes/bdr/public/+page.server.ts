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
		const content = await loadBdrSiteContent();
		const configuredFields = content.quoteForm.fields;
		const formData = await request.formData();
		const values: Record<string, string> = {};
		for (const field of configuredFields) {
			if (field.type !== 'file') {
				values[field.key] = getValue(formData, field.key);
			}
		}

		const contactName = values.contactName || values.companyName || 'Website lead';
		const companyName = values.companyName || values.contactName || 'Website lead';
		const email = values.email || '';
		const phone = values.phone || '';
		const siteName = values.siteName || companyName || contactName || 'Website lead';
		const serviceAddress = values.serviceAddress || 'Collected during follow-up';
		const serviceType = values.serviceType || 'General request';
		const propertyType = values.propertyType || 'Needs scoping';
		const requestedTimeline = values.requestedTimeline || 'Needs follow-up';
		const need = values.need || 'Project details to follow.';
		const priorityValue = values.priority as QuoteRequestPriority;
		const attachmentFiles = getAttachmentFiles(formData);
		const errors: Record<string, string> = {};

		for (const field of configuredFields) {
			if (field.type === 'file') {
				if (field.required && attachmentFiles.length === 0) {
					errors.attachments = `${field.label} is required.`;
				}
				continue;
			}

			const value = values[field.key] ?? '';
			if (field.required && !value) {
				errors[field.key] = `${field.label} is required.`;
				continue;
			}

			if (field.key === 'priority' && value && !priorities.has(value as QuoteRequestPriority)) {
				errors.priority = 'Priority must be standard, priority, or emergency.';
			}
		}

		if (Object.values(errors).some(Boolean)) {
			return fail(400, {
				errors,
				values
			});
		}

		const normalizedPriority = priorities.has(priorityValue) ? priorityValue : 'standard';
		const routingNoteParts = [];
		if (content.quoteForm.queueDestination) {
			routingNoteParts.push(`Queue destination: ${content.quoteForm.queueDestination}`);
		}
		if (content.quoteForm.notificationRecipients.length) {
			routingNoteParts.push(
				`Notification recipients: ${content.quoteForm.notificationRecipients.join(', ')}`
			);
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
				priority: normalizedPriority,
				need,
				attachments,
				assignedTo: content.quoteForm.queueDestination || 'Office intake',
				nextAction: content.quoteForm.notificationRecipients.length
					? `Notify ${content.quoteForm.notificationRecipients.join(', ')} and review submission.`
					: undefined,
				routingNote: routingNoteParts.join('. ') || undefined
			});
		} catch (cause) {
			console.error('Failed to submit quote request through API.', cause);
			return fail(502, {
				errors: {
					form: 'BDR could not submit your quote request right now. Please try again in a moment.'
				},
				values
			});
		}

		throw redirect(303, '/bdr/public?submitted=1#quote-request');
	}
};
