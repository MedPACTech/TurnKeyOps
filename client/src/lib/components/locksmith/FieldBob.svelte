<script lang="ts">
	import type { FieldDraft } from '$lib/locksmith-drafts';
	let { draft, online }: { draft: FieldDraft; online: boolean } = $props();
	let question = $state('');
	let answer = $state('');
	let issue = $state('');
	let busy = $state(false);
	async function ask(value = question) {
		if (busy || !online || !value.trim()) return;
		busy = true; issue = ''; answer = '';
		try {
			const response = await fetch('/carlzipf/tech/bob', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ question: value.trim(), draft }) });
			const result = await response.json();
			if (!response.ok) throw new Error(result.message || result.error || 'Bob could not answer right now.');
			answer = result.answer || 'Bob did not return an answer. Try a more specific question.';
			question = value.trim();
		} catch (cause) { issue = cause instanceof Error ? cause.message : 'Bob could not answer right now.'; }
		finally { busy = false; }
	}
</script>

<section id="field-bob" class="mt-5 rounded-xl border border-[var(--accent-border)] bg-[var(--accent-soft)] p-5" aria-labelledby="field-bob-title">
	<div class="flex flex-wrap items-start justify-between gap-3"><div><p class="text-xs font-bold uppercase tracking-widest text-[var(--accent-text)]">AI field assistant</p><h2 id="field-bob-title" class="mt-1 text-xl font-bold">Ask Bob</h2><p class="mt-1 text-sm text-slate-700">Get help reviewing opening measurements, handing, and hardware questions while you capture the visit.</p></div><span class="rounded-full bg-white px-3 py-1 text-xs font-semibold">{online ? 'Online' : 'Offline'}</span></div>
	<div class="mt-4 flex flex-wrap gap-2">{#each ['What measurements are missing?', 'What hardware fit should I verify?', 'Summarize this opening for the office'] as prompt}<button type="button" class="rounded-full border border-[var(--accent-border)] bg-white px-3 py-2 text-xs font-semibold" disabled={busy || !online} onclick={() => ask(prompt)}>{prompt}</button>{/each}</div>
	<form class="mt-4 flex gap-2" onsubmit={(event) => { event.preventDefault(); void ask(); }}><label class="sr-only" for="field-bob-question">Question for Bob</label><input id="field-bob-question" class="min-w-0 flex-1 rounded-lg border bg-white p-3 text-sm" maxlength="2000" placeholder="Ask about this door or lock…" bind:value={question} /><button class="rounded-lg bg-[var(--accent-text)] px-4 font-semibold text-white disabled:opacity-50" disabled={busy || !online || !question.trim()}>{busy ? 'Thinking…' : 'Ask'}</button></form>
	{#if issue}<p class="mt-3 text-sm text-red-800" role="alert">{issue}</p>{/if}
	{#if answer}<div class="mt-4 rounded-lg bg-white p-4 text-sm leading-6" role="status"><p class="font-semibold">Bob</p><p class="mt-1 whitespace-pre-wrap">{answer}</p></div>{/if}
	<p class="mt-3 text-xs text-slate-600">Bob offers guidance from the current device draft. Verify product fit and measurements; the server prepares the priced quote.</p>
</section>
