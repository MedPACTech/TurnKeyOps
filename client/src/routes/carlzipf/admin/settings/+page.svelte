<script lang="ts">
 import { locksmithCatalog, locksmithLaborDefaults } from '$lib/locksmith';
 import type { LocksmithSettings } from '$lib/server/locksmith-settings';
 import type { PageProps } from './$types';
 let { data, form }: PageProps = $props();
 let catalog = $state<LocksmithSettings['catalog']>([]);
 let loadedVersion = $state('');
 $effect(() => {
  if (data.version !== loadedVersion) {
   catalog = structuredClone(data.settings.catalog);
   loadedVersion = data.version;
  }
 });
 const toggleJobType = (index: number, type: 'residential' | 'commercial', checked: boolean) => {
  const current = catalog[index].jobTypes;
  catalog[index].jobTypes = checked ? [...new Set([...current, type])] : current.filter(value => value !== type);
 };
 const addItem = () => catalog.push({ id: '', name: '', jobTypes: ['residential'], unitPrice: 0, sample: true });
 const loadSamples = () => { catalog = locksmithCatalog.map(item => ({
  id: item.id, name: item.name, jobTypes: [...item.jobTypes], unitPrice: item.unitPrice,
  sample: true, ...(item.externalId ? { externalId: item.externalId } : {})
 })); };
</script>
<svelte:head><title>Quote policy · Carl Zipf</title></svelte:head>
<div class="mx-auto max-w-3xl space-y-6 pb-10">
 <header><p class="text-sm font-semibold text-[var(--accent-text)]">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">Quote policy</h1><p class="mt-3 text-[var(--text-muted)]">Configure pricing defaults for residential and commercial work in one shared workspace.</p></header>
 <p class="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">Field quotes use saved labor, tax, and catalog prices. <a class="font-semibold underline" href="#tax-rate">Set the tax rate here</a>, including 0% if appropriate. Sample items cannot appear on a customer quote. Confirm Carl Zipf’s actual prices and tax treatment before enabling an item for quoting. Public booking remains off.</p>
 {#if form?.message}<p role="status" class="text-emerald-800">{form.message}</p>{/if}
 {#if form?.error}<p role="alert" class="text-red-700">{form.error}</p>{/if}
 <form method="POST" action="?/save" class="grid gap-6 rounded-xl bg-white p-6 shadow-sm">
  <input type="hidden" name="version" value={data.version} />
  <label class="grid gap-2">Labor rate per hour ($)<input class="rounded border p-3" type="number" name="laborRatePerHour" min="0" step="0.01" value={data.settings.pricing.laborRatePerHour ?? ''} /><span class="text-sm text-slate-500">Leave blank until Carl Zipf confirms pricing.</span></label>
  <label id="tax-rate" class="grid gap-2 rounded-lg border border-amber-200 bg-amber-50 p-4">Tax rate (%)<input class="rounded border p-3" type="number" name="taxPercent" min="0" max="100" step="0.01" value={data.settings.pricing.taxPercent ?? ''} /><span class="text-sm text-slate-600">Enter 0 when no tax applies. Blank blocks a customer quote until this is confirmed.</span></label>
  <fieldset class="grid gap-4 rounded-lg border p-4"><legend class="px-2 font-semibold">Standard labor hours per opening</legend><p class="text-sm text-slate-600">These starter values are provisional. Confirm them with Carl Zipf. A technician can add hours in the field and must explain why.</p>
   {#each ['residential', 'commercial'] as jobType}<div class="grid gap-3"><h3 class="font-semibold capitalize">{jobType}</h3><div class="grid gap-3 sm:grid-cols-2">{#each Object.keys(locksmithLaborDefaults[jobType as 'residential' | 'commercial']) as service}<label class="grid gap-1 text-sm">{service}<input class="rounded border p-2" type="number" required min="0.25" max="100" step="0.25" name={`laborHours:${jobType}:${service}`} value={data.settings.pricing.laborHoursByJobType[jobType as 'residential' | 'commercial'][service]} /></label>{/each}</div></div>{/each}
  </fieldset>
  <label class="grid gap-2">Maximum tech discount (%)<input class="rounded border p-3" type="number" required name="maxDiscountPercent" min="0" max="100" step="0.01" value={data.settings.pricing.maxDiscountPercent} /></label>
  <label class="grid gap-2">Office approval above total ($)<input class="rounded border p-3" type="number" name="approvalAboveTotal" min="0" step="0.01" value={data.settings.pricing.approvalAboveTotal ?? ''} /><span class="text-sm text-slate-500">Leave blank for no amount threshold.</span></label>
  <label class="flex items-center gap-3"><input type="checkbox" name="requireOfficeApproval" checked={data.settings.pricing.requireOfficeApproval} />Require office approval for every quote</label>
  <button class="min-h-11 rounded-lg bg-[var(--accent-text)] px-5 font-semibold text-white" type="submit">Save quote policy</button>
 </form>
 <section class="space-y-4 rounded-xl bg-white p-6 shadow-sm" aria-labelledby="catalog-heading">
  <div class="flex flex-wrap items-start justify-between gap-3"><div><h2 id="catalog-heading" class="text-xl font-bold">Door and hardware catalog</h2><p class="mt-1 text-sm text-slate-600">Products are shared across the workspace and filtered by residential or commercial job type. External IDs can be mapped to Carl Zipf’s product database later.</p></div><span class="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold">{catalog.filter(item => !item.sample).length} enabled for quotes</span></div>
  <div class="flex flex-wrap gap-3"><button type="button" class="rounded-lg border px-4 py-2 text-sm font-semibold" onclick={addItem}>Add item</button><button type="button" class="rounded-lg border px-4 py-2 text-sm font-semibold" onclick={loadSamples}>Load sample products</button></div>
  <form method="POST" action="?/catalog" class="space-y-4">
   <input type="hidden" name="version" value={data.version} /><input type="hidden" name="catalogJson" value={JSON.stringify(catalog)} />
   {#each catalog as item, index (index)}
    <fieldset class="grid gap-3 rounded-lg border p-4 sm:grid-cols-2"><legend class="px-2 text-sm font-semibold">Item {index + 1}</legend>
     <label class="grid gap-1 text-sm">Product ID<input class="rounded border p-2" maxlength="100" bind:value={item.id} /></label>
     <label class="grid gap-1 text-sm">Name<input class="rounded border p-2" maxlength="200" bind:value={item.name} /></label>
     <label class="grid gap-1 text-sm">Unit price ($)<input class="rounded border p-2" type="number" min="0" max="1000000" step="0.01" bind:value={item.unitPrice} /></label>
     <label class="grid gap-1 text-sm">External product ID (optional)<input class="rounded border p-2" maxlength="100" value={item.externalId ?? ''} oninput={(event) => item.externalId = event.currentTarget.value} /></label>
     <div class="flex flex-wrap items-center gap-4 text-sm"><label class="flex items-center gap-2"><input type="checkbox" checked={item.jobTypes.includes('residential')} onchange={(event) => toggleJobType(index, 'residential', event.currentTarget.checked)} />Residential</label><label class="flex items-center gap-2"><input type="checkbox" checked={item.jobTypes.includes('commercial')} onchange={(event) => toggleJobType(index, 'commercial', event.currentTarget.checked)} />Commercial</label></div>
     <div class="flex flex-wrap items-center justify-between gap-3 text-sm"><label class="flex items-center gap-2"><input type="checkbox" bind:checked={item.sample} />Sample only (cannot be quoted)</label><button type="button" class="text-red-700 underline" onclick={() => catalog.splice(index, 1)}>Remove</button></div>
    </fieldset>
   {:else}<p class="rounded-lg border border-dashed p-5 text-sm text-slate-600">No products configured. Add an item or load the illustrative sample list.</p>{/each}
   <button class="min-h-11 w-full rounded-lg bg-[var(--accent-text)] px-5 font-semibold text-white" type="submit">Save hardware catalog</button>
  </form>
 </section>
 <a href="/carlzipf/admin/users" class="font-semibold underline">Manage tech capabilities →</a>
</div>
