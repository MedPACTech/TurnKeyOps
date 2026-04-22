<script lang="ts">
  interface QuickReplyOption {
    label: string;
    value: string;
  }

  export let options: QuickReplyOption[] = [];
  export let onSelect: (value: string) => void;
</script>

<div class="flex flex-wrap gap-2">
  {#each options as option}
    <button
      class="min-h-[3rem] rounded-2xl border border-brand-200 bg-white px-4 py-2 text-sm font-medium text-ink-800 shadow-sm transition-colors hover:border-brand-300 hover:bg-brand-50"
      on:click={() => onSelect(option.value)}
    >
      {option.label}
    </button>
  {/each}
</div>
