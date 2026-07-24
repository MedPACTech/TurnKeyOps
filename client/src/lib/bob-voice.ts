export const bobVoiceCookie = 'tko_bob_voice';

export const bobVoiceIds = [
	'practical',
	'friendly',
	'foreman',
	'advisor',
	'minimal',
	'gruff'
] as const;

export type BobVoiceId = (typeof bobVoiceIds)[number];

export type BobVoiceOption = {
	id: BobVoiceId;
	label: string;
	description: string;
	preview: string;
	greeting: string;
};

export const bobVoiceOptions: BobVoiceOption[] = [
	{
		id: 'practical',
		label: 'Practical Bob',
		description: 'Calm, direct, and focused on the next useful action.',
		preview: 'Three estimates need attention. Start with the oldest customer commitment.',
		greeting: 'What would you like to work on? Tell me in your own words, or choose a starting point below.'
	},
	{
		id: 'friendly',
		label: 'Friendly Bob',
		description: 'Warm, conversational, and encouraging without becoming chatty.',
		preview: 'You’re in good shape. Let’s clear these three estimates before they become problems.',
		greeting: 'What can I help you tackle today? Tell me what’s going on, or choose a starting point below.'
	},
	{
		id: 'foreman',
		label: 'Foreman Bob',
		description: 'Decisive, blunt, and organized around priorities.',
		preview: 'Three estimates are holding up the board. Handle the oldest one first.',
		greeting: 'What are we working on? Give me the situation, or pick a starting point and let’s move.'
	},
	{
		id: 'advisor',
		label: 'Advisor Bob',
		description: 'Explains reasoning, tradeoffs, and downstream effects.',
		preview: 'Three estimates need attention. The oldest one comes first because it carries the greatest customer-risk exposure.',
		greeting: 'What would you like to work through? Describe the situation, or choose a starting point and I’ll help frame the next decision.'
	},
	{
		id: 'minimal',
		label: 'Minimal Bob',
		description: 'Very short responses with almost no explanation.',
		preview: '3 estimates need follow-up. Open the oldest.',
		greeting: 'What do you need? Ask or choose below.'
	},
	{
		id: 'gruff',
		label: 'Gruff Bob',
		description: 'Profane, snarky, and quick to call out obvious or sloppy thinking.',
		preview: 'Three damn estimates are getting stale. Oldest one first—because apparently I have to explain how a queue works.',
		greeting: 'All right, what the hell are we fixing today? Get off your ass and tell me what to do, or pick something below and let’s get moving.'
	}
];

export const normalizeBobVoice = (value: string | null | undefined): BobVoiceId =>
	bobVoiceIds.includes(value as BobVoiceId) ? (value as BobVoiceId) : 'practical';
