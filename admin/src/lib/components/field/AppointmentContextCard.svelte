<script lang="ts">
  import { formatDateTime } from '$lib/utils/format';
  import type { FieldEstimateContextDetails } from '$lib/api/types';

  export let details: FieldEstimateContextDetails;
  export let compact = false;
</script>

<div class={`card border border-brand-100/80 ${compact ? 'p-4' : 'p-5'}`}>
  <div class="flex items-start gap-3">
    <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-2xl">
      📍
    </div>
    <div class="min-w-0 flex-1">
      <div class="flex items-center gap-2 flex-wrap">
        <h2 class="text-base font-semibold text-ink-950">{details.customerName || 'New estimate'}</h2>
        {#if details.estimateNumber}
          <span class="badge badge-blue">Estimate {details.estimateNumber}</span>
        {/if}
      </div>
      {#if details.customerCompany}
        <p class="mt-1 text-sm text-ink-500">{details.customerCompany}</p>
      {/if}
      <p class="mt-2 text-sm text-ink-700">{details.projectAddress || 'Project address not set yet'}</p>
    </div>
  </div>

  <div class={`mt-4 grid ${compact ? 'grid-cols-1 gap-2' : 'grid-cols-1 gap-3'} text-sm`}>
    {#if details.appointmentDateTime}
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] font-semibold uppercase tracking-[0.2em] text-ink-500">Appointment</p>
        <p class="mt-1 font-medium text-ink-900">{formatDateTime(details.appointmentDateTime)}</p>
      </div>
    {/if}
    <div class="rounded-xl bg-ink-50 px-3 py-2">
      <p class="text-[11px] font-semibold uppercase tracking-[0.2em] text-ink-500">Estimator</p>
      <p class="mt-1 font-medium text-ink-900">{details.estimatorName || 'Estimator not set yet'}</p>
    </div>
    {#if details.projectName}
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] font-semibold uppercase tracking-[0.2em] text-ink-500">Project</p>
        <p class="mt-1 font-medium text-ink-900">{details.projectName}</p>
      </div>
    {/if}
  </div>
</div>
