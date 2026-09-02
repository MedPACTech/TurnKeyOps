import { fail, redirect } from '@sveltejs/kit';
import { loadBdrSiteContent } from '$lib/server/bdr-site-content';
import { uploadQuoteRequestAttachments } from '$lib/server/quote-request-attachments';
import { submitQuoteRequest } from '$lib/server/quote-requests';
import type { QuoteRequestPriority } from '$lib/quote-requests';
import { bdrTenant } from '$lib/config/tenants';

const priorities = new Set<QuoteRequestPriority>(['standard', 'priority', 'emergency']);
const MAX_ATTACHMENTS = 10;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;

const getValue = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

const getAttachmentFiles = (formData: FormData): File[] =>
	formData
		.getAll('attachments')
		.filter((value): value is File => value instanceof File && value.size > 0);

export const load = async ({ fetch, url }) => {
	return {
		content: await loadBdrSiteContent(fetch),
		submitted: url.searchParams.get('submitted') === '1',
		submissionId: crypto.randomUUID(),
		reference: url.searchParams.get('reference')?.trim() ?? ''
	};
};

export const actions = {
	submitQuoteRequest: async ({ fetch, request }) => {
		const content = await loadBdrSiteContent(fetch);
		const configuredFields = content.quoteForm.fields;
		const formData = await request.formData();
		const requestedSubmissionId = getValue(formData, 'submissionId');
		const submissionId = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(requestedSubmissionId)
			? requestedSubmissionId
			: crypto.randomUUID();
		const website = getValue(formData, 'website');
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
		if (website) errors.form = 'The request could not be accepted. Refresh the page and try again.';
		if (attachmentFiles.length > MAX_ATTACHMENTS) errors.attachments = `Attach ${MAX_ATTACHMENTS} files or fewer.`;
		if (attachmentFiles.some((file) => file.size > MAX_ATTACHMENT_BYTES)) errors.attachments = 'Each attachment must be 10 MB or smaller.';

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
				values,
				submissionId
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

		let durableRequestCreated = false;
		try {
			const quoteRequestId = submissionId;
			await submitQuoteRequest(fetch, {
				id: quoteRequestId,
				tenantId: bdrTenant.id,
				website,
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
				attachments: [],
				assignedTo: content.quoteForm.queueDestination || 'Office intake',
				nextAction: content.quoteForm.notificationRecipients.length
					? `Notify ${content.quoteForm.notificationRecipients.join(', ')} and review submission.`
					: undefined,
				routingNote: routingNoteParts.join('. ') || undefined
			});
			durableRequestCreated = true;
			await uploadQuoteRequestAttachments(fetch, bdrTenant.id, quoteRequestId, attachmentFiles);
		} catch (cause) {
			console.error('Failed to submit quote request through API.', cause);
			const status = typeof cause === 'object' && cause !== null && 'status' in cause ? Number(cause.status) : 0;
			const timedOut = cause instanceof Error && (cause.name === 'TimeoutError' || cause.name === 'AbortError');
			const message = durableRequestCreated
				? `Your request was saved as ${submissionId.slice(0, 8).toUpperCase()}, but its attachments were not confirmed. Retry with the same files; the request will not be duplicated.`
				: status === 429
					? 'Too many requests were sent from this network. Wait one minute, then retry.'
					: status === 400
						? 'Some submission details were rejected. Review the highlighted fields and attachments, then retry.'
						: timedOut
							? 'The request timed out and was not confirmed. Check your connection and retry; duplicate prevention is enabled.'
							: 'BDR could not confirm a durable submission. Please retry; duplicate prevention is enabled.';
			return fail(502, {
				errors: {
					form: message
				},
				values,
				submissionId,
				durableRequestCreated
			});
		}

		throw redirect(303, `/bdr/public?submitted=1&reference=${encodeURIComponent(submissionId.slice(0, 8).toUpperCase())}#quote-request`);
	}
};
