<script lang="ts">
	import { onMount } from 'svelte';
	import { issuedReviewPath } from '$lib/locksmith-review';
	import type { FieldEstimate } from '$lib/locksmith-handoff';
	let { estimate }: { estimate: FieldEstimate } = $props();
	let origin = $state('');
	let now = $state(Date.now());
	let message = $state('');
	let path = $derived(issuedReviewPath(estimate, now));
	let url = $derived(path ? `${origin}${path}` : '');
	onMount(() => { origin = window.location.origin; const timer = setInterval(() => { now = Date.now(); }, 30_000); return () => clearInterval(timer); });
	async function copy() {
		if (!path || !origin) return;
		try { await navigator.clipboard.writeText(url); message = 'Review link copied. No email or text has been sent.'; }
		catch { message = 'Copy the review link from the field below.'; }
	}
</script>

{#if estimate.status === 'sent'}
	<section class="mt-4 rounded-md border border-green-200 bg-green-50 p-4" aria-label="Customer quote handoff">
		<h3 class="font-semibold">{estimate.delivery?.status === 'approved' ? 'Customer approved this revision' : estimate.delivery?.status === 'changes-requested' ? 'Customer requested changes' : 'Issued quote ready for customer review'}</h3>
		<p class="mt-2 text-sm">Revision {estimate.revisionNumber} · {estimate.delivery?.status?.replaceAll('-', ' ') ?? 'Refresh to check customer status'}</p>
		{#if estimate.delivery?.responseNote}<p class="mt-2 whitespace-pre-wrap text-sm">{estimate.delivery.responseNote}</p>{/if}
		{#if path}
			<div class="mt-4 flex flex-wrap gap-3"><a class="btn-primary" href={path} target="_blank" rel="noopener noreferrer">Open for customer on this device</a><button class="btn-secondary" onclick={copy}>Copy customer review link</button></div>
			<label class="label mt-4">Customer review link<input class="input mt-1" readonly value={url} onfocus={(event) => event.currentTarget.select()} /></label>
			<p class="mt-2 text-xs text-gray-600">The customer reviews and responds on the shared quote page. This link grants access to this quote; share it with the intended customer. Opening or copying it does not record approval or send a message.</p>
		{:else}<p class="mt-3 text-sm text-amber-900">No current review link is available. Refresh the quote status or ask the office to review its expiration.</p>{/if}
		{#if message}<p class="mt-3 text-sm" role="status">{message}</p>{/if}
	</section>
{/if}
