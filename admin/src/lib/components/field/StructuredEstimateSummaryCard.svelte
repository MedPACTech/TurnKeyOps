<script lang="ts">
  import type { FieldEstimateIntakeValues } from '$lib/field-intake/types';

  export let values: FieldEstimateIntakeValues;

  function boolLabel(value: boolean | undefined) {
    if (value === undefined) return 'Pending';
    return value ? 'Yes' : 'No';
  }

  function fmt(value: number | undefined) {
    return value === undefined ? 'Pending' : String(value);
  }
</script>

<aside class="card border border-brand-100/80 p-4">
  <div class="flex items-center gap-3">
    <div class="flex h-10 w-10 items-center justify-center rounded-2xl bg-brand-100 text-xl">
      🧾
    </div>
    <div>
      <p class="text-[11px] font-semibold uppercase tracking-[0.2em] text-brand-600">Structured Summary</p>
      <h2 class="text-base font-semibold text-ink-950">Estimate inputs</h2>
    </div>
  </div>

  <div class="mt-4 space-y-3 text-sm">
    <div class="rounded-xl bg-ink-50 px-3 py-2">
      <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Job Type</p>
      <p class="mt-1 font-medium text-ink-900">{values.projectType ?? 'Pending'}</p>
    </div>

    <div class="grid grid-cols-2 gap-2">
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Length</p>
        <p class="mt-1 font-medium text-ink-900">{fmt(values.lengthFt)}{values.lengthFt !== undefined ? ' ft' : ''}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Width</p>
        <p class="mt-1 font-medium text-ink-900">{fmt(values.widthFt)}{values.widthFt !== undefined ? ' ft' : ''}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Depth</p>
        <p class="mt-1 font-medium text-ink-900">{fmt(values.depthIn)}{values.depthIn !== undefined ? '"' : ''}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Pours</p>
        <p class="mt-1 font-medium text-ink-900">{fmt(values.pourCount)}</p>
      </div>
    </div>

    <div class="grid grid-cols-1 gap-2">
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Demo</p>
        <p class="mt-1 font-medium text-ink-900">{boolLabel(values.demoRequired)}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Excavation</p>
        <p class="mt-1 font-medium text-ink-900">{boolLabel(values.excavationRequired)}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Pump</p>
        <p class="mt-1 font-medium text-ink-900">{boolLabel(values.pumpRequired)}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Reinforcement</p>
        <p class="mt-1 font-medium text-ink-900">{values.reinforcementType ?? 'Pending'}</p>
      </div>
      <div class="rounded-xl bg-ink-50 px-3 py-2">
        <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Finish</p>
        <p class="mt-1 font-medium text-ink-900">{values.finishType ?? 'Pending'}</p>
      </div>
    </div>
  </div>
</aside>
