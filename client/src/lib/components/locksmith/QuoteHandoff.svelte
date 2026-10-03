<script lang="ts">
	import { createFieldSource, handoffErrors, standardLaborHours, type FieldEstimate, type FieldHandoffResult, type FieldPricingContext } from '$lib/locksmith-handoff';
	import type { FieldDraft } from '$lib/locksmith-drafts';
	import type { OfflinePhoto } from '$lib/locksmith-offline';
	import { fieldScopeMatchesEstimate } from '$lib/locksmith-review';
	import IssuedQuoteLink from './IssuedQuoteLink.svelte';
	let { draft = $bindable(), pricing, pricingError, online, available, persist, changed, getPhotos, busy = $bindable(false) }: {
		draft: FieldDraft; pricing: FieldPricingContext | null; pricingError: string; online: boolean; available: boolean;
		busy?: boolean; persist: () => Promise<boolean>; changed: () => void; getPhotos: () => OfflinePhoto[];
	} = $props();
	let message = $state('');
	let issues = $state<string[]>([]);
	let estimate = $state<FieldEstimate | null>(null);
	let photoMessage = $state('');
	let matchesServer = $derived(estimate ? fieldScopeMatchesEstimate(draft, estimate) : false);
	let standardHours = $derived(pricing ? standardLaborHours(draft, pricing) : 0);
	const money = (amount: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
	async function prepare() {
		if (busy || !online || !available) return;
		if (draft.laborHours && draft.laborHours < standardHours) { draft.laborHours = 0; draft.laborOverrideReason = ''; changed(); }
		issues = handoffErrors(draft, true);
		if (!pricing) issues.push('Reconnect and load the configured pricing context before preparing a quote.');
		if (pricing?.taxPercent === null) issues.push('The office must set Tax rate in Admin → Quote policy before preparing a quote; zero is supported.');
		if (pricing && (draft.laborHours ?? standardHours) < standardHours && (draft.laborHours ?? 0) !== 0) issues.push('Labor hours cannot be less than the configured standard.');
		if ((draft.laborHours ?? 0) > standardHours && !draft.laborOverrideReason?.trim()) issues.push('Explain why this opening needs additional labor hours.');
		if (Math.max(draft.laborHours ?? 0, standardHours) > 0 && pricing?.laborRatePerHour === null) issues.push('The office must configure an hourly labor rate before pricing labor.');
		if (pricing && draft.openings.some((opening) => opening.items.some((line) => !pricing.catalog.some((item) => item.id === line.productId && item.jobTypes.includes(draft.jobType))))) issues.push('Replace unavailable or sample items with configured catalog items.');
		if (issues.length) return;
		busy = true; message = ''; photoMessage = '';
		try {
			if (!draft.handoff) { const requestId = crypto.randomUUID(); draft.handoff = { requestId, source: createFieldSource(draft, requestId) }; changed(); }
			if (!await persist()) { message = 'Save the device draft successfully before handing it off. Your stable request reference protects retries.'; return; }
			const response = await fetch('/carlzipf/tech/handoff', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(draft) });
			const result = await response.json() as FieldHandoffResult & { message?: string };
			if (result.requestSaved) draft.handoff.requestSavedAt = new Date().toISOString();
			if (result.estimate) {
				estimate = result.estimate; draft.handoff.estimateVersion = result.estimate.version; draft.handoff.estimateSavedAt = result.estimate.savedAtUtc;
				message = result.recovered ? 'An existing server draft was recovered. Review it below; press Prepare server quote draft again to save your current field changes.' : 'Server-priced estimate draft saved. It has not been issued, emailed, signed, or scheduled.';
			} else message = `${result.requestSaved ? 'The customer request was saved, but detailed quote preparation was not confirmed. Measurements remain on this device. ' : ''}${result.error || result.message || 'The handoff was not confirmed. Retry with the same reference.'}`;
			changed();
			if (!await persist()) message += ' The latest handoff status could not be saved on this device. Keep this tab open; retries use the same request reference.';
			if (result.requestSaved && getPhotos().length) await uploadPhotos();
		} catch { message = 'The handoff outcome could not be confirmed. Your device draft and request reference are retained. Reconnect and retry; do not create a replacement request.'; }
		finally { busy = false; }
	}
	async function uploadPhotos() {
		if (!draft.handoff?.requestSavedAt) return;
		const pending = getPhotos();
		let confirmed = 0;
		try {
			while (pending.length) {
				const batch: OfflinePhoto[] = []; let bytes = 0;
				while (pending.length && batch.length < 10 && bytes + pending[0].blob.size <= 40 * 1024 * 1024) { const photo = pending.shift()!; batch.push(photo); bytes += photo.blob.size; }
				const data = new FormData(); data.set('requestId', draft.handoff.requestId);
				batch.forEach((photo) => { const name = `${photo.openingId}-${photo.name}`; data.append('files', photo.blob, name); });
				const response = await fetch('/carlzipf/tech/photos', { method: 'POST', body: data });
				if (!response.ok) throw new Error('Photo upload not confirmed');
				confirmed += batch.length;
			}
			photoMessage = `${confirmed} photo(s) uploaded to the shared request. Device copies remain available.`;
		} catch { photoMessage = `${confirmed} photo(s) confirmed in completed batches; remaining uploads were not confirmed. Retry the handoff to retry all photos safely. Keep the device copy.`; }
	}
	async function reviewQuote(issue = false) {
		if (busy || !online || !draft.handoff || (issue && (!estimate || !matchesServer))) return;
		busy = true;
		try {
			const response = await fetch(`/carlzipf/tech/quote${issue ? '' : `?requestId=${encodeURIComponent(draft.handoff.requestId)}`}`, issue ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ requestId: draft.handoff.requestId, expectedVersion: estimate!.version }) } : {});
			const result = await response.json();
			if (!response.ok) throw new Error(result.message || 'The quote status could not be confirmed. Refresh before retrying.');
			estimate = result.estimate;
			message = issue ? 'Customer review link confirmed. Open it for the customer or copy it below. No email or text has been sent.' : 'Current server quote loaded. Customer actions are recorded on the shared review page.';
		} catch (cause) { message = cause instanceof Error ? cause.message : 'Quote status was not confirmed. Reconnect and refresh before retrying.'; }
		finally { busy = false; }
	}
</script>

<section class="card mt-5">
	<h2 class="text-lg font-semibold">Prepare a shared quote draft</h2>
	<p class="mt-2 text-sm text-gray-600">Send opening measurements and selected hardware to the existing TurnKeyOps request and estimate modules. The server applies configured prices and approval rules.</p>
	{#if pricingError}<p class="mt-3 text-sm text-amber-900">{pricingError}</p>{:else if !pricing?.catalog.length}<p class="mt-3 text-sm text-amber-900">No authoritative product catalog is configured yet. Labor-only quotes can use the configured hourly rate; sample items cannot be priced.</p>{/if}
	<div inert={busy} class="mt-4 grid gap-4 sm:grid-cols-2" oninput={changed} onchange={changed}>
		<label class="label">Customer email<input class="input mt-1" type="email" readonly={Boolean(draft.handoff)} bind:value={draft.contactEmail} autocomplete="off" /></label>
		<label class="label">Customer phone<input class="input mt-1" type="tel" readonly={Boolean(draft.handoff)} bind:value={draft.contactPhone} autocomplete="off" /></label>
		<div class="label">Labor hours<p class="mt-1 text-sm font-semibold">Standard: {standardHours} hour{standardHours === 1 ? '' : 's'} for {draft.openings.length} opening{draft.openings.length === 1 ? '' : 's'}</p><p class="mt-1 text-xs text-gray-500">Calculated from the office’s per-opening service standards. Add hours if site conditions require more time.</p><label class="mt-2 block text-sm">Additional hours needed<input class="input mt-1" type="number" min="0" max="1000" step="0.25" value={Math.max(0, (draft.laborHours ?? 0) - standardHours)} oninput={(event) => { draft.laborHours = standardHours + Number(event.currentTarget.value || 0); if (draft.laborHours === standardHours) draft.laborOverrideReason = ''; changed(); }} /></label><p class="mt-1 text-xs text-gray-500">Quote labor total: {Math.max(draft.laborHours ?? 0, standardHours)} hours</p></div>
		{#if (draft.laborHours ?? 0) > standardHours}<label class="label">Reason for additional labor<input class="input mt-1" maxlength="500" bind:value={draft.laborOverrideReason} placeholder="Example: damaged jamb needs repair before installation" /></label>{/if}
		<label class="label">Requested discount (%)<input class="input mt-1" type="number" min="0" max="100" step="0.1" bind:value={draft.discountPercent} placeholder="0" /></label>
	</div>
	{#if draft.handoff}<p class="mt-4 break-all text-xs text-gray-500">Shared request reference: {draft.handoff.requestId}{draft.handoff.requestSavedAt ? ' · saved to office' : ' · confirmation pending'}. The original customer, contact, property and job type are locked to this reference; the office can correct the shared request.</p>{/if}
	{#if issues.length}<ul class="mt-4 list-disc space-y-1 pl-5 text-sm text-red-800" role="alert">{#each issues as issue}<li>{issue}</li>{/each}</ul>{/if}
	<button class="btn-primary mt-4" disabled={busy || !online || !available || !pricing || estimate?.status === 'sent'} onclick={prepare}>{busy ? 'Working…' : 'Prepare server quote draft'}</button>
	<p class="mt-2 text-xs text-gray-500">Requires connectivity. The customer request is saved first; photos upload separately. No signature, delivery, booking, or payment is performed here.</p>
	{#if message}<p class="mt-4 text-sm" role="status">{message}</p>{/if}
	{#if photoMessage}<p class="mt-2 text-sm" role="status">{photoMessage}</p>{/if}
	{#if draft.handoff?.requestSavedAt}<div class="mt-4 flex flex-wrap gap-3"><button class="btn-secondary" disabled={busy || !online} onclick={() => reviewQuote()}>Refresh quote & customer status</button>{#if estimate?.status === 'ready-to-send'}<button class="btn-primary" disabled={busy || !online || !matchesServer} onclick={() => reviewQuote(true)}>Issue customer review link</button>{/if}</div>{#if estimate?.status === 'ready-to-send' && !matchesServer}<p class="mt-2 text-sm text-amber-900">The server quote scope differs from this device draft. Reconcile the changes before issuing a link; the office can review the saved server revision.</p>{/if}{/if}
	{#if estimate?.locksmithPricing}<div class="mt-5 rounded-md border border-gray-200 p-4"><div class="flex flex-wrap justify-between gap-3"><h3 class="font-semibold">Server estimate · revision {estimate.revisionNumber}</h3><span class="badge-yellow">Server status: {estimate.status}</span></div><p class="mt-3 text-2xl font-bold">{money(estimate.locksmithPricing.total)}</p><p class="mt-2 text-sm text-gray-600">Discount: {money(estimate.locksmithPricing.discountAmount)} · Tax: {money(estimate.locksmithPricing.taxAmount)}{estimate.locksmithPricing.taxPercent === null ? ' (not configured)' : ''}</p><p class="mt-2 text-xs text-gray-500">Pricing policy {estimate.locksmithPricing.policyVersion} · saved {new Date(estimate.savedAtUtc).toLocaleString()}. This amount reflects the saved server revision; later field edits need another handoff.</p>{#if estimate.locksmithPricing.officeApprovedAtUtc}<p class="mt-3 text-sm text-green-800">Office pricing approval recorded.</p>{:else if estimate.locksmithPricing.approvalReasons.length}<ul class="mt-3 list-disc space-y-1 pl-5 text-sm text-amber-900">{#each estimate.locksmithPricing.approvalReasons as reason}<li>{reason}</li>{/each}</ul>{/if}</div><IssuedQuoteLink {estimate} />{/if}
</section>
