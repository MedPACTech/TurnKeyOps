<script lang="ts">
  export let title = '';
  export let subtitle = '';
  export let icon = '🧾';
  export let emptyMessage = 'No details captured yet.';
</script>

<section class="card border border-brand-100/80 p-4">
  <div class="flex items-start gap-3">
    <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-2xl">
      {icon}
    </div>
    <div class="min-w-0">
      <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">{title}</p>
      {#if subtitle}
        <p class="mt-1 text-sm text-ink-500">{subtitle}</p>
      {/if}
    </div>
  </div>

  <div class="mt-4">
    <slot>
      <p class="text-sm text-ink-500">{emptyMessage}</p>
    </slot>
  </div>
</section>
