<script lang="ts">
	import { enhance } from '$app/forms';
	import type { SubmitFunction } from '@sveltejs/kit';
	import type { PageProps } from './$types';
	import {
		ArrowRight,
		Archive,
		ArchiveRestore,
		Bot,
		CalendarDays,
		ChevronDown,
		CircleAlert,
		FilePlus2,
		FileText,
		MessageSquare,
		Mic,
		MicOff,
		Send,
		Sparkles,
		Trash2
	} from 'lucide-svelte';

	let { data, form }: PageProps = $props();
	let draft = $state('');
	let isListening = $state(false);
	let speechMessage = $state('');
	let isWaitingForBob = $state(false);
	let archivedOpen = $state(false);
	let recognition: SpeechRecognitionLike | null = null;

	type SpeechResultListLike = {
		length: number;
		[index: number]: {
			isFinal: boolean;
			[index: number]: { transcript: string };
		};
	};

	type SpeechRecognitionLike = {
		continuous: boolean;
		interimResults: boolean;
		lang: string;
		start: () => void;
		stop: () => void;
		onresult: ((event: { results: SpeechResultListLike }) => void) | null;
		onerror: ((event: { error: string }) => void) | null;
		onend: (() => void) | null;
	};

	type SpeechRecognitionConstructor = new () => SpeechRecognitionLike;

	const conversation = $derived(data.selectedConversation);
	const isEstimateBuilder = $derived(conversation.mode === 'estimate-builder');
	const isEstimateFollowup = $derived(conversation.mode === 'estimate-followup');
	const estimateDraft = $derived(conversation.estimateDraft);
	const estimateProgress = $derived(data.estimateProgress);
	const actionResult = $derived(
		form as { action?: string; message?: string; conversationId?: string } | null
	);
	const activeConversations = $derived(
		data.conversations.filter((item) => item.id !== 'bob-home' && !item.archivedAtUtc)
	);
	const archivedConversations = $derived(
		data.conversations.filter((item) => Boolean(item.archivedAtUtc))
	);

	const generalPrompts = [
		'Review today’s priorities',
		'Show estimates needing follow-up',
		'Start a new estimate',
		'Review unpaid invoices'
	];

	const formatConversationTime = (value: string) =>
		new Date(value).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });

	function confirmConversationDelete(event: MouseEvent, title: string) {
		if (!window.confirm(`Delete “${title}”? This conversation cannot be recovered.`)) {
			event.preventDefault();
		}
	}

	function handleComposerKeydown(event: KeyboardEvent) {
		if (event.key !== 'Enter' || event.shiftKey || event.isComposing) return;

		event.preventDefault();
		const textarea = event.currentTarget as HTMLTextAreaElement;
		if (!draft.trim()) return;
		textarea.form?.requestSubmit();
	}

	const enhanceBobMessage: SubmitFunction = () => {
		isWaitingForBob = true;
		speechMessage = '';

		return async ({ update }) => {
			try {
				await update();
				draft = '';
			} finally {
				isWaitingForBob = false;
			}
		};
	};

	function toggleDictation() {
		if (isListening) {
			recognition?.stop();
			return;
		}

		const speechWindow = window as typeof window & {
			SpeechRecognition?: SpeechRecognitionConstructor;
			webkitSpeechRecognition?: SpeechRecognitionConstructor;
		};
		const Recognition = speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition;

		if (!Recognition) {
			speechMessage = 'Voice input is not supported in this browser.';
			return;
		}

		const startingDraft = draft.trim();
		recognition = new Recognition();
		recognition.continuous = false;
		recognition.interimResults = true;
		recognition.lang = 'en-US';
		recognition.onresult = (event) => {
			let transcript = '';
			for (let index = 0; index < event.results.length; index += 1) {
				transcript += event.results[index][0]?.transcript ?? '';
			}
			draft = `${startingDraft}${startingDraft && transcript ? ' ' : ''}${transcript}`.trimStart();
		};
		recognition.onerror = (event) => {
			speechMessage =
				event.error === 'not-allowed'
					? 'Microphone access was blocked. Allow it in your browser settings to speak to Bob.'
					: 'Bob could not hear that. Try again.';
			isListening = false;
		};
		recognition.onend = () => {
			isListening = false;
		};

		speechMessage = '';
		isListening = true;
		try {
			recognition.start();
		} catch {
			isListening = false;
			speechMessage = 'The microphone is already in use. Try again in a moment.';
		}
	}
</script>

<svelte:head>
	<title>Ask Bob · BDR Admin</title>
</svelte:head>

<section class="flex h-full min-h-0 overflow-hidden bg-white">
	<aside class="hidden w-64 shrink-0 flex-col border-r border-[var(--shell-border)] bg-[#f8f9fb] lg:flex">
		<div class="border-b border-[var(--shell-border)] px-4 py-4">
			<h1 class="text-lg font-semibold text-[var(--text-strong)]">Conversations</h1>
			<a
				href="/bdr/admin/bob"
				class={`mt-3 flex min-h-11 items-center gap-2 rounded-md px-3 text-sm font-semibold transition ${
					conversation.id === 'bob-home'
						? 'bg-white text-[var(--accent-text)] shadow-sm ring-1 ring-[var(--shell-border)]'
						: 'text-[var(--text-base)] hover:bg-white'
				}`}
			>
				<Sparkles class="h-4 w-4 text-[var(--accent-text)]" aria-hidden="true" />
				Ask Bob
			</a>
		</div>

		<nav class="min-h-0 flex-1 overflow-y-auto p-2" aria-label="Bob conversations">
			{#each activeConversations as item}
				<div
					class={`group mb-1 flex items-start rounded-md transition ${
						item.id === conversation.id
							? 'bg-white shadow-sm ring-1 ring-[var(--shell-border)]'
							: 'hover:bg-white/70'
					}`}
				>
					<a
						href={`/bdr/admin/bob?conversation=${encodeURIComponent(item.id)}`}
						class="flex min-w-0 flex-1 items-start gap-2 px-3 py-3"
					>
						{#if item.mode === 'estimate-builder' || item.mode === 'estimate-followup'}
							<FileText class="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-text)]" aria-hidden="true" />
						{:else}
							<MessageSquare class="mt-0.5 h-4 w-4 shrink-0 text-[var(--text-muted)]" aria-hidden="true" />
						{/if}
						<div class="min-w-0 flex-1">
							<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{item.title}</p>
							<p class="mt-1 text-xs text-[var(--muted)]">
								{item.mode === 'estimate-builder'
									? 'Estimate Builder'
									: item.mode === 'estimate-followup'
										? 'Estimate Follow-up'
										: formatConversationTime(item.updatedAtUtc)}
								</p>
							</div>
					</a>
					<div class="flex shrink-0 items-center gap-0.5 py-2 pr-1 opacity-60 transition group-hover:opacity-100 group-focus-within:opacity-100">
							<form method="POST" action="?/archiveConversation">
								<input type="hidden" name="conversationId" value={item.id} />
								<button
									type="submit"
									class="inline-flex h-8 w-8 items-center justify-center rounded-md text-[var(--muted)] hover:bg-slate-100 hover:text-[var(--text-strong)]"
									aria-label={`Archive ${item.title}`}
									title="Archive"
								>
									<Archive class="h-3.5 w-3.5" aria-hidden="true" />
								</button>
							</form>
							<form method="POST" action="?/deleteConversation">
								<input type="hidden" name="conversationId" value={item.id} />
								<button
									type="submit"
									class="inline-flex h-8 w-8 items-center justify-center rounded-md text-[var(--muted)] hover:bg-rose-50 hover:text-rose-700"
									aria-label={`Delete ${item.title}`}
									title="Delete"
									onclick={(event) => confirmConversationDelete(event, item.title)}
								>
									<Trash2 class="h-3.5 w-3.5" aria-hidden="true" />
								</button>
							</form>
					</div>
				</div>
			{/each}

			{#if archivedConversations.length}
				<button
					type="button"
					class="mt-3 flex min-h-10 w-full items-center justify-between rounded-md px-3 text-xs font-semibold uppercase tracking-[0.1em] text-[var(--muted)] hover:bg-white"
					onclick={() => (archivedOpen = !archivedOpen)}
					aria-expanded={archivedOpen}
				>
					<span>Archived · {archivedConversations.length}</span>
					<ChevronDown class={`h-4 w-4 transition ${archivedOpen ? 'rotate-180' : ''}`} aria-hidden="true" />
				</button>
				{#if archivedOpen}
					{#each archivedConversations as item}
						<div class="mb-1 flex items-center rounded-md px-3 py-2 text-[var(--text-muted)] hover:bg-white">
							<div class="min-w-0 flex-1">
								<p class="truncate text-sm font-medium">{item.title}</p>
								<p class="mt-0.5 text-xs">{formatConversationTime(item.updatedAtUtc)}</p>
							</div>
							<form method="POST" action="?/restoreConversation">
								<input type="hidden" name="conversationId" value={item.id} />
								<button
									type="submit"
									class="inline-flex h-8 w-8 items-center justify-center rounded-md hover:bg-[var(--accent-soft)] hover:text-[var(--accent-text)]"
									aria-label={`Restore ${item.title}`}
									title="Restore"
								>
									<ArchiveRestore class="h-3.5 w-3.5" aria-hidden="true" />
								</button>
							</form>
							<form method="POST" action="?/deleteConversation">
								<input type="hidden" name="conversationId" value={item.id} />
								<button
									type="submit"
									class="inline-flex h-8 w-8 items-center justify-center rounded-md hover:bg-rose-50 hover:text-rose-700"
									aria-label={`Delete ${item.title}`}
									title="Delete"
									onclick={(event) => confirmConversationDelete(event, item.title)}
								>
									<Trash2 class="h-3.5 w-3.5" aria-hidden="true" />
								</button>
							</form>
						</div>
					{/each}
				{/if}
			{/if}
		</nav>
	</aside>

	<div class="flex min-w-0 flex-1 flex-col">
		<header class="flex min-h-[73px] items-center justify-between gap-4 border-b border-[var(--shell-border)] px-4 py-3 sm:px-6">
			<div class="min-w-0">
				<div class="flex items-center gap-2">
					{#if isEstimateBuilder}
						<FileText class="h-4 w-4 text-[var(--accent-text)]" aria-hidden="true" />
						<p class="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">Estimate Builder</p>
					{:else if isEstimateFollowup}
						<CircleAlert class="h-4 w-4 text-[var(--accent-text)]" aria-hidden="true" />
						<p class="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">Estimate follow-up</p>
					{:else}
						<Sparkles class="h-4 w-4 text-[var(--accent-text)]" aria-hidden="true" />
						<p class="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">Operating partner</p>
					{/if}
				</div>
				<h2 class="mt-1 truncate text-lg font-semibold text-[var(--text-strong)]">{conversation.title}</h2>
			</div>
			<div class="flex shrink-0 items-center gap-1">
				{#if isEstimateBuilder}
					<p class="mr-1 hidden text-sm font-medium text-[var(--text-muted)] sm:block">
						{estimateProgress.complete} of {estimateProgress.total} details
					</p>
				{/if}
				{#if conversation.id !== 'bob-home'}
					<form method="POST" action="?/archiveConversation" class="lg:hidden">
						<input type="hidden" name="conversationId" value={conversation.id} />
						<button
							type="submit"
							class="inline-flex h-10 w-10 items-center justify-center rounded-md text-[var(--text-muted)] hover:bg-slate-100"
							aria-label="Archive conversation"
							title="Archive conversation"
						>
							<Archive class="h-4 w-4" aria-hidden="true" />
						</button>
					</form>
					<form method="POST" action="?/deleteConversation" class="lg:hidden">
						<input type="hidden" name="conversationId" value={conversation.id} />
						<button
							type="submit"
							class="inline-flex h-10 w-10 items-center justify-center rounded-md text-[var(--text-muted)] hover:bg-rose-50 hover:text-rose-700"
							aria-label="Delete conversation"
							title="Delete conversation"
							onclick={(event) => confirmConversationDelete(event, conversation.title)}
						>
							<Trash2 class="h-4 w-4" aria-hidden="true" />
						</button>
					</form>
				{/if}
			</div>
		</header>

			<div class="min-h-0 flex-1 overflow-y-auto bg-white px-4 py-6 sm:px-8">
				<div class="mx-auto max-w-3xl space-y-6">
					{#if isEstimateBuilder}
						<details class="border-y border-[var(--shell-border)] py-3 xl:hidden">
							<summary class="flex min-h-10 cursor-pointer list-none items-center justify-between gap-3 text-sm font-semibold text-[var(--text-strong)]">
								<span>Working brief</span>
								<span class="text-xs font-medium text-[var(--text-muted)]">
									{estimateProgress.complete} of {estimateProgress.total}
								</span>
							</summary>
							<dl class="mt-2 divide-y divide-[var(--shell-border)]">
								{#each [
									['Customer', estimateDraft?.contactName],
									['Job site', estimateDraft?.serviceAddress],
									['Project', estimateDraft?.projectType],
									['Scope', estimateDraft?.scope],
									['Measurements', estimateDraft?.dimensions],
									['Depth', estimateDraft?.depth],
									['Timeline', estimateDraft?.timeline]
								] as field}
									<div class="py-2">
										<dt class="text-xs font-semibold uppercase tracking-[0.1em] text-[var(--muted)]">{field[0]}</dt>
										<dd class={`mt-1 text-sm ${field[1] ? 'text-[var(--text-strong)]' : 'italic text-[var(--muted)]'}`}>
											{field[1] || 'Waiting for answer'}
										</dd>
									</div>
								{/each}
							</dl>
						</details>
					{/if}

					{#each conversation.messages as message}
					<div class={`flex gap-3 ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}>
						{#if message.role === 'bob'}
							<div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-[var(--accent-solid)] text-white">
								<Bot class="h-4 w-4" aria-hidden="true" />
							</div>
						{/if}
						<div class="max-w-[82%]">
							<div
								class={`px-4 py-3 text-sm leading-6 ${
									message.role === 'user'
										? 'rounded-2xl rounded-br-sm bg-[#1f2933] text-white'
										: 'border-l-2 border-[var(--accent-border)] text-[var(--text-base)]'
								}`}
							>
								<p class="whitespace-pre-line">{message.content}</p>
							</div>
							{#if message.role === 'bob' &&
							message.id === conversation.messages.at(-1)?.id &&
							message.suggestedReplies?.length}
								<form method="POST" action="?/ask" class="mt-3 flex flex-wrap gap-2">
									<input type="hidden" name="conversationId" value={conversation.id} />
									{#each message.suggestedReplies as reply}
										<button
											type="submit"
											name="question"
											value={reply}
											class="min-h-10 rounded-full border border-[var(--accent-border)] bg-white px-4 text-sm font-semibold text-[var(--accent-text)] transition hover:bg-[var(--accent-soft)] focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)]"
										>
											{reply}
										</button>
									{/each}
								</form>
							{/if}
							{#if message.role === 'bob' &&
							message.id === conversation.messages.at(-1)?.id &&
							message.actions?.length}
								<div class="mt-3 flex flex-wrap gap-2">
									{#each message.actions as action}
										{#if action.kind === 'schedule-inspection'}
											<form method="POST" action="?/scheduleInspection">
												<input type="hidden" name="conversationId" value={conversation.id} />
												<input type="hidden" name="requestId" value={action.requestId} />
												<input type="hidden" name="visitDate" value={action.visitDate} />
												<input type="hidden" name="windowStart" value={action.windowStart} />
												<input type="hidden" name="windowEnd" value={action.windowEnd} />
												<input
													type="hidden"
													name="assignedFieldResource"
													value={action.assignedFieldResource}
												/>
												<button
													type="submit"
													class="inline-flex min-h-10 items-center gap-2 rounded-full border border-[var(--accent-border)] bg-white px-4 text-sm font-semibold text-[var(--accent-text)] transition hover:bg-[var(--accent-soft)] focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)]"
												>
													<CalendarDays class="h-4 w-4" aria-hidden="true" />
													{action.label}
												</button>
											</form>
										{:else}
											<a
												href={action.href}
												class="inline-flex min-h-10 items-center gap-2 rounded-full border border-[var(--shell-border-strong)] bg-white px-4 text-sm font-semibold text-[var(--text-base)] transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)]"
											>
												<CalendarDays class="h-4 w-4" aria-hidden="true" />
												{action.label}
											</a>
										{/if}
									{/each}
								</div>
							{/if}
						</div>
					</div>
				{/each}

				{#if isEstimateFollowup}
					<div class="divide-y divide-[var(--shell-border)] border-y border-[var(--shell-border)] xl:hidden">
						{#each data.estimateFollowups as item}
							<a href={item.href} class="group block py-4">
								<div class="flex items-start justify-between gap-3">
									<div>
										<p class="text-sm font-semibold text-[var(--text-strong)]">{item.customer}</p>
										<p class="mt-1 text-xs text-[var(--text-muted)]">{item.project} · {item.status.replaceAll('-', ' ')}</p>
									</div>
									<ArrowRight class="mt-1 h-4 w-4 shrink-0 text-[var(--muted)]" aria-hidden="true" />
								</div>
								<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{item.reason}</p>
								<p class="mt-1 text-xs font-medium text-[var(--accent-text)]">{item.nextAction}</p>
							</a>
						{/each}
					</div>
				{/if}

				{#if actionResult?.message && actionResult.conversationId === conversation.id}
					<p class="rounded-md bg-rose-50 px-3 py-2 text-sm font-medium text-rose-700">{actionResult.message}</p>
				{/if}

				{#if !isEstimateBuilder && !isEstimateFollowup && conversation.messages.length <= 2}
					<form method="POST" action="?/ask" class="grid gap-2 pt-2 sm:grid-cols-2">
						<input type="hidden" name="conversationId" value={conversation.id} />
						{#each generalPrompts as prompt}
							<button
								type="submit"
								name="question"
								value={prompt}
								class="min-h-16 rounded-lg border border-[var(--shell-border)] px-3 py-2 text-left text-sm leading-5 text-[var(--text-base)] transition hover:border-[var(--accent-border)] hover:bg-[var(--accent-soft)]"
							>
								{prompt}
							</button>
						{/each}
					</form>
				{/if}
			</div>
		</div>

		<footer class="border-t border-[var(--shell-border)] bg-white px-3 py-3 sm:px-6 sm:py-4">
			<div
				class={`mx-auto mb-2 flex max-w-3xl items-center gap-2 text-xs text-[var(--text-muted)] transition ${
					isWaitingForBob ? 'min-h-5 opacity-100' : 'h-0 overflow-hidden opacity-0'
				}`}
				role="status"
				aria-live="polite"
			>
				<Bot class="h-4 w-4 shrink-0 text-[var(--accent-text)]" aria-hidden="true" />
				<span class="font-medium text-[var(--text-base)]">Bob is working on it</span>
				<span class="flex gap-0.5" aria-hidden="true">
					<span class="h-1 w-1 animate-pulse rounded-full bg-current"></span>
					<span class="h-1 w-1 animate-pulse rounded-full bg-current [animation-delay:150ms]"></span>
					<span class="h-1 w-1 animate-pulse rounded-full bg-current [animation-delay:300ms]"></span>
				</span>
				<span>AI responses can take a few seconds.</span>
			</div>
			<form
				method="POST"
				action="?/ask"
				use:enhance={enhanceBobMessage}
				class="mx-auto flex max-w-3xl items-center gap-2 sm:gap-3"
			>
				<input type="hidden" name="conversationId" value={conversation.id} />
				<label class="sr-only" for="bob-message">Message Bob</label>
				<div class="relative min-w-0 flex-1">
					<textarea
						id="bob-message"
						name="question"
						bind:value={draft}
						rows="1"
						class="block min-h-12 max-h-40 w-full resize-y rounded-xl border border-[var(--shell-border-strong)] bg-white py-3 pl-4 pr-12 text-base leading-6 text-[var(--text-strong)] outline-none transition placeholder:text-[var(--muted)] focus:border-[var(--accent-solid)] focus:ring-2 focus:ring-[var(--focus-ring)] disabled:cursor-wait disabled:bg-slate-50 sm:text-sm"
						placeholder={isListening
							? 'Listening…'
							: isEstimateBuilder
								? 'Answer Bob’s question…'
								: 'Message Bob…'}
						onkeydown={handleComposerKeydown}
						disabled={isWaitingForBob}
						required
					></textarea>
					<button
						type="button"
						class={`absolute bottom-1.5 right-1.5 inline-flex h-9 w-9 items-center justify-center rounded-lg transition ${
							isListening
								? 'bg-rose-100 text-rose-700'
								: 'text-[var(--text-muted)] hover:bg-[var(--accent-soft)] hover:text-[var(--accent-text)]'
						}`}
						onclick={toggleDictation}
						aria-label={isListening ? 'Stop listening' : 'Speak to Bob'}
						aria-pressed={isListening}
						title={isListening ? 'Stop listening' : 'Speak to Bob'}
					>
						{#if isListening}
							<MicOff class="h-5 w-5" aria-hidden="true" />
						{:else}
							<Mic class="h-5 w-5" aria-hidden="true" />
						{/if}
					</button>
				</div>
				<button
					type="submit"
					disabled={isWaitingForBob || !draft.trim()}
					class="inline-flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-[var(--accent-solid)] text-white transition hover:bg-[var(--accent-solid-hover)] disabled:cursor-not-allowed disabled:bg-slate-300"
					aria-label="Send message"
				>
					<Send class="h-5 w-5" aria-hidden="true" />
				</button>
			</form>
		</footer>
	</div>

	{#if isEstimateBuilder || isEstimateFollowup}
		<aside class="hidden w-[390px] shrink-0 overflow-y-auto border-l border-[var(--shell-border)] bg-[#fbfbfc] p-5 xl:block">
		{#if isEstimateBuilder}
			<div class="flex items-start justify-between gap-4">
				<div>
					<p class="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">Working brief</p>
					<h2 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">Estimate details</h2>
				</div>
				<span class="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-[var(--text-muted)] ring-1 ring-[var(--shell-border)]">
					{Math.round((estimateProgress.complete / estimateProgress.total) * 100)}%
				</span>
			</div>

			<div class="mt-5 h-1.5 overflow-hidden rounded-full bg-slate-200">
				<div
					class="h-full rounded-full bg-[var(--accent-solid)] transition-all"
					style={`width: ${(estimateProgress.complete / estimateProgress.total) * 100}%`}
				></div>
			</div>

			<dl class="mt-5 divide-y divide-[var(--shell-border)] border-y border-[var(--shell-border)]">
				{#each [
					['Customer', estimateDraft?.contactName],
					['Company / property', estimateDraft?.companyName],
					['Email', estimateDraft?.email],
					['Phone', estimateDraft?.phone],
					['Job site', estimateDraft?.serviceAddress],
					['Project', estimateDraft?.projectType],
					['Scope', estimateDraft?.scope],
					['Measurements', estimateDraft?.dimensions],
					['Depth', estimateDraft?.depth],
					['Timeline', estimateDraft?.timeline],
					['Notes', estimateDraft?.notes]
				] as field}
					<div class="py-3">
						<dt class="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">{field[0]}</dt>
						<dd class={`mt-1 text-sm leading-6 ${field[1] ? 'text-[var(--text-strong)]' : 'italic text-[var(--muted)]'}`}>
							{field[1] || 'Waiting for answer'}
						</dd>
					</div>
				{/each}
			</dl>

			{#if estimateDraft?.createdRequestId}
				<a
					href={`/bdr/admin/estimates?request=${encodeURIComponent(estimateDraft.createdRequestId)}`}
					class="mt-5 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-4 text-sm font-semibold text-white"
				>
					Open in Estimates
					<ArrowRight class="h-4 w-4" aria-hidden="true" />
				</a>
			{:else}
				<form method="POST" action="?/createEstimate" class="mt-5">
					<input type="hidden" name="conversationId" value={conversation.id} />
					<button
						type="submit"
						disabled={!estimateProgress.isComplete}
						class="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-4 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)] disabled:cursor-not-allowed disabled:bg-slate-300"
					>
						<FilePlus2 class="h-4 w-4" aria-hidden="true" />
						Create internal estimate
					</button>
				</form>
			{/if}
		{:else}
			<div>
				<p class="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">Live estimate pipeline</p>
				<h2 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">
					{data.estimateFollowups.length} need a next action
				</h2>
				<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">
					Ranked by estimate stage and time since the latest recorded activity.
				</p>
			</div>

			<div class="mt-5 divide-y divide-[var(--shell-border)] border-y border-[var(--shell-border)]">
				{#each data.estimateFollowups as item}
					<a href={item.href} class="group block py-4">
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">{item.customer}</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">
									{item.project} · {item.status.replaceAll('-', ' ')}
								</p>
							</div>
							<span
								class={`rounded-full px-2 py-1 text-[11px] font-semibold ${
									item.priority === 'high'
										? 'bg-rose-50 text-rose-700'
										: item.priority === 'medium'
											? 'bg-amber-50 text-amber-700'
											: 'bg-slate-100 text-slate-600'
								}`}
							>
								{item.ageDays}d
							</span>
						</div>
						<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{item.reason}</p>
						<p class="mt-1 text-xs font-medium text-[var(--accent-text)]">{item.nextAction}</p>
					</a>
				{/each}
			</div>
		{/if}
		</aside>
	{/if}
</section>
