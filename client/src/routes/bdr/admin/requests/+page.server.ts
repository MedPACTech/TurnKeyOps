import { fail } from '@sveltejs/kit';
import {
	buildQuoteRequestQualification,
	buildQuoteRequestInbox,
	getQuoteRequestMetrics,
	isQuoteRequestSiteVisitCancellationReasonCode,
	isQuoteRequestClosedStatus,
	isQuoteRequestMissingInfoReasonCode,
	quoteRequestStatuses,
	type QuoteRequestMissingInfoReasonCode,
	type QuoteRequestStatus
} from '$lib/quote-requests';
import {
	cancelQuoteRequestSiteVisit,
	loadQuoteRequests,
	scheduleQuoteRequestSiteVisit,
	updateQuoteRequest
} from '$lib/server/quote-requests';

const buildScheduleSiteVisitHref = (requestId: string) =>
	`/bdr/admin/calendar?scheduleRequest=${encodeURIComponent(requestId)}`;

const buildServiceAddress = (formData: FormData) => {
	const address1 = String(formData.get('address1') ?? '').trim();
	const address2 = String(formData.get('address2') ?? '').trim();
	const city = String(formData.get('city') ?? '').trim();
	const state = String(formData.get('state') ?? '').trim();
	const postalCode = String(formData.get('postalCode') ?? '').trim();
	const cityStateZip = [city, [state, postalCode].filter(Boolean).join(' ')].filter(Boolean).join(', ');
	return [address1, address2, cityStateZip].filter(Boolean).join(', ');
};

const parseTimeToMinutes = (value: string) => {
	const match = /^(\d{2}):(\d{2})$/.exec(value);
	if (!match) return null;
	const hours = Number(match[1]);
	const minutes = Number(match[2]);
	if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return null;
	return hours * 60 + minutes;
};

const formatTimeLabel = (value: string) => {
	const minutes = parseTimeToMinutes(value);
	if (minutes == null) return value;
	const hours = Math.floor(minutes / 60);
	const remainder = minutes % 60;
	return new Date(2026, 0, 1, hours, remainder).toLocaleTimeString('en-US', {
		hour: 'numeric',
		minute: '2-digit'
	});
};

const formatDateLabel = (value: string) =>
	new Date(`${value}T12:00:00`).toLocaleDateString('en-US', {
		month: 'short',
		day: 'numeric'
	});

const rangesOverlap = (startA: number, endA: number, startB: number, endB: number) => startA < endB && startB < endA;

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch);
	const inbox = buildQuoteRequestInbox(requests);

	return {
		requests: inbox,
		metrics: getQuoteRequestMetrics(inbox),
		source,
		scheduleSiteVisitBaseHref: '/bdr/admin/calendar',
		scheduleSiteVisitByRequestId: Object.fromEntries(
			inbox.map((request) => [request.id, buildScheduleSiteVisitHref(request.id)])
		)
	};
};

export const actions = {
	updateRequest: async ({ fetch, request }) => {
		const formData = await request.formData();
		const id = String(formData.get('id') ?? '').trim();
		const status = String(formData.get('status') ?? '').trim() as QuoteRequestStatus;
		const assignedTo = String(formData.get('assignedTo') ?? '').trim();
		const nextAction = String(formData.get('nextAction') ?? '').trim();
		const contactName = String(formData.get('contactName') ?? '').trim();
		const email = String(formData.get('email') ?? '').trim();
		const phone = String(formData.get('phone') ?? '').trim();
		const siteName = String(formData.get('siteName') ?? '').trim();
		const requestedTimeline = String(formData.get('requestedTimeline') ?? '').trim();
		const serviceAddress = buildServiceAddress(formData);
		const missingInfoReasonCodes = formData
			.getAll('missingInfoReasonCodes')
			.map((value) => String(value).trim())
			.filter(isQuoteRequestMissingInfoReasonCode) as QuoteRequestMissingInfoReasonCode[];

		if (!id || !quoteRequestStatuses.includes(status)) {
			return fail(400, { message: 'Valid request id and status are required.', updatedRequestId: id });
		}

		if (status === 'needs-info' && missingInfoReasonCodes.length === 0) {
			return fail(400, { message: 'Choose at least one Needs Info reason code before saving.', updatedRequestId: id });
		}

		try {
			await updateQuoteRequest(fetch, {
				id,
				status,
				assignedTo,
				nextAction,
				missingInfoReasonCodes,
				contactName,
				email,
				phone,
				siteName,
				serviceAddress,
				requestedTimeline
			});
		} catch (cause) {
			console.error('Failed to persist quote request update through API.', cause);
			return fail(502, { message: 'Could not save the quote request update to the API.', updatedRequestId: id });
		}

		return { success: true, updatedRequestId: id };
	},
	scheduleSiteVisit: async ({ fetch, request }) => {
		const formData = await request.formData();
		const id = String(formData.get('id') ?? '').trim();
		const visitDate = String(formData.get('visitDate') ?? '').trim();
		const windowStart = String(formData.get('windowStart') ?? '').trim();
		const windowEnd = String(formData.get('windowEnd') ?? '').trim();
		const siteContact = String(formData.get('siteContact') ?? '').trim();
		const siteContactPhone = String(formData.get('siteContactPhone') ?? '').trim();
		const assignedFieldResource = String(formData.get('assignedFieldResource') ?? '').trim();
		const notes = String(formData.get('notes') ?? '').trim();

		if (!id) {
			return fail(400, { scheduleMessage: 'A valid quote request is required before scheduling.', scheduledRequestId: id });
		}

		if (!visitDate || !windowStart || !windowEnd || !siteContact || !assignedFieldResource) {
			return fail(400, {
				scheduleMessage: 'Visit date, time window, site contact, and assigned field resource are required.',
				scheduledRequestId: id
			});
		}

		const startMinutes = parseTimeToMinutes(windowStart);
		const endMinutes = parseTimeToMinutes(windowEnd);
		if (startMinutes == null || endMinutes == null || endMinutes <= startMinutes) {
			return fail(400, {
				scheduleMessage: 'Choose a valid site visit window with an end time after the start time.',
				scheduledRequestId: id
			});
		}

		const { requests } = await loadQuoteRequests(fetch);
		const selectedRequest = requests.find((entry) => entry.id === id);
		if (!selectedRequest) {
			return fail(404, { scheduleMessage: 'Quote request record was not found.', scheduledRequestId: id });
		}

		const qualification = buildQuoteRequestQualification(selectedRequest);
		if (!qualification.isQualified && selectedRequest.status !== 'inspection-scheduled') {
			return fail(400, {
				scheduleMessage: `Clear qualification blockers before scheduling: ${qualification.blockerLabels.join(' · ')}.`,
				scheduledRequestId: id
			});
		}

		if (isQuoteRequestClosedStatus(selectedRequest.status)) {
			return fail(400, {
				scheduleMessage: 'Closed or won requests cannot be scheduled for a new site visit.',
				scheduledRequestId: id
			});
		}

		const conflictingRequest = requests.find((candidate) => {
			if (candidate.id === id || !candidate.siteVisitSchedule) return false;
			if (candidate.siteVisitSchedule.visitDate !== visitDate) return false;
			if (candidate.siteVisitSchedule.assignedFieldResource.trim().toLowerCase() !== assignedFieldResource.trim().toLowerCase()) {
				return false;
			}

			const candidateStart = parseTimeToMinutes(candidate.siteVisitSchedule.windowStart);
			const candidateEnd = parseTimeToMinutes(candidate.siteVisitSchedule.windowEnd);
			if (candidateStart == null || candidateEnd == null) return false;
			return rangesOverlap(startMinutes, endMinutes, candidateStart, candidateEnd);
		});

		if (conflictingRequest?.siteVisitSchedule) {
			return fail(409, {
				scheduleMessage: `${assignedFieldResource} is already booked on ${formatDateLabel(visitDate)} from ${formatTimeLabel(conflictingRequest.siteVisitSchedule.windowStart)} to ${formatTimeLabel(conflictingRequest.siteVisitSchedule.windowEnd)} for ${conflictingRequest.customerName}.`,
				scheduledRequestId: id
			});
		}

		try {
			await scheduleQuoteRequestSiteVisit(fetch, {
				id,
				visitDate,
				windowStart,
				windowEnd,
				siteContact,
				siteContactPhone,
				assignedFieldResource,
				notes
			});
		} catch (cause) {
			console.error('Failed to persist site visit scheduling through API.', cause);
			return fail(502, { scheduleMessage: 'Could not save the site visit schedule to the API.', scheduledRequestId: id });
		}

		return { scheduleSuccess: true, scheduledRequestId: id };
	},
	cancelSiteVisit: async ({ fetch, request }) => {
		const formData = await request.formData();
		const id = String(formData.get('id') ?? '').trim();
		const reasonCode = String(formData.get('cancellationReasonCode') ?? '').trim();
		const notes = String(formData.get('cancellationNotes') ?? '').trim();

		if (!id) {
			return fail(400, {
				cancelMessage: 'A valid quote request is required before cancelling the site visit.',
				cancelledRequestId: id
			});
		}

		if (!isQuoteRequestSiteVisitCancellationReasonCode(reasonCode)) {
			return fail(400, {
				cancelMessage: 'Choose a cancellation reason code before removing the site visit from the queue.',
				cancelledRequestId: id
			});
		}

		try {
			await cancelQuoteRequestSiteVisit(fetch, {
				id,
				reasonCode,
				notes
			});
		} catch (cause) {
			console.error('Failed to persist site visit cancellation through API.', cause);
			return fail(502, {
				cancelMessage: 'Could not save the site visit cancellation to the API.',
				cancelledRequestId: id
			});
		}

		return { cancelSuccess: true, cancelledRequestId: id };
	}
};
