<script lang="ts">
  import Sidebar from './Sidebar.svelte';
  import MobileNav from './MobileNav.svelte';
  import ToastContainer from '../ui/ToastContainer.svelte';

  let sidebarOpen = false;
</script>

<div class="flex h-full bg-[radial-gradient(circle_at_top_right,rgba(249,115,22,0.1),transparent_24%),linear-gradient(180deg,#fffdf9_0%,#f6f8fb_100%)]">
  <!-- Desktop sidebar -->
  <div class="hidden lg:flex lg:w-64 lg:flex-col">
    <Sidebar />
  </div>

  <!-- Mobile sidebar -->
  {#if sidebarOpen}
    <div class="fixed inset-0 z-50 lg:hidden">
      <div class="fixed inset-0 bg-gray-900/50" on:click={() => sidebarOpen = false} role="presentation"></div>
      <div class="fixed inset-y-0 left-0 w-64 bg-ink-950 shadow-xl">
        <Sidebar on:close={() => sidebarOpen = false} />
      </div>
    </div>
  {/if}

  <!-- Main content -->
  <div class="flex flex-1 flex-col min-w-0">
    <MobileNav on:menu={() => sidebarOpen = true} />
    <main class="flex-1 overflow-auto p-4 lg:p-6">
      <slot />
    </main>
  </div>
</div>

<ToastContainer />
