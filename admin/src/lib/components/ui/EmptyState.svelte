<script lang="ts">
  export let icon = '📋';
  export let title = 'Nothing here yet';
  export let message = '';
  export let actionLabel = '';
  export let actionHref = '';
</script>

<div class="flex flex-col items-center justify-center py-12 text-center">
  <span class="text-5xl mb-4">{icon}</span>
  <h3 class="text-lg font-semibold text-gray-900 mb-1">{title}</h3>
  {#if message}
    <p class="text-sm text-gray-500 max-w-sm mb-4">{message}</p>
  {/if}
  {#if actionLabel && actionHref}
    <a href={actionHref} class="btn-primary">{actionLabel}</a>
  {/if}
  <slot />
</div>
