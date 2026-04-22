<script lang="ts">
  export let open = false;
  export let title = '';
  export let size: 'sm' | 'md' | 'lg' = 'md';

  const sizes = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl' };
</script>

{#if open}
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="fixed inset-0 bg-gray-900/50" on:click={() => open = false} role="presentation" />
    <div class="relative bg-white rounded-xl shadow-2xl w-full {sizes[size]} max-h-[85vh] overflow-auto">
      <div class="flex items-center justify-between px-5 py-4 border-b">
        <h2 class="text-lg font-semibold">{title}</h2>
        <button class="btn-icon" on:click={() => open = false}>✕</button>
      </div>
      <div class="p-5">
        <slot />
      </div>
    </div>
  </div>
{/if}
