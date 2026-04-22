<script lang="ts">
  export let value: string | number;
  export let label: string;
  export let icon = '';
  export let trend: 'up' | 'down' | 'neutral' = 'neutral';
  export let href = '';
</script>

<svelte:element this={href ? 'a' : 'div'} {href}
  class="stat-card hover:shadow-md transition-shadow {href ? 'cursor-pointer' : ''}">
  <div class="flex items-center justify-between mb-2">
    <span class="text-2xl">{icon}</span>
    {#if trend === 'up'}
      <span class="text-xs text-green-600 font-medium">▲</span>
    {:else if trend === 'down'}
      <span class="text-xs text-red-600 font-medium">▼</span>
    {/if}
  </div>
  <p class="stat-value">{value}</p>
  <p class="stat-label">{label}</p>
</svelte:element>
