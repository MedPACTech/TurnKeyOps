const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const storeDir = `${getCwd()}/.svelte-kit`;
const storePath = `${storeDir}/local-bob-conversations.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
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

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;
const makeId = (prefix: string) => `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

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
				id: makeId('message'),
				role: 'bob',
				content:
					'What would you like to work on? Tell me in your own words, or choose one of the common starting points below.',
				createdAtUtc: now
			}
		]
	};
};

const normalizeEstimateConversation = (conversation: BobConversation): BobConversation => {
	if (conversation.mode !== 'estimate-builder') return conversation;
	const firstQuestion = estimateQuestions[0].prompt;
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

export const loadBobConversations = async (): Promise<BobConversation[]> => {
	try {
		const fs = await getFs();
		const parsed = JSON.parse(await fs.readFile(storePath, 'utf-8')) as Array<
			| BobConversation
			| (Omit<BobConversation, 'mode' | 'estimateDraft'> & {
					mode: 'quote-builder';
					quoteDraft?: Omit<BobEstimateDraft, 'depth'>;
			  })
		>;
		if (!Array.isArray(parsed)) return [];
		return parsed.map((conversation) => {
			if (conversation.mode !== 'quote-builder') {
				return normalizeEstimateConversation(conversation as BobConversation);
			}
			const { quoteDraft, ...legacyConversation } = conversation;
			return normalizeEstimateConversation({
				...legacyConversation,
				mode: 'estimate-builder',
				title: conversation.title === 'New quote' ? 'New estimate' : conversation.title,
				estimateDraft: { ...emptyEstimateDraft(), ...(quoteDraft ?? {}) }
			});
		});
	} catch {
		return [];
	}
};

const saveBobConversations = async (conversations: BobConversation[]) => {
	const fs = await getFs();
	await fs.mkdir(storeDir, { recursive: true });
	await fs.writeFile(storePath, JSON.stringify(conversations, null, 2));
};

export const ensureBobConversations = async () => {
	const conversations = await loadBobConversations();
	if (conversations.some((conversation) => conversation.id === bobHomeConversationId)) {
		return conversations;
	}
	const initial = defaultConversation();
	const next = [initial, ...conversations];
	await saveBobConversations(next);
	return next;
};

export const createBobConversation = async (mode: BobConversationMode) => {
	const conversations = await ensureBobConversations();
	const now = new Date().toISOString();
	const title =
		mode === 'estimate-builder'
			? 'New estimate'
			: mode === 'estimate-followup'
				? 'Estimate follow-up'
				: 'New conversation';
	const introduction =
		mode === 'estimate-builder'
			? `Let’s build the internal estimate. Tell me what you already know; I’ll capture every useful detail and ask only for what is still missing.\n\n${estimateQuestions[0].prompt}`
			: mode === 'estimate-followup'
				? 'I reviewed the live estimate pipeline and surfaced the records that need a next action.'
				: 'What would you like to work on? Tell me in your own words, or choose a starting point below.';
	const conversation: BobConversation = {
		id: makeId('bob'),
		title,
		mode,
		createdAtUtc: now,
		updatedAtUtc: now,
		messages: [
			{ id: makeId('message'), role: 'bob', content: introduction, createdAtUtc: now }
		],
		estimateDraft: mode === 'estimate-builder' ? emptyEstimateDraft() : undefined
	};
	await saveBobConversations([conversation, ...conversations]);
	return conversation;
};

export const getBobConversation = async (id: string | null | undefined) => {
	const conversations = await ensureBobConversations();
	return conversations.find((conversation) => conversation.id === id) ?? conversations[0];
};

export const setBobConversationArchived = async (conversationId: string, archived: boolean) => {
	if (conversationId === bobHomeConversationId) return;
	const conversations = await ensureBobConversations();
	const next = conversations.map((conversation) =>
		conversation.id === conversationId
			? {
					...conversation,
					updatedAtUtc: new Date().toISOString(),
					archivedAtUtc: archived ? new Date().toISOString() : undefined
				}
			: conversation
	);
	await saveBobConversations(next);
};

export const deleteBobConversation = async (conversationId: string) => {
	if (conversationId === bobHomeConversationId) return;
	const conversations = await ensureBobConversations();
	await saveBobConversations(
		conversations.filter((conversation) => conversation.id !== conversationId)
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
			id: makeId('message'),
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
	suggestedReplies?: string[]
) => {
	const conversations = await ensureBobConversations();
	const next = conversations.map((conversation) => {
		if (conversation.id !== conversationId) return conversation;
		let updated = addMessage(conversation, 'user', question);
		updated = addMessage(updated, 'bob', answer, suggestedReplies);
		return {
			...updated,
			title: conversation.title === 'New conversation' ? question.slice(0, 54) : conversation.title
		};
	});
	await saveBobConversations(next);
	return next.find((conversation) => conversation.id === conversationId)!;
};

export const appendBobMessage = async (
	conversationId: string,
	role: BobMessage['role'],
	content: string,
	suggestedReplies?: string[],
	actions?: BobMessageAction[]
) => {
	const conversations = await ensureBobConversations();
	const next = conversations.map((conversation) =>
		conversation.id === conversationId
			? addMessage(conversation, role, content, suggestedReplies, actions)
			: conversation
	);
	await saveBobConversations(next);
	return next.find((conversation) => conversation.id === conversationId);
};

export const advanceEstimateConversation = async (
	conversationId: string,
	answer: string,
	extractedFields: Record<string, string>,
	acknowledgement?: string
) => {
	const conversations = await ensureBobConversations();
	let updatedConversation: BobConversation | undefined;
	const next = conversations.map((conversation) => {
		if (conversation.id !== conversationId || conversation.mode !== 'estimate-builder') return conversation;
		const draft = { ...(conversation.estimateDraft ?? emptyEstimateDraft()) };
		for (const question of estimateQuestions) {
			const value = Object.entries(extractedFields).find(
				([key]) => key.toLowerCase() === String(question.key).toLowerCase()
			)?.[1];
			if (value?.trim()) draft[question.key] = value.trim();
		}

		let updated = addMessage(conversation, 'user', answer);
		const nextQuestion = estimateQuestions.find(
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
	await saveBobConversations(next);
	return updatedConversation;
};

export const markEstimateConversationCreated = async (
	conversationId: string,
	requestId: string
) => {
	const conversations = await ensureBobConversations();
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
	await saveBobConversations(next);
	return next.find((conversation) => conversation.id === conversationId);
};

export const getEstimateBuilderProgress = (draft: BobEstimateDraft | undefined) => {
	const value = draft ?? emptyEstimateDraft();
	const complete = estimateQuestions.filter((question) =>
		String(value[question.key] ?? '').trim()
	).length;
	return {
		complete,
		total: estimateQuestions.length,
		isComplete: complete === estimateQuestions.length
	};
};
