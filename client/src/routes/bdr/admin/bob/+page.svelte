<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';

	type ChatMessage = {
		id: string;
		author: 'bob' | 'user';
		body: string;
	};

	const promptSuggestions = [
		'What needs attention today?',
		'Which estimates are closest to money?',
		'What should I order for today?',
		'Are any invoices at risk?',
		'What is blocking the quote queue?'
	];

	const metrics = [
		{ label: 'AI scope', value: 'Back office', detail: 'Bob answers from BDR operating context, not generic chat.' },
		{ label: 'Mode', value: 'Ask', detail: 'Question first, then Bob points to the right workflow.' },
		{ label: 'Status', value: 'Scaffolded', detail: 'Ready for the tenant-scoped Bob service.' }
	];

	let draft = $state('');
	let messages = $state<ChatMessage[]>([
		{
			id: 'bob-welcome',
			author: 'bob',
			body: 'Ask me what needs attention, what is blocked, what to follow up on, or where money is sitting. I will keep the answer tied to the BDR back office.'
		}
	]);

	const getBobReply = (question: string) => {
		const normalized = question.toLowerCase();
		if (normalized.includes('invoice') || normalized.includes('money') || normalized.includes('paid')) {
			return 'I would start in Invoices. There are customer-approved draft invoices waiting for review, and active invoices should show the next billing move before anyone chases work elsewhere.';
		}
		if (normalized.includes('estimate') || normalized.includes('quote')) {
			return 'Start with the quote and estimate lanes. New quote requests should move through site readiness, then the estimate draft should be sent from the estimate screen once totals are ready.';
		}
		if (normalized.includes('order') || normalized.includes('concrete') || normalized.includes('material')) {
			return 'Check Bob’s order list on the dashboard, then confirm against today’s scheduled work in Calendar. The order list should become an AI-generated purchasing lane once the job schedule is live.';
		}
		if (normalized.includes('block') || normalized.includes('risk')) {
			return 'The safest first check is Quotes for readiness blockers, then Invoices for collection risk. Anything blocked should have a visible next action before it leaves the current lane.';
		}
		if (normalized.includes('today') || normalized.includes('attention')) {
			return 'Today I would check: quote requests needing readiness, estimates ready to send, customer-approved invoice drafts, and any calendar work that depends on materials or weather.';
		}
		return 'I can help with quotes, estimates, calendar, invoices, contacts, admin settings, and the public-site workflow. Ask me about a queue, customer, blocker, or next move.';
	};

	const sendMessage = (text = draft) => {
		const body = text.trim();
		if (!body) return;
		const idBase = `${Date.now()}`;
		messages = [
			...messages,
			{ id: `user-${idBase}`, author: 'user', body },
			{ id: `bob-${idBase}`, author: 'bob', body: getBobReply(body) }
		];
		draft = '';
	};
</script>

<AdminWorkspace
	kicker="External Admin / Ask Bob"
	title="Ask Bob"
	description="Ask business questions and get contractor backoffice guidance without leaving the admin shell."
	{metrics}
	contextLabel="Prompts"
	focusLabel="Thread"
>
	{#snippet context()}
		<div class="space-y-2">
			<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
				Suggested questions
			</p>
			{#each promptSuggestions as prompt}
				<button
					type="button"
					class="w-full rounded-lg bg-white/80 px-3 py-3 text-left text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-white"
					onclick={() => sendMessage(prompt)}
				>
					{prompt}
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			{#each messages as message}
				<div class={`rounded-lg px-3 py-3 shadow-sm ${message.author === 'bob' ? 'bg-white/80' : 'bg-[#fff4ea]'}`}>
					<div class="flex items-center gap-2">
						<span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white text-lg shadow-sm">
							{message.author === 'bob' ? '👷' : 'ER'}
						</span>
						<p class="text-sm font-semibold text-[var(--text-strong)]">{message.author === 'bob' ? 'Bob' : 'You'}</p>
					</div>
					<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{message.body}</p>
				</div>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		<div class="flex min-h-[620px] flex-col rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
			<div class="flex items-start justify-between gap-4">
				<div class="flex items-start gap-3">
					<span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-2xl shadow-sm">👷</span>
					<div>
						<h2 class="text-xl font-semibold text-[var(--text-strong)]">Bob</h2>
						<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">Business-aware assistant for BDR office work.</p>
					</div>
				</div>
				<span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-semibold text-[var(--accent-text)]">AI</span>
			</div>

			<div class="mt-5 flex-1 space-y-3 overflow-y-auto rounded-lg bg-[var(--shell-panel-strong)] p-4">
				{#each messages as message}
					<div class={`flex ${message.author === 'user' ? 'justify-end' : 'justify-start'}`}>
						<div class={`max-w-[78%] rounded-lg px-4 py-3 shadow-sm ${message.author === 'user' ? 'bg-[var(--accent-solid)] text-white' : 'bg-white text-[var(--text-base)]'}`}>
							<p class="text-sm leading-6">{message.body}</p>
						</div>
					</div>
				{/each}
			</div>

			<form
				class="mt-4 flex flex-col gap-3 sm:flex-row"
				onsubmit={(event) => {
					event.preventDefault();
					sendMessage();
				}}
			>
				<label class="sr-only" for="ask-bob-input">Ask Bob</label>
				<input
					id="ask-bob-input"
					bind:value={draft}
					class="min-h-12 flex-1 rounded-lg border border-transparent bg-white px-4 text-sm text-[var(--text-strong)] shadow-sm outline-none ring-1 ring-[var(--shell-border)] transition focus:ring-[var(--accent-border)]"
					placeholder="Ask Bob about quotes, estimates, invoices, schedule, or blockers"
				/>
				<button
					type="submit"
					class="min-h-12 rounded-lg bg-[var(--accent-solid)] px-5 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]"
				>
					Ask Bob
				</button>
			</form>
		</div>
	{/snippet}
</AdminWorkspace>
