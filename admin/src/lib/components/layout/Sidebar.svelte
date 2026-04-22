<script lang="ts">
  import { page } from '$app/stores';
  import { auth, currentUser } from '$stores/auth';
  import { createEventDispatcher } from 'svelte';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';

  const dispatch = createEventDispatcher();

  const nav = [
    { href: '/app', label: 'Dashboard', icon: '📊' },
    { href: '/app/calendar', label: 'Calendar', icon: '📅' },
    { href: '/app/customers', label: 'Customers', icon: '👥' },
    { href: '/app/jobs', label: 'Jobs', icon: '🏗️' },
    { href: '/app/estimates', label: 'Estimates', icon: '📝' },
    { href: '/app/estimate-defaults', label: 'Defaults', icon: '⚙️' },
    { href: '/app/invoices', label: 'Invoices', icon: '💰' },
    { href: '/app/chat', label: 'Ask Bob', icon: '👷‍♂️' },
  ];

  $: currentPath = $page.url.pathname;
  $: isActive = (href: string) => currentPath === href || (href !== '/app' && currentPath.startsWith(`${href}/`));
</script>

<nav class="flex flex-col h-full bg-ink-950 text-white">
  <!-- Logo -->
  <div class="px-5 py-5 border-b border-white/10">
    <div class="flex items-start gap-3">
      <BrandLogo dark className="w-full" />
      <button class="ml-auto lg:hidden text-white/70 hover:text-white" on:click={() => dispatch('close')}>✕</button>
    </div>
  </div>

  <!-- Nav links -->
  <div class="flex-1 py-5 space-y-1.5 px-3 overflow-y-auto">
    {#each nav as item}
      <a
        href={item.href}
        class="flex items-center gap-3 px-3 py-3 rounded-xl text-sm font-medium transition-colors
               {isActive(item.href)
                 ? 'bg-white text-ink-950 shadow-[0_20px_35px_-30px_rgba(255,255,255,0.9)]'
                 : 'text-white/72 hover:bg-white/10 hover:text-white'}"
        on:click={() => dispatch('close')}
      >
        <span
          class="flex h-8 w-8 items-center justify-center rounded-lg text-lg
            {isActive(item.href) ? 'bg-brand-500/18' : 'bg-white/5'}"
        >
          {item.icon}
        </span>
        {item.label}
      </a>
    {/each}
  </div>

  <!-- User footer -->
  <div class="border-t border-white/10 p-4">
    <div class="flex items-center gap-3">
      <div class="w-9 h-9 rounded-full bg-brand-500 flex items-center justify-center text-xs font-bold text-white">
        {($currentUser?.firstName?.[0] ?? '') + ($currentUser?.lastName?.[0] ?? '')}
      </div>
      <div class="flex-1 min-w-0">
        <p class="text-sm font-medium truncate">{$currentUser?.firstName} {$currentUser?.lastName}</p>
        <p class="text-xs text-white/50 truncate">{$currentUser?.email}</p>
      </div>
      <button class="btn-icon text-white/50 hover:bg-white/10 hover:text-white" on:click={() => auth.logout()} title="Sign out">
        🚪
      </button>
    </div>
  </div>
</nav>
