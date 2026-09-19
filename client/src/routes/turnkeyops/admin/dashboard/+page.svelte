<script lang="ts">
 import { tenants } from '$lib/config/tenants';
 const tradeCount = new Set(tenants.map(tenant => tenant.tradeProfile)).size;
</script>
<section class="space-y-5 rounded-lg border border-[var(--shell-border)] bg-white p-5">
 <div><h1 class="text-2xl font-semibold text-[var(--text-strong)]">Platform overview</h1>
 <p class="mt-2 text-sm text-[var(--text-muted)]">Configured company surfaces. Operational records and permissions are managed within each company.</p></div>
 <div class="grid gap-4 sm:grid-cols-2">
  <div><p class="text-sm text-[var(--text-muted)]">Configured companies</p><p class="text-3xl font-semibold">{tenants.length}</p></div>
  <div><p class="text-sm text-[var(--text-muted)]">Configured trade profiles</p><p class="text-3xl font-semibold">{tradeCount}</p></div>
 </div>
 <div class="divide-y divide-[var(--shell-border)]">
  {#each tenants as tenant}
   <div class="flex flex-wrap items-center justify-between gap-3 py-4">
    <div><h2 class="font-semibold">{tenant.name}</h2><p class="text-sm text-[var(--text-muted)]">{tenant.tradeLabel}</p></div>
    <a class="rounded-md border border-[var(--shell-border)] px-4 py-2 text-sm font-semibold" href={tenant.adminPath}>Open {tenant.shortName} admin</a>
   </div>
  {/each}
 </div>
 <p class="text-sm text-[var(--text-muted)]">Launch readiness, incident counts, and release confidence are not tracked in this view.</p>
</section>
