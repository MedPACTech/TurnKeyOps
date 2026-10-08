<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { beforeNavigate, invalidateAll } from '$app/navigation';
	import { locksmithServices } from '$lib/locksmith';
	import { draftStorageKey, fieldDraftErrors, measurementFields, newOpening, readFieldDraft, type FieldDraft, type FieldJobType } from '$lib/locksmith-drafts';
	import { acceptedPhotoTypes, catalogFingerprint, deleteDeviceDraft, DraftConflictError, InvalidDeviceDraftError, isDraftStale, loadDeviceDraft, maximumDraftPhotoBytes, maximumPhotoBytes, saveDeviceDraft, type OfflinePhoto, type SavedFieldDraft } from '$lib/locksmith-offline';
	import InstallFieldApp from './InstallFieldApp.svelte';
	import QuoteHandoff from './QuoteHandoff.svelte';
	import FieldBob from './FieldBob.svelte';
	import type { FieldPricingContext } from '$lib/locksmith-handoff';
	type CatalogItem = { id: string; name: string; jobTypes: FieldJobType[]; unitPrice: number };
	let { context, catalog, pricingContext = null, pricingError = '' }: { context: { tenantId: string; userId: string; capabilities: FieldJobType[] }; catalog: CatalogItem[]; pricingContext?: FieldPricingContext | null; pricingError?: string } = $props();
	let configuredCatalog = $derived(Boolean(pricingContext?.catalog.length));
	const identity = untrack(() => ({ ...context, capabilities: [...context.capabilities] }));
	let draft = $state<FieldDraft>({ version: 1, jobType: identity.capabilities[0] ?? 'residential', customer: '', site: '', openings: [] });
	let message = $state('Save measurements and photos on this device before leaving the workspace.');
	let online = $state(true);
	let savedDraft = $state<SavedFieldDraft | null>(null);
	let currentRecord = $state<SavedFieldDraft | null>(null);
	let legacyDraft = $state<FieldDraft | null>(null);
	let photos = $state<Record<string, (OfflinePhoto & { url: string })[]>>({});
	let dirty = $state(false);
	let editVersion = 0;
	let saving = $state(false);
	let quoteBusy = $state(false);
	let refreshingCatalog = $state(false);
	let visitGeneration = $state(0);
	let storageReady = $state(false);
	let invalidSavedRevision = $state<string | null>(null);
	let invalidSavedDraft = $state(false);
	let conflict = $state(false);
	let now = $state(Date.now());
	let channel: BroadcastChannel | undefined;
	const storageKey = draftStorageKey(identity.tenantId, identity.userId);
	const money = (amount: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
	let catalogVersion = $derived(catalogFingerprint(catalog));
	let availableItems = $derived(catalog.filter((item) => item.jobTypes.includes(draft.jobType)));
	let materialSubtotal = $derived(draft.openings.reduce((total, opening) => total + opening.items.reduce((sum, line) => sum + (availableItems.find((item) => item.id === line.productId)?.unitPrice ?? 0) * (Number.isInteger(line.quantity) && line.quantity > 0 && line.quantity <= 999 ? line.quantity : 0), 0), 0));
	let validationErrors = $derived([...fieldDraftErrors(draft), ...draft.openings.flatMap((opening, index) => opening.items.some((line) => !availableItems.some((item) => item.id === line.productId)) ? [`Opening ${index + 1}: a selected item is no longer available for this job type. Choose another item or remove it.`] : [])]);
	let unknownCount = $derived(draft.openings.reduce((total, opening) => total + measurementFields.filter(([key]) => !opening.measurements[key].value || opening.measurements[key].certainty !== 'measured').length, 0));
	let stale = $derived(currentRecord ? isDraftStale(currentRecord, catalogVersion, now) : false);
	function markDirty() { dirty = true; editVersion++; }
	async function refreshCatalog() {
		if (refreshingCatalog) return;
		refreshingCatalog = true;
		try { await invalidateAll(); message = 'Catalog refreshed. Check selected items and prices before preparing the quote.'; }
		catch { message = 'Could not refresh the catalog. Check your connection and try again.'; }
		finally { refreshingCatalog = false; }
	}
	beforeNavigate(({ cancel, willUnload }) => {
		if (!willUnload && (dirty || quoteBusy) && !window.confirm('Leave this field draft? Unsaved measurements and photos will be lost.')) cancel();
	});
	onMount(() => {
		let mounted = true;
		online = navigator.onLine;
		void loadDeviceDraft(identity).then((saved) => {
			if (!mounted) return;
			savedDraft = saved; storageReady = true;
			if (!saved) { try { const raw = localStorage.getItem(storageKey); if (raw) legacyDraft = readFieldDraft(raw, identity.capabilities); } catch { /* Legacy storage may be disabled; IndexedDB is independent. */ } }
		}).catch((cause) => { if (mounted) { message = cause instanceof Error ? cause.message : 'Device storage is unavailable. Keep this tab open.'; if (cause instanceof InvalidDeviceDraftError) { invalidSavedDraft = true; invalidSavedRevision = cause.revision; storageReady = true; } } });
		if ('BroadcastChannel' in window) {
			channel = new BroadcastChannel('turnkey-locksmith-drafts');
			channel.onmessage = (event) => { if (event.data?.scope === storageKey && event.data.revision !== currentRecord?.revision) { conflict = true; message = 'Another tab changed the saved draft. Review the latest device copy before saving.'; } };
		}
		const update = () => { online = navigator.onLine; };
		const beforeLeave = (event: BeforeUnloadEvent) => { if (dirty || quoteBusy) { event.preventDefault(); event.returnValue = ''; } };
		const timer = window.setInterval(() => { now = Date.now(); }, 60_000);
		window.addEventListener('online', update); window.addEventListener('offline', update); window.addEventListener('beforeunload', beforeLeave);
		return () => { mounted = false; channel?.close(); window.clearInterval(timer); window.removeEventListener('online', update); window.removeEventListener('offline', update); window.removeEventListener('beforeunload', beforeLeave); Object.values(photos).flat().forEach((photo) => URL.revokeObjectURL(photo.url)); };
	});
	function changeType(next: FieldJobType) {
		if (!identity.capabilities.includes(next) || next === draft.jobType) return;
		if (draft.openings.some((opening) => opening.items.length) && !window.confirm('Changing job type clears the selected hardware. Keep opening measurements and continue?')) return;
		draft.jobType = next; draft.openings.forEach((opening) => { opening.items = []; }); markDirty();
	}
	function applyRecord(saved: SavedFieldDraft) {
		Object.values(photos).flat().forEach((photo) => URL.revokeObjectURL(photo.url));
		photos = {};
		for (const photo of saved.photos) photos[photo.openingId] = [...(photos[photo.openingId] ?? []), { ...photo, url: URL.createObjectURL(photo.blob) }];
		visitGeneration++; draft = structuredClone(saved.draft); currentRecord = saved; savedDraft = null; legacyDraft = null; dirty = false; conflict = false;
		message = `Restored measurements and ${saved.photos.length} photo(s) from this device. Office handoff status is separate from this device copy.`;
	}
	async function restore() {
		if (saving || quoteBusy || (dirty && !window.confirm('Replace this tab’s unsaved measurements and photos with the latest saved draft?'))) return;
		try {
			const latest = await loadDeviceDraft(identity);
			if (latest) applyRecord(latest);
			else { currentRecord = null; savedDraft = null; conflict = false; message = 'The device copy was deleted. Your current tab is unchanged; save it to create a new device copy.'; markDirty(); }
		} catch (cause) { message = cause instanceof Error ? cause.message : 'Unable to load this device draft.'; }
	}
	async function save() {
		if (saving || conflict || invalidSavedDraft || !storageReady) return false;
		if (validationErrors.length) { message = 'Correct the measurements or quantities listed below before saving.'; return false; }
		saving = true;
		const savedEditVersion = editVersion;
		try {
			const snapshot: FieldDraft = JSON.parse(JSON.stringify(draft));
			const photoSnapshot = Object.values(photos).flat().map(({ id, openingId, name, blob }) => ({ id, openingId, name, blob }));
			const saved = await saveDeviceDraft(identity, snapshot, photoSnapshot, currentRecord?.revision ?? null, catalogVersion);
			currentRecord = saved; savedDraft = null; legacyDraft = null; dirty = savedEditVersion !== editVersion;
			try { localStorage.removeItem(storageKey); } catch { /* A successful device save must not be reported as failed because legacy cleanup is blocked. */ }
			channel?.postMessage({ scope: storageKey, revision: saved.revision });
			message = `Saved ${saved.photos.length} photo(s) and measurements on this device at ${new Date(saved.savedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}. Device copy saved; office handoff is a separate action.`;
				return !dirty;
		} catch (cause) { conflict = cause instanceof DraftConflictError; message = cause instanceof Error ? cause.message : 'Unable to save on this device. Keep this tab open.'; return false; }
		finally { saving = false; }
	}
	function addPhotos(id: string, files: FileList | null) {
		if (!files) return;
		let bytes = Object.values(photos).flat().reduce((sum, photo) => sum + photo.blob.size, 0);
		const valid = Array.from(files).filter((file) => { if (!acceptedPhotoTypes.includes(file.type) || file.size > maximumPhotoBytes || bytes + file.size > maximumDraftPhotoBytes) return false; bytes += file.size; return true; });
		photos[id] = [...(photos[id] ?? []), ...valid.map((file) => ({ id: crypto.randomUUID(), openingId: id, name: file.name, blob: file, url: URL.createObjectURL(file) }))];
		if (valid.length) markDirty();
		message = valid.length === files.length ? 'Photos added. Save the draft to keep them on this device.' : 'Some photos were skipped. Use JPEG, PNG, or WebP up to 10 MB each and 100 MB per draft.';
	}
	function removeOpening(id: string) {
		if (!window.confirm('Remove this opening and its measurements, hardware, and photos?')) return;
		(photos[id] ?? []).forEach((photo) => URL.revokeObjectURL(photo.url)); delete photos[id];
		draft.openings = draft.openings.filter((opening) => opening.id !== id); markDirty();
	}
	async function startNewVisit() {
		if (saving || quoteBusy || !storageReady || !window.confirm('Start another field visit? This removes the current device draft and its photos. Confirm that any work you need has reached the office. Existing office records remain unchanged.')) return;
		try {
			await deleteDeviceDraft(identity, currentRecord?.revision ?? savedDraft?.revision ?? invalidSavedRevision);
			try { localStorage.removeItem(storageKey); } catch { /* Older local storage may be unavailable. */ }
			Object.values(photos).flat().forEach((photo) => URL.revokeObjectURL(photo.url)); photos = {};
			draft = { version: 1, jobType: identity.capabilities[0] ?? 'residential', customer: '', site: '', openings: [] };
			currentRecord = null; savedDraft = null; legacyDraft = null; invalidSavedDraft = false; invalidSavedRevision = null; conflict = false; dirty = false; visitGeneration++;
			channel?.postMessage({ scope: storageKey, revision: null }); message = 'New field visit started. Save its measurements and photos before leaving.';
		} catch (cause) { conflict = cause instanceof DraftConflictError; message = cause instanceof Error ? cause.message : 'Could not start another visit.'; }
	}
	async function clearSavedDraft() {
		if (saving || quoteBusy || !window.confirm('Delete this account’s saved measurements and photos from this device? Your current tab remains open.')) return;
		saving = true;
		try {
			await deleteDeviceDraft(identity, currentRecord?.revision ?? savedDraft?.revision ?? invalidSavedRevision);
			try { localStorage.removeItem(storageKey); } catch { /* IndexedDB deletion succeeded. */ }
			currentRecord = null; savedDraft = null; legacyDraft = null; conflict = false; invalidSavedDraft = false; invalidSavedRevision = null; markDirty();
			channel?.postMessage({ scope: storageKey, revision: null }); message = 'Saved measurements and photos removed from this device. This tab is unchanged.';
		} catch (cause) { conflict = cause instanceof DraftConflictError; message = cause instanceof Error ? cause.message : 'Could not clear device storage.'; }
		finally { saving = false; }
	}
</script>


<svelte:head><title>Field workspace | Carl Zipf Lock Shop</title><meta name="robots" content="noindex, nofollow" /></svelte:head>

<div class="mx-auto max-w-6xl px-4 py-6 sm:px-6">
	<InstallFieldApp />
	<header class="flex flex-wrap items-start justify-between gap-4 border-b border-gray-200 pb-5">
		<div><p class="text-xs font-bold uppercase tracking-widest text-amber-800">Carl Zipf Lock Shop · Field workspace</p><h1 class="mt-2 text-3xl font-bold">Measure. Specify. Prepare.</h1><p class="mt-2 max-w-2xl text-sm text-gray-600">Build an opening-by-opening draft for residential or commercial work.</p></div>
		<div class="flex flex-wrap items-center gap-3"><a class="btn-secondary" href="/carlzipf/admin/jobs">Today’s Jobs</a><a class="btn-secondary" href="/carlzipf/tech/invoices">Invoices & completion</a><span class={online ? 'badge-gray' : 'badge-yellow'}>{online ? 'Field workspace · device draft' : 'Offline · device draft'}</span></div>
	</header>
	{#if !context.capabilities.length}
		<section class="card mt-6"><h2 class="text-lg font-semibold">Job types need to be assigned</h2><p class="mt-2 text-sm text-gray-600">Ask an administrator to enable Residential, Commercial, or both in your staff settings before capturing an opening.</p></section>
	{:else}
		<div class="mt-5 rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950"><strong>Field capture.</strong> {configuredCatalog ? 'Configured catalog prices are shown for planning; the server calculates the quote.' : 'Sample catalog prices are illustrative.'} This device draft is not a customer quote. Automatic synchronization is not connected. Quote issuance, signatures, email, live scheduling, and payments require a verified online workflow.</div>
		<FieldBob {draft} {online} />
		{#if savedDraft}
			<div class="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-md border border-blue-200 bg-blue-50 p-4"><p class="text-sm">A saved {savedDraft.draft.jobType} draft with {savedDraft.draft.openings.length} opening(s) and {savedDraft.photos.length} photo(s) is available on this device.</p><div class="flex gap-2"><button class="btn-secondary" onclick={restore}>Restore draft</button><button class="btn-secondary" onclick={() => { savedDraft = null; }}>Dismiss</button></div></div>
		{/if}

		{#if legacyDraft}<div class="mt-4 rounded-md border border-blue-200 bg-blue-50 p-4 text-sm">An older text-only draft is available. <button class="underline" onclick={() => { if (dirty && !window.confirm('Replace current edits with the older text-only draft?')) return; Object.values(photos).flat().forEach((photo) => URL.revokeObjectURL(photo.url)); photos = {}; draft = legacyDraft!; legacyDraft = null; markDirty(); message = 'Older draft restored. Save it to upgrade device storage.'; }}>Restore older draft</button></div>{/if}
		{#if invalidSavedDraft}<div class="mt-4 rounded-md border border-amber-300 bg-amber-50 p-4 text-sm" role="alert">The saved device draft cannot be restored with your current capabilities or format. Use “Clear saved draft” to remove it before saving new work.</div>{/if}
		{#if conflict}<div class="mt-4 rounded-md border border-red-300 bg-red-50 p-4 text-sm text-red-950" role="alert"><strong>Another tab changed this device draft.</strong> Saving is paused to protect that copy. Loading the latest replaces this tab’s unsaved edits. <button class="mt-2 block underline" onclick={restore} disabled={saving}>Load latest device draft</button></div>{/if}
		{#if stale}<div class="mt-4 rounded-md border border-amber-300 bg-amber-50 p-4 text-sm text-amber-950"><strong>Review this older draft.</strong> It is over 24 hours old or the sample catalog has changed. Recheck measurements, hardware, and online pricing before preparing a customer quote.</div>{/if}
		{#if !online}<div class="mt-4 rounded-md border border-gray-300 bg-white p-4 text-sm">You can keep capturing and saving measurements and photos in this open workspace. Reopening or refreshing requires connectivity to verify your account. Live quotes, signing, email, and scheduling are unavailable offline.</div>{/if}
		<div class="mt-5 grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_300px]">
			<div inert={quoteBusy} class="space-y-5" oninput={markDirty} onchange={markDirty}>
				<section class="card"><h2 class="text-lg font-semibold">Visit details</h2><p class="mt-1 text-sm text-gray-500">Capture a draft request. This does not create or assign a job.</p><div class="mt-4 grid gap-4 sm:grid-cols-2">
					<label class="label">Job type<select disabled={Boolean(draft.handoff)} class="input mt-1 capitalize" value={draft.jobType} onchange={(event) => { changeType(event.currentTarget.value as FieldJobType); event.currentTarget.value = draft.jobType; }}>{#each context.capabilities as jobType}<option value={jobType}>{jobType}</option>{/each}</select></label>
					<label class="label">Customer / company<input class="input mt-1" readonly={Boolean(draft.handoff)} bind:value={draft.customer} placeholder="Customer name" autocomplete="off" /></label>
					<label class="label sm:col-span-2">Property / site<input class="input mt-1" readonly={Boolean(draft.handoff)} bind:value={draft.site} placeholder="Address, building, or property name" autocomplete="off" /></label>
				</div></section>
				{#each draft.openings as opening, index (opening.id)}
					<section class="card"><div class="flex items-center justify-between gap-3"><h2 class="text-lg font-semibold">Opening {index + 1}</h2><button class="btn-secondary" onclick={() => removeOpening(opening.id)} aria-label={`Remove opening ${index + 1}`}>Remove</button></div>
						<div class="mt-4 grid gap-4 sm:grid-cols-2"><label class="label">Opening name<input class="input mt-1" bind:value={opening.name} placeholder="e.g. Front entry, Suite 102" /></label><label class="label">Work needed<select class="input mt-1" bind:value={opening.service}>{#each locksmithServices as service}<option>{service}</option>{/each}</select></label></div>
						<details class="mt-5 border-t border-gray-200 pt-4" open><summary class="cursor-pointer font-semibold">Measurements <span class="text-sm font-normal text-gray-500">· inches</span></summary><p class="mt-2 text-xs leading-relaxed text-gray-500">Record only accessible dimensions. Door slab, frame/unit, and rough opening are separate measurements. Estimated values need verification against the selected product’s template before ordering.</p>
							<div class="mt-4 grid gap-4 sm:grid-cols-2">{#each measurementFields as [key, label]}<div><label class="label" for={`${opening.id}-${key}`}>{label}</label><div class="flex gap-2"><input class="input min-w-0" id={`${opening.id}-${key}`} type="text" inputmode="decimal" placeholder="Unknown" bind:value={opening.measurements[key].value} /><select class="input max-w-32" aria-label={`${label} certainty for opening ${index + 1}`} bind:value={opening.measurements[key].certainty}><option value="unknown">Unknown</option><option value="measured">Measured</option><option value="estimated">Estimated</option></select></div></div>{/each}</div>
						</details>
						<label class="label mt-5">Handing / swing observations<input class="input mt-1" bind:value={opening.handing} placeholder="Describe viewed side, hinge side, and swing direction" /></label><p class="text-xs text-gray-500">Describe your viewing position. Confirm manufacturer handing conventions before ordering.</p>
						<label class="label mt-4">Existing hardware and site notes<textarea class="input mt-1" rows="3" bind:value={opening.notes} placeholder="Hinge and strike locations, lock function, threshold, clearance, condition, desired work…"></textarea></label>
						{#if draft.jobType === 'commercial'}<label class="label mt-4">Commercial opening observations<textarea class="input mt-1" rows="3" bind:value={opening.commercialNotes} placeholder="Single/pair, active leaf, closer/exit hardware, observed rating labels…"></textarea></label><p class="text-xs text-gray-500">Observed labels and notes do not certify code compliance.</p>{/if}
						<label class="label mt-4">Opening photos<input type="file" class="input mt-1" accept="image/jpeg,image/png,image/webp" multiple onchange={(event) => { addPhotos(opening.id, event.currentTarget.files); event.currentTarget.value = ''; }} /></label><p class="text-xs text-gray-500">Up to 10 MB each, 100 MB per draft. Save the draft to retain photos on this device. Photos are not uploaded.</p>
						<div class="mt-3 flex flex-wrap gap-3">{#each photos[opening.id] ?? [] as photo, photoIndex}<div class="w-28"><img class="h-24 w-28 rounded-md object-cover" src={photo.url} alt={`Opening photo: ${photo.name}`} /><button class="mt-1 text-xs underline" onclick={() => { URL.revokeObjectURL(photo.url); photos[opening.id] = photos[opening.id].filter((_, i) => i !== photoIndex); markDirty(); }}>Remove photo {photoIndex + 1}</button></div>{/each}</div>
						<div class="mt-5 border-t border-gray-200 pt-4"><div class="flex flex-wrap items-center justify-between gap-2"><h3 class="font-semibold">Doors & hardware</h3><button class="text-sm font-semibold underline" disabled={refreshingCatalog || !online} onclick={refreshCatalog}>{refreshingCatalog ? 'Refreshing…' : 'Refresh catalog'}</button></div><p class="mt-1 text-xs text-gray-500">{draft.jobType === 'commercial' ? 'Commercial' : 'Residential'} {configuredCatalog ? 'configured catalog' : 'starter catalog'} · {availableItems.length} item{availableItems.length === 1 ? '' : 's'} available · compatibility requires tech verification.</p>
							{#each opening.items as line, lineIndex}<div class="mt-3 flex flex-wrap items-end gap-2"><label class="label min-w-40 flex-1">{configuredCatalog ? 'Catalog item' : 'Sample item'}<select class="input mt-1" bind:value={line.productId}>{#if !availableItems.some((item) => item.id === line.productId)}<option value={line.productId} disabled>Previously selected item unavailable — choose another</option>{/if}{#each availableItems as item}<option value={item.id}>{item.name} · {money(item.unitPrice)}</option>{/each}</select></label><label class="label w-20">Quantity<input class="input mt-1" type="number" min="1" max="999" step="1" bind:value={line.quantity} /></label><button class="btn-secondary mb-1" aria-label={`Remove item ${lineIndex + 1} from opening ${index + 1}`} onclick={() => { opening.items = opening.items.filter((_, i) => i !== lineIndex); markDirty(); }}>×</button></div>{/each}
							<button class="btn-secondary mt-3" disabled={!availableItems.length} onclick={() => { opening.items = [...opening.items, { productId: availableItems[0].id, quantity: 1 }]; markDirty(); }}>{configuredCatalog ? 'Add catalog item' : 'Add sample item'}</button>
						</div>
					</section>
				{:else}<section class="card py-10 text-center"><h2 class="text-lg font-semibold">Start with an opening</h2><p class="mt-2 text-sm text-gray-500">Name each door or opening to keep measurements and hardware together.</p></section>{/each}
				<button class="btn-secondary w-full" onclick={() => { draft.openings = [...draft.openings, newOpening()]; markDirty(); }}>+ Add opening</button>
			</div>
			<aside class="card lg:sticky lg:top-5"><span class="badge-yellow">Device draft</span><h2 class="mt-3 text-lg font-semibold">Scope summary</h2><p class="mt-2 text-sm text-gray-600">{draft.openings.length} opening(s) · <span class="capitalize">{draft.jobType}</span></p><div class="mt-4 border-y border-gray-200 py-4"><p class="text-xs text-gray-500">{configuredCatalog ? 'Configured item subtotal' : 'Illustrative item subtotal'}</p><p class="mt-1 text-3xl font-bold">{money(materialSubtotal)}</p><p class="mt-2 text-xs leading-relaxed text-gray-500">{configuredCatalog ? 'Planning subtotal only.' : 'Sample prices only.'} Final labor rules, tax, customer discounts, and pricing guardrails have not been applied.</p></div><p class="mt-4 text-sm text-gray-600">{unknownCount} dimension(s) unverified or not recorded. Required measurements will depend on the work and selected products.</p>{#if validationErrors.length}<ul class="mt-3 list-disc space-y-2 pl-4 text-xs text-red-700" aria-label="Draft errors">{#each validationErrors as issue}<li>{issue}</li>{/each}</ul>{/if}<button class="btn-primary mt-5 w-full" onclick={save} disabled={saving || quoteBusy || conflict || invalidSavedDraft || !storageReady}>{saving ? 'Saving…' : 'Save draft & photos on device'}</button><p class="mt-3 text-xs leading-relaxed text-gray-500" aria-live="polite">{message}</p>{#if dirty}<p class="mt-2 text-xs font-semibold text-amber-800">Unsaved changes</p>{/if}<p class="mt-4 text-xs leading-relaxed text-gray-500">Use a trusted device. Saved measurements and photos remain in this browser for your account until cleared. Device storage can be removed by the browser; it is not an office backup.</p><button class="mt-3 text-xs underline" onclick={clearSavedDraft} disabled={saving || quoteBusy || !storageReady}>Clear saved draft</button></aside>
		</div>
	{/if}
	{#if context.capabilities.length}{#key visitGeneration}<QuoteHandoff bind:draft bind:busy={quoteBusy} pricing={pricingContext} {pricingError} {online} available={storageReady && !saving && !conflict && !invalidSavedDraft} persist={save} changed={markDirty} getPhotos={() => Object.values(photos).flat()} />{/key}<button class="btn-secondary mt-5" disabled={saving || quoteBusy || !storageReady} onclick={startNewVisit}>Start another field visit</button>{/if}
</div>
