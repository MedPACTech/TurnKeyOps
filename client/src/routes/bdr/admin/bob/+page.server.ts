import { fail, redirect } from '@sveltejs/kit';
import { authTokenCookie, getAuthApiBaseUrl } from '$lib/server/auth-session';
import {
	bobVoiceCookie,
	bobVoiceOptions,
	normalizeBobVoice,
	type BobVoiceId
} from '$lib/bob-voice';
import {
	buildBobBriefing,
	buildEstimateFollowups,
	executeBobRecommendation
} from '$lib/server/bob-operations';
import { bdrEmployeeContacts } from '$lib/bdr-team';
import {
	advanceEstimateConversation,
	appendBobMessage,
	appendGeneralConversationExchange,
	bobHomeConversationId,
	createBobConversation,
	deleteBobConversation,
	ensureBobConversations,
	getBobConversation,
	getEstimateBuilderProgress,
	markEstimateConversationCreated,
	setBobConversationArchived,
	type BobConversation,
	type BobConversationMode,
	type BobEstimateDraft
} from '$lib/server/bob-conversations';
import {
	loadQuoteRequests,
	scheduleQuoteRequestSiteVisit,
	submitQuoteRequest,
	updateQuoteRequest
} from '$lib/server/quote-requests';

type BobAnalyzeResult = {
	intent: 'general' | 'start_estimate' | 'estimate_followup';
	confidence: number;
	answer: string;
	fields: Record<string, string>;
	suggestedReplies?: string[];
};

type BobAnalyzeEnvelope = {
	success?: boolean;
	data?: BobAnalyzeResult;
	errors?: Array<{ message?: string }>;
};

const formString = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

const analyzeWithBob = async ({
	fetch,
	token,
	message,
	mode,
	voice,
	estimate,
	context,
	conversation
}: {
	fetch: typeof globalThis.fetch;
	token: string;
	message: string;
	mode: BobConversationMode;
	voice: BobVoiceId;
	estimate?: BobEstimateDraft;
	context: unknown;
	conversation: BobConversation['messages'];
}) => {
	const response = await fetch(`${getAuthApiBaseUrl()}/api/bob/analyze`, {
		method: 'POST',
		headers: {
			Authorization: `Bearer ${token}`,
			'Content-Type': 'application/json'
		},
		body: JSON.stringify({
			message,
			mode,
			voice,
			estimate,
			context,
			conversation: conversation.slice(-12)
		})
	});
	const payload = (await response.json()) as BobAnalyzeEnvelope;
	if (!response.ok || !payload.data) {
		const detail = payload.errors?.map((item) => item.message).filter(Boolean).join(', ');
		throw new Error(detail || `Bob request failed with ${response.status}.`);
	}
	return payload.data;
};

const summarizeEstimateFollowups = (
	items: Awaited<ReturnType<typeof buildEstimateFollowups>>
) => {
	if (!items.length) {
		return 'There are no qualified, in-progress, drafted, or sent estimates waiting for a next action.';
	}
	const highPriority = items.filter((item) => item.priority === 'high').length;
	return `I found ${items.length} estimate${items.length === 1 ? '' : 's'} needing a next action${
		highPriority ? `, including ${highPriority} high-priority follow-up${highPriority === 1 ? '' : 's'}` : ''
	}. I ranked them by stage and time since the last recorded activity.`;
};

const voiceLegacyPipelineMessage = (content: string, voice: BobVoiceId) => {
	if (voice !== 'gruff') return content;

	if (
		content ===
		'I reviewed the live estimate pipeline and surfaced the records that need a next action.'
	) {
		return 'I went through the live estimate pipeline and pulled out the jobs somebody needs to get off their ass and move.';
	}

	const summary = content.match(
		/^I found (\d+) estimates? needing a next action(?:, including (\d+) high-priority follow-ups?)?\. I ranked them by stage and time since the last recorded activity\.$/
	);
	if (summary) {
		const total = Number(summary[1]);
		const highPriority = Number(summary[2] ?? 0);
		return `I found ${total} estimate${total === 1 ? '' : 's'} sitting there waiting for somebody to do their damn job${
			highPriority
				? `, and ${highPriority} ${highPriority === 1 ? 'is' : 'are'} high priority`
				: ''
		}. I put the biggest fires first, so quit screwing around and start at the top.`;
	}

	if (
		content ===
		'There are no qualified, in-progress, drafted, or sent estimates waiting for a next action.'
	) {
		return 'Nothing’s rotting in the estimate pipeline right now. Hell, enjoy it while it lasts.';
	}

	return content;
};

const inspectionIntent = /\b(schedule|book|set up|arrange|inspection|site visit)\b/i;
const slotStarts = ['09:00', '11:00', '13:30', '15:30'];
const addMinutes = (time: string, minutes: number) => {
	const [hours, currentMinutes] = time.split(':').map(Number);
	const total = hours * 60 + currentMinutes + minutes;
	return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
};
const localDate = (date: Date) =>
	`${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
const timeLabel = (value: string) => {
	const [hours, minutes] = value.split(':').map(Number);
	return new Date(2026, 0, 1, hours, minutes).toLocaleTimeString('en-US', {
		hour: 'numeric',
		minute: '2-digit'
	});
};
const dateLabel = (value: string, today: string) =>
	value === today
		? 'Today'
		: new Date(`${value}T12:00:00`).toLocaleDateString('en-US', {
				weekday: 'short',
				month: 'short',
				day: 'numeric'
			});
const rangesOverlap = (startA: string, endA: string, startB: string, endB: string) =>
	startA < endB && startB < endA;

const buildInspectionOffer = async (
	fetch: typeof globalThis.fetch,
	conversation: BobConversation,
	question: string,
	voice: BobVoiceId
) => {
	if (!inspectionIntent.test(question)) return null;
	const { requests } = await loadQuoteRequests(fetch);
	const conversationText = [...conversation.messages.slice(-6).map((message) => message.content), question]
		.join(' ')
		.toLowerCase();
	const candidates = requests
		.filter((request) => ['qualified', 'inspection-scheduled'].includes(request.status))
		.map((request) => {
			const names = [
				request.siteName,
				request.companyName,
				request.contactName,
				request.customerName
			]
				.filter(Boolean)
				.map((value) => String(value).toLowerCase());
			return {
				request,
				score: names.reduce(
					(score, name) => score + (name.length > 3 && conversationText.includes(name) ? name.length : 0),
					0
				)
			};
		})
		.sort((left, right) => right.score - left.score);
	const target = candidates[0]?.score ? candidates[0].request : null;
	if (!target) return null;

	const fieldResource =
		bdrEmployeeContacts
			.filter((employee) => employee.skills.includes('field-inspection'))
			.sort((left, right) => left.workload - right.workload)[0]?.displayName ??
		target.assignedTo ??
		'Field estimator';
	const now = new Date();
	const today = localDate(now);
	const actions: NonNullable<BobConversation['messages'][number]['actions']> = [];
	for (let dayOffset = 0; dayOffset < 4 && actions.length < 3; dayOffset += 1) {
		const date = new Date(now);
		date.setDate(now.getDate() + dayOffset);
		const visitDate = localDate(date);
		for (const windowStart of slotStarts) {
			if (actions.length >= 3) break;
			const windowEnd = addMinutes(windowStart, 90);
			if (
				dayOffset === 0 &&
				Number(windowStart.slice(0, 2)) * 60 + Number(windowStart.slice(3)) <=
					now.getHours() * 60 + now.getMinutes() + 60
			) {
				continue;
			}
			const conflict = requests.some(
				(request) =>
					request.id !== target.id &&
					request.siteVisitSchedule?.visitDate === visitDate &&
					request.siteVisitSchedule.assignedFieldResource === fieldResource &&
					rangesOverlap(
						windowStart,
						windowEnd,
						request.siteVisitSchedule.windowStart,
						request.siteVisitSchedule.windowEnd
					)
			);
			if (!conflict) {
				actions.push({
					kind: 'schedule-inspection',
					label: `${dateLabel(visitDate, today)} · ${timeLabel(windowStart)}`,
					requestId: target.id,
					visitDate,
					windowStart,
					windowEnd,
					assignedFieldResource: fieldResource
				});
			}
		}
	}
	actions.push({
		kind: 'open-calendar',
		label: 'Review calendar',
		href: `/bdr/admin/calendar?scheduleRequest=${encodeURIComponent(target.id)}`
	});
	const firstSlot = actions.find((action) => action.kind === 'schedule-inspection');
	const customer = target.siteName || target.companyName || target.contactName || target.customerName;
	const content =
		voice === 'gruff'
			? `I checked the damn calendar. ${fieldResource} can inspect ${customer} ${firstSlot && firstSlot.kind === 'schedule-inspection' ? `${firstSlot.label.toLowerCase()}` : 'at the next opening'}. Pick a time below, or open the calendar if your pansy ass needs something different.`
			: `I checked the calendar. ${fieldResource} can inspect ${customer} ${firstSlot && firstSlot.kind === 'schedule-inspection' ? firstSlot.label.toLowerCase() : 'at the next opening'}. Choose a time below, or review the calendar for another opening.`;
	return { target, content, actions };
};

export const load = async ({ fetch, url, cookies }) => {
	const [briefing, conversations, estimateFollowups] = await Promise.all([
		buildBobBriefing(fetch),
		ensureBobConversations(),
		buildEstimateFollowups(fetch)
	]);
	const storedConversation =
		conversations.find((conversation) => conversation.id === url.searchParams.get('conversation')) ??
		conversations.find((conversation) => conversation.id === bobHomeConversationId) ??
		conversations[0];
	const bobVoice = normalizeBobVoice(cookies.get(bobVoiceCookie));
	const voiceGreeting =
		bobVoiceOptions.find((option) => option.id === bobVoice)?.greeting ??
		bobVoiceOptions[0].greeting;
	const selectedConversation = {
		...storedConversation,
		messages: storedConversation.messages.map((message, index) => {
			if (index === 0 && message.role === 'bob' && storedConversation.id === bobHomeConversationId) {
				return { ...message, content: voiceGreeting };
			}
			return message.role === 'bob'
				? { ...message, content: voiceLegacyPipelineMessage(message.content, bobVoice) }
				: message;
		})
	};

	return {
		briefing,
		conversations,
		selectedConversation,
		bobVoice,
		estimateProgress: getEstimateBuilderProgress(selectedConversation.estimateDraft),
		estimateFollowups
	};
};

export const actions = {
	archiveConversation: async ({ request }) => {
		const formData = await request.formData();
		await setBobConversationArchived(formString(formData, 'conversationId'), true);
		throw redirect(303, '/bdr/admin/bob');
	},
	restoreConversation: async ({ request }) => {
		const formData = await request.formData();
		const conversationId = formString(formData, 'conversationId');
		await setBobConversationArchived(conversationId, false);
		throw redirect(303, `/bdr/admin/bob?conversation=${encodeURIComponent(conversationId)}`);
	},
	deleteConversation: async ({ request }) => {
		const formData = await request.formData();
		await deleteBobConversation(formString(formData, 'conversationId'));
		throw redirect(303, '/bdr/admin/bob');
	},
	ask: async ({ request, fetch, cookies }) => {
		const formData = await request.formData();
		const question = formString(formData, 'question');
		const conversationId = formString(formData, 'conversationId');
		if (!question) return fail(400, { action: 'ask', message: 'Ask Bob a question.', conversationId });

		const conversation = await getBobConversation(conversationId);
		const token = cookies.get(authTokenCookie);
		if (!token) {
			return fail(401, { action: 'ask', message: 'Sign in again before asking Bob.', conversationId });
		}

		try {
			const bobVoice = normalizeBobVoice(cookies.get(bobVoiceCookie));
			if (conversation.mode === 'estimate-followup') {
				const inspectionOffer = await buildInspectionOffer(fetch, conversation, question, bobVoice);
				if (inspectionOffer) {
					await appendBobMessage(conversation.id, 'user', question);
					await appendBobMessage(
						conversation.id,
						'bob',
						inspectionOffer.content,
						undefined,
						inspectionOffer.actions
					);
					throw redirect(
						303,
						`/bdr/admin/bob?conversation=${encodeURIComponent(conversation.id)}`
					);
				}
			}
			const briefing = await buildBobBriefing(fetch);
			const analysis = await analyzeWithBob({
				fetch,
				token,
				message: question,
				mode: conversation.mode,
				voice: bobVoice,
				estimate: conversation.estimateDraft,
				context: briefing.context,
				conversation: conversation.messages
			});

			if (conversation.mode === 'estimate-builder') {
				await advanceEstimateConversation(
					conversation.id,
					question,
					analysis.fields,
					analysis.answer
				);
				throw redirect(303, `/bdr/admin/bob?conversation=${encodeURIComponent(conversation.id)}`);
			}

			if (conversation.mode === 'estimate-followup') {
				const followups = await buildEstimateFollowups(fetch);
				await appendGeneralConversationExchange(
					conversation.id,
					question,
					analysis.answer || summarizeEstimateFollowups(followups),
					analysis.suggestedReplies
				);
				throw redirect(303, `/bdr/admin/bob?conversation=${encodeURIComponent(conversation.id)}`);
			}

			if (analysis.intent === 'start_estimate') {
				const estimateConversation = await createBobConversation('estimate-builder');
				if (Object.keys(analysis.fields).length) {
					await advanceEstimateConversation(
						estimateConversation.id,
						question,
						analysis.fields,
						analysis.answer
					);
				}
				throw redirect(
					303,
					`/bdr/admin/bob?conversation=${encodeURIComponent(estimateConversation.id)}`
				);
			}

			if (analysis.intent === 'estimate_followup') {
				const followupConversation = await createBobConversation('estimate-followup');
				const followups = await buildEstimateFollowups(fetch);
				await appendBobMessage(followupConversation.id, 'user', question);
				await appendBobMessage(
					followupConversation.id,
					'bob',
					analysis.answer || summarizeEstimateFollowups(followups),
					analysis.suggestedReplies
				);
				throw redirect(
					303,
					`/bdr/admin/bob?conversation=${encodeURIComponent(followupConversation.id)}`
				);
			}

			const targetConversation =
				conversation.id === bobHomeConversationId
					? await createBobConversation('general')
					: conversation;
			await appendGeneralConversationExchange(
				targetConversation.id,
				question,
				analysis.answer || 'I need a little more detail to help with that.',
				analysis.suggestedReplies
			);
			throw redirect(
				303,
				`/bdr/admin/bob?conversation=${encodeURIComponent(targetConversation.id)}`
			);
		} catch (cause) {
			if (cause && typeof cause === 'object' && 'status' in cause && cause.status === 303) throw cause;
			return fail(502, {
				action: 'ask',
				message: cause instanceof Error ? cause.message : 'Bob is unavailable right now.',
				conversationId
			});
		}
	},
	scheduleInspection: async ({ request, fetch, cookies }) => {
		const formData = await request.formData();
		const conversationId = formString(formData, 'conversationId');
		const requestId = formString(formData, 'requestId');
		const visitDate = formString(formData, 'visitDate');
		const windowStart = formString(formData, 'windowStart');
		const windowEnd = formString(formData, 'windowEnd');
		const assignedFieldResource = formString(formData, 'assignedFieldResource');
		const { requests } = await loadQuoteRequests(fetch);
		const target = requests.find((entry) => entry.id === requestId);
		if (!target) {
			return fail(404, {
				action: 'scheduleInspection',
				message: 'That inspection request is no longer available.',
				conversationId
			});
		}
		const conflict = requests.find(
			(entry) =>
				entry.id !== requestId &&
				entry.siteVisitSchedule?.visitDate === visitDate &&
				entry.siteVisitSchedule.assignedFieldResource === assignedFieldResource &&
				rangesOverlap(
					windowStart,
					windowEnd,
					entry.siteVisitSchedule.windowStart,
					entry.siteVisitSchedule.windowEnd
				)
		);
		if (conflict) {
			return fail(409, {
				action: 'scheduleInspection',
				message: 'That opening was just taken. Ask Bob to check the calendar again.',
				conversationId
			});
		}
		try {
			await scheduleQuoteRequestSiteVisit(fetch, {
				id: requestId,
				visitDate,
				windowStart,
				windowEnd,
				siteContact: target.contactName || target.customerName,
				siteContactPhone: target.phone,
				assignedFieldResource,
				notes: 'Inspection scheduled through Ask Bob.'
			});
			const voice = normalizeBobVoice(cookies.get(bobVoiceCookie));
			const customer = target.siteName || target.companyName || target.contactName || target.customerName;
			const confirmation =
				voice === 'gruff'
					? `Done. I scheduled ${customer} for ${dateLabel(visitDate, localDate(new Date())).toLowerCase()} at ${timeLabel(windowStart)} with ${assignedFieldResource}. Try not to screw up the easy part—show up.`
					: `Inspection scheduled for ${customer} on ${dateLabel(visitDate, localDate(new Date()))} at ${timeLabel(windowStart)} with ${assignedFieldResource}.`;
			await appendBobMessage(conversationId, 'bob', confirmation, undefined, [
				{
					kind: 'open-calendar',
					label: 'Open calendar',
					href: `/bdr/admin/calendar?scheduleRequest=${encodeURIComponent(requestId)}`
				}
			]);
			throw redirect(
				303,
				`/bdr/admin/bob?conversation=${encodeURIComponent(conversationId)}`
			);
		} catch (cause) {
			if (cause && typeof cause === 'object' && 'status' in cause && cause.status === 303) throw cause;
			return fail(500, {
				action: 'scheduleInspection',
				message: cause instanceof Error ? cause.message : 'Bob could not schedule that inspection.',
				conversationId
			});
		}
	},
	approve: async ({ request, fetch }) => {
		const formData = await request.formData();
		const recommendationId = formString(formData, 'recommendationId');
		const conversationId = formString(formData, 'conversationId');
		const briefing = await buildBobBriefing(fetch);
		const recommendation = briefing.recommendations.find((item) => item.id === recommendationId);
		if (!recommendation) {
			return fail(404, {
				action: 'approve',
				message: 'That recommendation is no longer current.',
				conversationId
			});
		}

		try {
			await executeBobRecommendation(fetch, recommendation);
			throw redirect(303, `/bdr/admin/bob?conversation=${encodeURIComponent(conversationId)}`);
		} catch (cause) {
			if (cause && typeof cause === 'object' && 'status' in cause && cause.status === 303) throw cause;
			return fail(500, {
				action: 'approve',
				message: cause instanceof Error ? cause.message : 'Bob could not complete the approved action.',
				conversationId
			});
		}
	},
	createEstimate: async ({ request, fetch }) => {
		const formData = await request.formData();
		const conversationId = formString(formData, 'conversationId');
		const conversation = await getBobConversation(conversationId);
		const draft = conversation.estimateDraft;
		const progress = getEstimateBuilderProgress(draft);
		if (conversation.mode !== 'estimate-builder' || !draft || !progress.isComplete) {
			return fail(400, {
				action: 'createEstimate',
				message: 'Finish the estimate interview before creating the draft.',
				conversationId
			});
		}

		if (draft.createdRequestId) {
			throw redirect(303, `/bdr/admin/estimates?request=${encodeURIComponent(draft.createdRequestId)}`);
		}

		const estimateRequest = await submitQuoteRequest(fetch, {
			companyName: /^residential$/i.test(draft.companyName) ? '' : draft.companyName,
			contactName: draft.contactName,
			email: draft.email,
			phone: draft.phone,
			siteName: draft.companyName || draft.contactName,
			serviceAddress: draft.serviceAddress,
			serviceType: draft.projectType,
			propertyType: /^residential$/i.test(draft.companyName) ? 'Residential' : 'Commercial',
			requestedTimeline: draft.timeline,
			priority: 'standard',
			need: `${draft.scope}\n\nMeasurements: ${draft.dimensions}\n\nDepth: ${draft.depth}\n\nNotes: ${draft.notes}`,
			attachments: [],
			assignedTo: 'Bob · office review',
			nextAction: 'Review quantities, pricing, margin, assumptions, and customer-ready terms.',
			routingNote: 'Internal estimate created through Bob Estimate Builder.'
		});

		await updateQuoteRequest(fetch, {
			id: estimateRequest.id,
			status: 'estimate-drafted',
			assignedTo: 'Bob · office review',
			nextAction: 'Review quantities, pricing, margin, assumptions, and customer-ready terms.',
			missingInfoReasonCodes: [],
			contactName: draft.contactName,
			email: draft.email,
			phone: draft.phone,
			siteName: draft.companyName || draft.contactName,
			serviceAddress: draft.serviceAddress,
			requestedTimeline: draft.timeline
		});
		await markEstimateConversationCreated(conversation.id, estimateRequest.id);
		throw redirect(303, `/bdr/admin/estimates?request=${encodeURIComponent(estimateRequest.id)}`);
	}
};
