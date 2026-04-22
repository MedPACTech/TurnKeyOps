<script lang="ts">
  import { browser } from '$app/environment';
  import { goto } from '$app/navigation';
  import { auth, isAuthenticated } from '$stores/auth';
  import { AppShell } from '$components';

  // Redirect to login if not authenticated
  $: if (browser && !$isAuthenticated) {
    goto('/auth/login');
  }
</script>

{#if $isAuthenticated}
  <AppShell>
    <slot />
  </AppShell>
{/if}
