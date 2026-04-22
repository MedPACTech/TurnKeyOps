<script lang="ts">
  export let placeholder = 'Type your answer';
  export let hint = 'You can also answer in plain language.';
  export let submitLabel = 'Send answer';
  export let onSubmit: (value: string) => void;

  let value = '';

  function submit() {
    const trimmed = value.trim();
    if (!trimmed) return;

    onSubmit(trimmed);
    value = '';
  }
</script>

<div class="space-y-3 rounded-[1.6rem] border border-brand-100 bg-ink-950 px-4 py-4 text-white shadow-[0_22px_44px_-30px_rgba(15,23,42,0.85)]">
  <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] text-brand-200">
    <span>Bob shortcut</span>
    <span class="h-px flex-1 bg-white/10"></span>
  </div>

  <label class="block">
    <span class="sr-only">Answer Bob in plain language</span>
    <textarea
      class="min-h-[6rem] w-full rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-sm leading-6 text-white placeholder:text-white/45 focus:border-brand-300 focus:outline-none focus:ring-2 focus:ring-brand-300/40"
      bind:value
      rows="3"
      placeholder={placeholder}
    ></textarea>
  </label>

  <p class="text-xs leading-5 text-white/65">{hint}</p>

  <button class="btn-primary w-full min-h-[3.25rem] rounded-2xl text-base" on:click={submit}>
    {submitLabel}
  </button>
</div>
