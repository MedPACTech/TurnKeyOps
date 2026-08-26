import { getAuthApiBaseUrl } from './auth-session';

export type BobPersistenceContext = {
	fetch: typeof globalThis.fetch;
	token: string;
};

type ApiEnvelope<T> = { success?: boolean; data?: T; errors?: Array<{ message?: string }> };
type StoredChat = {
	id: string;
	title: string;
	mode: BobConversationMode;
	stateJson: string;
	dateCreated?: string;
	dateUpdated?: string;
	archivedAtUtc?: string;
};
type StoredMessage = {
	id: string;
	role: string;
	content: string;
	metadataJson: string;
	idempotencyKey: string;
	dateCreated?: string;
};

export type BobConversationMode = 'general' | 'estimate-builder' | 'estimate-followup';
export type BobMessage = {
	id: string;
	role: 'user' | 'bob';
	content: string;
	createdAtUtc: string;
	suggestedReplies?: string[];
	actions?: BobMessageAction[];
};

export type BobMessageAction =
	| {
			kind: 'schedule-inspection';
			label: string;
			requestId: string;
			visitDate: string;
			windowStart: string;
			windowEnd: string;
			assignedFieldResource: string;
	  }
	| { kind: 'open-calendar'; label: string; href: string };

export type BobEstimateDraft = {
	contactName: string;
	companyName: string;
	email: string;
	phone: string;
	serviceAddress: string;
	projectType: string;
	scope: string;
	dimensions: string;
	depth: string;
	timeline: string;
	notes: string;
	createdRequestId?: string;
};

export type BobConversation = {
	id: string;
	title: string;
	mode: BobConversationMode;
	createdAtUtc: string;
	updatedAtUtc: string;
	messages: BobMessage[];
	estimateDraft?: BobEstimateDraft;
	archivedAtUtc?: string;
};

const emptyEstimateDraft = (): BobEstimateDraft => ({
	contactName: '',
	companyName: '',
	email: '',
	phone: '',
	serviceAddress: '',
	projectType: '',
	scope: '',
	dimensions: '',
	depth: '',
	timeline: '',
	notes: ''
});

const estimateQuestions: Array<{
	key: keyof BobEstimateDraft;
	prompt: string;
	suggestedReplies?: string[];
}> = [
	{ key: 'contactName', prompt: 'Who is the customer or primary contact?' },
	{
		key: 'companyName',
		prompt: 'Is this for a company or property name? Say “residential” if not.',
		suggestedReplies: ['Residential']
	},
	{ key: 'email', prompt: 'What email should the estimate be tied to?' },
	{ key: 'phone', prompt: 'What is the best phone number for the customer?' },
	{ key: 'serviceAddress', prompt: 'What is the full job-site address?' },
	{ key: 'projectType', prompt: 'What kind of work are we estimating?' },
	{
		key: 'scope',
		prompt:
			'Describe the scope, including demolition, prep, finish, access, and anything the customer specifically requested.'
	},
	{
		key: 'dimensions',
		prompt:
			'What measurements or quantities do we have? Include length, width, square footage, or concrete yards if known.'
	},
	{
		key: 'depth',
		prompt: 'What thickness or depth should the estimate use?',
		suggestedReplies: ['4 inches', '5 inches', '6 inches']
	},
	{
		key: 'timeline',
		prompt: 'When does the customer want the work completed?',
		suggestedReplies: ['No firm deadline']
	},
	{
		key: 'notes',
		prompt:
			'Any final assumptions, exclusions, site constraints, or internal notes? Say “none” if there are no additional notes.',
		suggestedReplies: ['None']
	}
];
const landClearingEstimateQuestions: typeof estimateQuestions = [
	{ key: 'contactName', prompt: 'Who is the customer or primary contact?' },
	{
		key: 'companyName',
		prompt: 'Is this for a company or property name? Say “residential” if not.',
		suggestedReplies: ['Residential']
	},
	{ key: 'email', prompt: 'What email should the estimate be tied to?' },
	{ key: 'phone', prompt: 'What is the best phone number for the customer?' },
	{ key: 'serviceAddress', prompt: 'What is the full property address?' },
	{ key: 'projectType', prompt: 'What service are we estimating?', suggestedReplies: ['Land clearing', 'Tree removal', 'Forestry mulching'] },
	{
		key: 'scope',
		prompt: 'Describe the requested work, including what must be cleared or removed and the desired finished condition.'
	},
	{
		key: 'dimensions',
		prompt: 'What site quantities do we know? Include acreage, vegetation density, tree count and diameter, or trail length.'
	},
	{
		key: 'depth',
		prompt: 'What should we know about terrain, equipment access, hauling or disposal, grading, and restoration?'
	},
	{
		key: 'timeline',
		prompt: 'When does the customer want the work completed?',
		suggestedReplies: ['No firm deadline']
	},
	{
		key: 'notes',
		prompt: 'Any final assumptions, exclusions, hazards, permits, utilities, or internal notes? Say “none” if not.',
		suggestedReplies: ['None']
	}
];
const questionsForTenant = (tenantSlug: string) =>
	tenantSlug === 'thinkpink' ? landClearingEstimateQuestions : estimateQuestions;

const makeId = () => globalThis.crypto.randomUUID();

export const bobHomeConversationId = 'bob-home';

const defaultConversation = (): BobConversation => {
	const now = new Date().toISOString();
	return {
		id: bobHomeConversationId,
		title: 'Ask Bob',
		mode: 'general',
		createdAtUtc: now,
		updatedAtUtc: now,
		messages: [
			{
				id: makeId(),
				role: 'bob',
				content:
					'What would you like to work on? Tell me in your own words, or choose one of the common starting points below.',
				createdAtUtc: now
			}
		]
	};
};

const normalizeEstimateConversation = (conversation: BobConversation, tenantSlug = 'bdr'): BobConversation => {
	if (conversation.mode !== 'estimate-builder') return conversation;
	const firstQuestion = questionsForTenant(tenantSlug)[0].prompt;
	let messages = [...conversation.messages];
	if (
		messages[0]?.role === 'bob' &&
		messages[0].content.startsWith('Let’s build the internal estimate.') &&
		messages[1]?.role === 'bob' &&
		messages[1].content === firstQuestion
	) {
		messages = [
			{ ...messages[0], content: `${messages[0].content}\n\n${firstQuestion}` },
			...messages.slice(2)
		];
	}
	messages = messages.filter((message, index, allMessages) => {
		if (
			message.role === 'user' &&
			/^start (?:a )?(?:new )?estimate$/i.test(message.content.trim()) &&
			allMessages[index + 1]?.role === 'bob' &&
			allMessages[index + 1].content.startsWith(
				'I did not find a new estimate detail in that response.'
			)
		) {
			return false;
		}
		if (
			message.role === 'bob' &&
			message.content.startsWith('I did not find a new estimate detail in that response.') &&
			allMessages[index - 1]?.role === 'user' &&
			/^start (?:a )?(?:new )?estimate$/i.test(allMessages[index - 1].content.trim())
		) {
			return false;
		}
		return true;
	});
	return { ...conversation, messages };
};

const requestStore = async <T>(
	context: BobPersistenceContext,
	path: string,
	init?: RequestInit
): Promise<T> => {
	if (!context.token) throw new Error('A valid Bob session is required.');
	const response = await context.fetch(`${getAuthApiBaseUrl()}/api${path}`, {
		...init,
		headers: {
			Authorization: `Bearer ${context.token}`,
			Accept: 'application/json',
			...(init?.body ? { 'Content-Type': 'application/json' } : {}),
			...init?.headers
		}
	});
	if (response.status === 204) return undefined as T;
	const payload = (await response.json()) as ApiEnvelope<T>;
	if (!response.ok || payload.success === false || payload.data === undefined) {
		const detail = payload.errors?.map((item) => item.message).filter(Boolean).join(', ');
		throw new Error(detail || `Bob persistence request failed with ${response.status}.`);
	}
	return payload.data;
};

const parseJson = <T>(value: string | undefined, fallback: T): T => {
	try {
		return value ? (JSON.parse(value) as T) : fallback;
	} catch {
		return fallback;
	}
};

const listStoredChats = (context: BobPersistenceContext) =>
	requestStore<StoredChat[]>(context, '/chats');
const listStoredMessages = (context: BobPersistenceContext, chatId: string) =>
	requestStore<StoredMessage[]>(context, `/chats/${encodeURIComponent(chatId)}/messages`);

export const loadBobConversations = async (
	tenantSlug: string,
	context: BobPersistenceContext
): Promise<BobConversation[]> => {
	const chats = await listStoredChats(context);
	const conversations = await Promise.all(chats.map(async (chat) => {
		const messages = await listStoredMessages(context, chat.id);
		const state = parseJson<{ estimateDraft?: BobEstimateDraft }>(chat.stateJson, {});
		return normalizeEstimateConversation({
			id: chat.id,
			title: chat.title,
			mode: chat.mode,
			createdAtUtc: chat.dateCreated ?? new Date().toISOString(),
			updatedAtUtc: chat.dateUpdated ?? new Date().toISOString(),
			archivedAtUtc: chat.archivedAtUtc,
			estimateDraft: state.estimateDraft,
			messages: messages.map((message) => {
				const metadata = parseJson<Pick<BobMessage, 'suggestedReplies' | 'actions'>>(message.metadataJson, {});
				return {
					id: message.idempotencyKey || message.id,
					role: message.role === 'assistant' ? 'bob' : 'user',
					content: message.content,
					createdAtUtc: message.dateCreated ?? new Date().toISOString(),
					...metadata
				};
			})
		}, tenantSlug);
	}));
	return [defaultConversation(), ...conversations];
};

const saveBobConversations = async (
	conversations: BobConversation[],
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const desired = conversations.filter((conversation) => conversation.id !== bobHomeConversationId);
	const stored = await listStoredChats(context);
	const storedById = new Map(stored.map((chat) => [chat.id, chat]));
	for (const conversation of desired) {
		const body = JSON.stringify({
			id: conversation.id,
			title: conversation.title,
			mode: conversation.mode,
			stateJson: JSON.stringify({ estimateDraft: conversation.estimateDraft }),
			archived: Boolean(conversation.archivedAtUtc)
		});
		if (storedById.has(conversation.id)) {
			await requestStore<StoredChat>(context, `/chats/${encodeURIComponent(conversation.id)}`, {
				method: 'PUT',
				body
			});
		} else {
			await requestStore<StoredChat>(context, '/chats', { method: 'POST', body });
		}

		const storedMessages = storedById.has(conversation.id)
			? await listStoredMessages(context, conversation.id)
			: [];
		const messageKeys = new Set(storedMessages.map((message) => message.idempotencyKey || message.id));
		for (const message of conversation.messages.filter((item) => !messageKeys.has(item.id))) {
			await requestStore<StoredMessage>(
				context,
				`/chats/${encodeURIComponent(conversation.id)}/messages/append`,
				{
					method: 'POST',
					body: JSON.stringify({
						role: message.role === 'bob' ? 'assistant' : 'user',
						content: message.content,
						metadataJson: JSON.stringify({
							suggestedReplies: message.suggestedReplies,
							actions: message.actions
						}),
						idempotencyKey: message.id
					})
				}
			);
		}
	}

	const desiredIds = new Set(desired.map((conversation) => conversation.id));
	for (const chat of stored.filter((item) => !desiredIds.has(item.id))) {
		await requestStore<void>(context, `/chats/${encodeURIComponent(chat.id)}`, { method: 'DELETE' });
	}
};

export const ensureBobConversations = async (tenantSlug: string, context: BobPersistenceContext) =>
	loadBobConversations(tenantSlug, context);

export const createBobConversation = async (
	mode: BobConversationMode,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const conversations = await ensureBobConversations(tenantSlug, context);
	const questions = questionsForTenant(tenantSlug);
	const now = new Date().toISOString();
	const title =
		mode === 'estimate-builder'
			? 'New estimate'
			: mode === 'estimate-followup'
				? 'Estimate follow-up'
				: 'New conversation';
	const introduction =
		mode === 'estimate-builder'
			? `Let’s build the internal estimate. Tell me what you already know; I’ll capture every useful detail and ask only for what is still missing.\n\n${questions[0].prompt}`
			: mode === 'estimate-followup'
				? 'I reviewed the live estimate pipeline and surfaced the records that need a next action.'
				: 'What would you like to work on? Tell me in your own words, or choose a starting point below.';
	const conversation: BobConversation = {
		id: makeId(),
		title,
		mode,
		createdAtUtc: now,
		updatedAtUtc: now,
		messages: [
			{ id: makeId(), role: 'bob', content: introduction, createdAtUtc: now }
		],
		estimateDraft: mode === 'estimate-builder' ? emptyEstimateDraft() : undefined
	};
	await saveBobConversations([conversation, ...conversations], tenantSlug, context);
	return conversation;
};

export const getBobConversation = async (
	id: string | null | undefined,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const conversations = await ensureBobConversations(tenantSlug, context);
	return conversations.find((conversation) => conversation.id === id) ?? conversations[0];
};

export const setBobConversationArchived = async (
	conversationId: string,
	archived: boolean,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	if (conversationId === bobHomeConversationId) return;
	const conversations = await ensureBobConversations(tenantSlug, context);
	const next = conversations.map((conversation) =>
		conversation.id === conversationId
			? {
					...conversation,
					updatedAtUtc: new Date().toISOString(),
					archivedAtUtc: archived ? new Date().toISOString() : undefined
				}
			: conversation
	);
	await saveBobConversations(next, tenantSlug, context);
};

export const deleteBobConversation = async (
	conversationId: string,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	if (conversationId === bobHomeConversationId) return;
	const conversations = await ensureBobConversations(tenantSlug, context);
	await saveBobConversations(
		conversations.filter((conversation) => conversation.id !== conversationId),
		tenantSlug,
		context
	);
};

const addMessage = (
	conversation: BobConversation,
	role: BobMessage['role'],
	content: string,
	suggestedReplies?: string[],
	actions?: BobMessageAction[]
) => ({
	...conversation,
	updatedAtUtc: new Date().toISOString(),
	messages: [
		...conversation.messages,
		{
			id: makeId(),
			role,
			content,
			createdAtUtc: new Date().toISOString(),
			...(suggestedReplies?.length ? { suggestedReplies: suggestedReplies.slice(0, 3) } : {}),
			...(actions?.length ? { actions: actions.slice(0, 4) } : {})
		}
	]
});

export const appendGeneralConversationExchange = async (
	conversationId: string,
	question: string,
	answer: string,
	suggestedReplies: string[] | undefined,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const conversations = await ensureBobConversations(tenantSlug, context);
	const questions = questionsForTenant(tenantSlug);
	const next = conversations.map((conversation) => {
		if (conversation.id !== conversationId) return conversation;
		let updated = addMessage(conversation, 'user', question);
		updated = addMessage(updated, 'bob', answer, suggestedReplies);
		return {
			...updated,
			title: conversation.title === 'New conversation' ? question.slice(0, 54) : conversation.title
		};
	});
	await saveBobConversations(next, tenantSlug, context);
	return next.find((conversation) => conversation.id === conversationId)!;
};

export const appendBobMessage = async (
	conversationId: string,
	role: BobMessage['role'],
	content: string,
	suggestedReplies: string[] | undefined,
	actions: BobMessageAction[] | undefined,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const conversations = await ensureBobConversations(tenantSlug, context);
	const next = conversations.map((conversation) =>
		conversation.id === conversationId
			? addMessage(conversation, role, content, suggestedReplies, actions)
			: conversation
	);
	await saveBobConversations(next, tenantSlug, context);
	return next.find((conversation) => conversation.id === conversationId);
};

export const advanceEstimateConversation = async (
	conversationId: string,
	answer: string,
	extractedFields: Record<string, string>,
	acknowledgement: string | undefined,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const conversations = await ensureBobConversations(tenantSlug, context);
	const questions = questionsForTenant(tenantSlug);
	let updatedConversation: BobConversation | undefined;
	const next = conversations.map((conversation) => {
		if (conversation.id !== conversationId || conversation.mode !== 'estimate-builder') return conversation;
		const draft = { ...(conversation.estimateDraft ?? emptyEstimateDraft()) };
		for (const question of questions) {
			const value = Object.entries(extractedFields).find(
				([key]) => key.toLowerCase() === String(question.key).toLowerCase()
			)?.[1];
			if (value?.trim()) draft[question.key] = value.trim();
		}

		let updated = addMessage(conversation, 'user', answer);
		const nextQuestion = questions.find(
			(question) => !String(draft[question.key] ?? '').trim()
		);
		const captured = Object.keys(extractedFields).length
			? acknowledgement || 'I added those details to the estimate.'
			: '';
		const reply = nextQuestion
			? captured
				? `${captured}\n\n${nextQuestion.prompt}`
				: nextQuestion.prompt
			: captured
				? `${captured}\n\nThe estimate brief is complete. Review it, then create the internal estimate draft.`
				: 'The estimate brief is complete. Review it, then create the internal estimate draft.';
		updated = addMessage(updated, 'bob', reply, nextQuestion?.suggestedReplies);
		updatedConversation = {
			...updated,
			title:
				draft.contactName && draft.projectType
					? `${draft.contactName} · ${draft.projectType}`.slice(0, 64)
					: draft.contactName || conversation.title,
			estimateDraft: draft
		};
		return updatedConversation;
	});
	await saveBobConversations(next, tenantSlug, context);
	return updatedConversation;
};

export const markEstimateConversationCreated = async (
	conversationId: string,
	requestId: string,
	tenantSlug: string,
	context: BobPersistenceContext
) => {
	const conversations = await ensureBobConversations(tenantSlug, context);
	const next = conversations.map((conversation) =>
		conversation.id === conversationId && conversation.estimateDraft
			? {
					...addMessage(
						conversation,
						'bob',
						'Internal estimate created. Open it in Estimates to review quantities, pricing, margin, assumptions, and customer-ready terms.'
					),
					estimateDraft: { ...conversation.estimateDraft, createdRequestId: requestId }
				}
			: conversation
	);
	await saveBobConversations(next, tenantSlug, context);
	return next.find((conversation) => conversation.id === conversationId);
};

export const getEstimateBuilderProgress = (draft: BobEstimateDraft | undefined, tenantSlug = 'bdr') => {
	const value = draft ?? emptyEstimateDraft();
	const questions = questionsForTenant(tenantSlug);
	const complete = questions.filter((question) =>
		String(value[question.key] ?? '').trim()
	).length;
	return {
		complete,
		total: questions.length,
		isComplete: complete === questions.length
	};
};
