<script lang="ts">
	import { onMount, type Snippet } from 'svelte';
	import { Menu, PanelLeftClose, PanelLeftOpen, LogOut, Sun, Moon, Monitor, Sparkles } from 'lucide-svelte';
	import AdminIcon from './AdminIcon.svelte';
	import AdminDrawer from './AdminDrawer.svelte';
	import '$lib/styles/admin-theme.css';
	import { invalidateAll } from '$app/navigation';
	import type { BdrAdminNavItem, BdrAdminRole } from '$lib/config/platform';
	import type { ExternalAdminTheme } from '$lib/config/external-admin';
	import {
		bobVoiceCookie,
		bobVoiceOptions,
		normalizeBobVoice,
		type BobVoiceId
	} from '$lib/bob-voice';

	let {
		role,
		activePath,
		activeNav,
		initialBobVoice,
		navItems,
		tenantName,
		workspaceLabel,
		workspaceSummary,
		homeHref,
		publicHref,
		operatorEmail,
		theme,
		children
	} = $props<{
		role: BdrAdminRole;
		activePath: string;
		activeNav: BdrAdminNavItem;
		initialBobVoice: BobVoiceId;
		navItems: BdrAdminNavItem[];
		tenantName: string;
		workspaceLabel: string;
		workspaceSummary: string;
		homeHref: string;
		publicHref: string;
		operatorEmail: string;
		theme: ExternalAdminTheme;
		children: Snippet;
	}>();

	let sidebarOpen = $state(false);
	let navCollapsed = $state(false);
	let profileOpen = $state(false);
	let bobVoice = $derived(initialBobVoice);

	const isBobWorkspace = $derived(activeNav.slug === 'bob');
	const operatorName = $derived(operatorEmail || 'Workspace operator');
	const operatorInitials = $derived(
		operatorEmail
			? operatorEmail
					.split('@')[0]
					.split(/[._-]/)
					.filter(Boolean)
					.slice(0, 2)
					.map((part: string) => part[0]?.toUpperCase())
					.join('')
			: 'OP'
	);

	const isActive = (item: BdrAdminNavItem) => activeNav.slug === item.slug || activePath === item.href;
	const selectedBobVoice = $derived(
		bobVoiceOptions.find((option) => option.id === bobVoice) ?? bobVoiceOptions[0]
	);

	async function updateBobVoice(event: Event) {
		bobVoice = normalizeBobVoice((event.currentTarget as HTMLSelectElement).value);
		document.cookie = `${bobVoiceCookie}=${encodeURIComponent(bobVoice)}; Path=/; Max-Age=31536000; SameSite=Lax`;
		await invalidateAll();
	}

  type ThemeChoice = 'system' | 'light' | 'dark';
  let themeChoice = $state<ThemeChoice>('system');
  let systemDark = $state(false);
  const resolvedTheme = $derived(themeChoice === 'system' ? (systemDark ? 'dark' : 'light') : themeChoice);
  onMount(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    systemDark = media.matches;
    try { const saved = localStorage.getItem('tko-admin-theme'); if (saved === 'light' || saved === 'dark') themeChoice = saved; } catch { /* Storage can be unavailable in private contexts. */ }
    const update = () => { systemDark = media.matches; };
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  });
  function changeTheme(choice: ThemeChoice) {
    themeChoice = choice;
    try { if (choice === 'system') localStorage.removeItem('tko-admin-theme'); else localStorage.setItem('tko-admin-theme', choice); } catch { /* The theme still works for this visit. */ }
  }
</script>

{#snippet wordmark(collapsed = false)}
  <a href={homeHref} aria-label={`${workspaceLabel} home`} class="inline-flex min-h-11 items-center gap-2 font-semibold tracking-tight text-[var(--text-strong)]">
    <Sparkles size={22} class="shrink-0 text-[var(--teal-text)]" aria-hidden="true" />
    {#if !collapsed}<span>TurnKeyOps<span class="text-[var(--teal-text)]">.AI</span></span>{/if}
  </a>
{/snippet}
{#snippet navigation(collapsed = false, mobile = false)}
  <nav aria-label={mobile ? 'Mobile navigation' : 'Primary navigation'} class="min-h-0 flex-1 space-y-1 overflow-y-auto px-3 py-4">
    {#each navItems as item}
      <a href={item.href} title={item.label} aria-label={collapsed ? item.label : undefined} aria-current={isActive(item) ? 'page' : undefined}
        class={`flex min-h-11 items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium transition-colors ${collapsed ? 'justify-center' : ''} ${isActive(item) ? 'bg-[var(--nav-active-bg)] text-[var(--nav-active-text)]' : 'text-[var(--nav-text)] hover:bg-[var(--nav-hover)]'}`}
        onclick={() => { sidebarOpen = false; }}>
        <AdminIcon name={item.slug} />{#if !collapsed}<span>{item.label}</span>{/if}
      </a>
    {/each}
  </nav>
{/snippet}
{#snippet themeControls(collapsed = false)}
  <button type="button" class="btn-ghost w-full" aria-label={`Switch to ${resolvedTheme === 'dark' ? 'light' : 'dark'} mode`} onclick={() => changeTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}>
    {#if resolvedTheme === 'dark'}<Sun size={18} aria-hidden="true" />{:else}<Moon size={18} aria-hidden="true" />{/if}
    {#if !collapsed}<span>{resolvedTheme === 'dark' ? 'Light mode' : 'Dark mode'}</span>{/if}
  </button>
{/snippet}
<div class="admin-theme concept-admin-shell h-dvh overflow-hidden" data-theme={resolvedTheme} style={`--tenant-identity:${theme.accent}`}>
  <a class="skip-link" href="#admin-content">Skip to content</a>
  <div class="flex h-full min-h-0">
    <aside class={`hidden shrink-0 flex-col border-r border-[var(--nav-divider)] bg-[var(--nav-bg)] transition-[width] duration-200 lg:flex ${navCollapsed ? 'w-[88px]' : 'w-64'}`}>
      <div class="border-b border-[var(--nav-divider)] px-5 py-4">
        {@render wordmark(navCollapsed)}
        {#if !navCollapsed}<p class="mt-1 truncate text-xs text-[var(--text-muted)]">{tenantName}</p>{/if}
      </div>
      {@render navigation(navCollapsed)}
      <div class="space-y-2 border-t border-[var(--nav-divider)] p-3">
        {@render themeControls(navCollapsed)}
        <button type="button" class="btn-ghost w-full" aria-label={navCollapsed ? 'Expand navigation' : 'Collapse navigation'} aria-expanded={!navCollapsed} onclick={() => (navCollapsed = !navCollapsed)}>
          {#if navCollapsed}<PanelLeftOpen size={18} aria-hidden="true" />{:else}<PanelLeftClose size={18} aria-hidden="true" /><span>Collapse navigation</span>{/if}
        </button>
        <div class={`flex items-center gap-2 ${navCollapsed ? 'flex-col' : ''}`}>
          <button type="button" class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[var(--teal-soft)] text-sm font-semibold text-[var(--teal-text)]" aria-label="Open profile" onclick={() => (profileOpen = true)}>{operatorInitials}</button>
          {#if !navCollapsed}<div class="min-w-0 flex-1"><p class="truncate text-sm font-medium">{operatorName}</p><p class="truncate text-xs text-[var(--text-muted)]">{workspaceLabel}</p></div>{/if}
          <form method="POST" action="/auth/logout"><input type="hidden" name="returnTo" value={activePath} /><button type="submit" class="btn-icon" aria-label="Sign out" title="Sign out"><LogOut size={18} aria-hidden="true" /></button></form>
        </div>
      </div>
    </aside>
    <div class="flex min-h-0 min-w-0 flex-1 flex-col">
      <header class="flex h-16 shrink-0 items-center justify-between border-b border-[var(--border)] bg-[var(--surface)] px-4 lg:hidden">
        {@render wordmark()}
        <button type="button" class="btn-icon" aria-label="Open navigation" aria-expanded={sidebarOpen} onclick={() => (sidebarOpen = true)}><Menu size={22} aria-hidden="true" /></button>
      </header>
      <main id="admin-content" tabindex="-1" class={`admin-workarea min-h-0 min-w-0 flex-1 ${isBobWorkspace ? 'overflow-hidden p-0' : 'overflow-auto px-4 py-5 lg:px-8 lg:py-8'}`}>
        {@render children()}
      </main>
    </div>
  </div>
  {#if sidebarOpen}
    <AdminDrawer title="Navigation" side="left" width="20rem" close={() => (sidebarOpen = false)}>
      <p class="px-3 text-sm text-[var(--text-muted)]">{tenantName}</p>
      {@render navigation(false, true)}
      {@render themeControls()}
      <button type="button" class="btn-secondary mt-3 w-full" onclick={() => { sidebarOpen = false; profileOpen = true; }}>Profile & Bob voice</button>
    </AdminDrawer>
  {/if}
  {#if profileOpen}
    <AdminDrawer title="Operator settings" width="26rem" close={() => (profileOpen = false)}>
      <div class="space-y-6">
        <section><h3 class="break-words font-semibold text-[var(--text-strong)]">{operatorName}</h3><p class="mt-1 text-sm text-[var(--text-muted)]">{workspaceLabel} · {role}</p></section>
        <section class="border-t border-[var(--border)] pt-5">
          <label for="admin-theme-choice" class="label flex items-center gap-2"><Monitor size={16} aria-hidden="true" />Appearance</label>
          <select id="admin-theme-choice" class="input" value={themeChoice} onchange={(event) => changeTheme(event.currentTarget.value as ThemeChoice)}><option value="system">Use system setting</option><option value="light">Light</option><option value="dark">Dark</option></select>
        </section>
        <section class="border-t border-[var(--border)] pt-5">
          <label for="bob-voice" class="label">Bob voice</label>
          <p id="bob-voice-description" class="mb-3 text-sm text-[var(--text-muted)]">Changes how Bob talks to you, not what he can do.</p>
          <select id="bob-voice" class="input" aria-describedby="bob-voice-description" value={bobVoice} onchange={updateBobVoice}>{#each bobVoiceOptions as option}<option value={option.id}>{option.label}</option>{/each}</select>
          <p class="mt-3 text-sm leading-6">{selectedBobVoice.description}</p>
          <blockquote class="mt-3 border-l-2 border-[var(--teal-border)] pl-3 text-sm italic leading-6 text-[var(--text-muted)]">“{selectedBobVoice.preview}”</blockquote>
        </section>
        <section class="border-t border-[var(--border)] pt-5"><h3 class="font-semibold text-[var(--text-strong)]">Workspace</h3><p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{workspaceSummary}</p><a href={publicHref} class="mt-3 inline-flex min-h-11 items-center text-sm font-semibold text-[var(--teal-text)] hover:underline">View public site</a></section>
        <form method="POST" action="/auth/logout"><input type="hidden" name="returnTo" value={activePath} /><button type="submit" class="btn-secondary w-full"><LogOut size={18} aria-hidden="true" />Sign out</button></form>
      </div>
    </AdminDrawer>
  {/if}
</div>
<style>
  .skip-link { position:fixed; left:1rem; top:-5rem; z-index:100; padding:.75rem 1rem; background:var(--surface); color:var(--teal-text); border:2px solid var(--focus-ring); border-radius:6px; }
  .skip-link:focus { top:1rem; }
</style>
