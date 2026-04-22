<script lang="ts">
  export let loading = false;
  export let hasAppointment = false;
  export let error: string | null = null;
  export let onAppointment: () => void;
  export let onCreateNew: () => void;
  export let onRetry: (() => void) | null = null;
</script>

<section class="card border border-white/70">
  <div class="flex items-start gap-4">
    <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-3xl shadow-sm">
      👷‍♂️
    </div>
    <div class="min-w-0">
      <p class="text-[11px] font-semibold uppercase tracking-[0.26em] text-brand-600">Bob</p>
      <h1 class="mt-2 text-2xl font-bold text-ink-950">New Concrete Estimate</h1>
      <p class="mt-3 text-sm leading-6 text-ink-600">
        Hi, I&apos;m Bob. Are you at your scheduled appointment, or do you need a new estimate?
      </p>
    </div>
  </div>

  {#if error}
    <div class="mt-5 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
      <p>{error}</p>
      {#if onRetry}
        <button class="mt-3 text-sm font-semibold text-red-800 hover:underline" on:click={onRetry}>Try again</button>
      {/if}
    </div>
  {/if}

  <div class="mt-6 space-y-3">
    <button
      class="btn-primary w-full min-h-[3.5rem] justify-between rounded-2xl px-5 text-left text-base"
      disabled={!hasAppointment || loading}
      on:click={onAppointment}
    >
      <span>I&apos;m at my appointment</span>
      <span class="text-xl leading-none">→</span>
    </button>

    {#if !hasAppointment && !loading}
      <p class="px-1 text-sm text-ink-500">
        No scheduled appointment was found right now, but you can still start a new estimate.
      </p>
    {/if}

    <button
      class="btn-secondary w-full min-h-[3.5rem] justify-between rounded-2xl px-5 text-left text-base"
      disabled={loading}
      on:click={onCreateNew}
    >
      <span>Create new estimate</span>
      <span class="text-xl leading-none">＋</span>
    </button>
  </div>
</section>
