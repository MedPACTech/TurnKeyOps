<script lang="ts">
  import { page } from '$app/stores';
  import { createEventDispatcher } from 'svelte';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';

  const dispatch = createEventDispatcher();
  $: title = getTitle($page.url.pathname);

  function getTitle(path: string): string {
    if (path === '/app') return 'Dashboard';
    const segment = path.split('/').pop() ?? '';
    return segment
      .split('-')
      .filter(Boolean)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }
</script>

<header class="lg:hidden flex items-center gap-3 px-4 py-3 bg-white/90 border-b border-white/70 shadow-sm backdrop-blur-sm">
  <button class="btn-icon bg-brand-50 text-brand-700 hover:bg-brand-100" on:click={() => dispatch('menu')} aria-label="Open navigation menu">
    <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"/>
    </svg>
  </button>
  <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
    <BrandLogo compact className="max-w-[8.5rem]" />
    <h1 class="truncate text-sm font-semibold uppercase tracking-[0.18em] text-ink-500">{title}</h1>
  </div>
</header>
