<script lang="ts">
  import { browser } from '$app/environment';
  import { goto } from '$app/navigation';
  import { isAuthenticated } from '$stores/auth';

  $: if (browser && !$isAuthenticated) {
    goto('/auth/login');
  }
</script>

{#if $isAuthenticated}
  <slot />
{/if}
