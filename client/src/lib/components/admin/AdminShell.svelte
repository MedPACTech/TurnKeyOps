<script lang="ts">
	import type { Snippet } from 'svelte';
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

	const navIconFor = (slug: string) =>
		({
			dashboard: '📊',
			calendar: '📅',
			jobs: '🏗️',
			estimates: '📝',
			invoices: '💰',
			customers: '👥',
			requests: '📥',
			content: '🌐',
			bob: '👷',
			settings: '⚙️'
		})[slug] ?? '⚙️';

	const isActive = (item: BdrAdminNavItem) => activeNav.slug === item.slug || activePath === item.href;
	const selectedBobVoice = $derived(
		bobVoiceOptions.find((option) => option.id === bobVoice) ?? bobVoiceOptions[0]
	);

	async function updateBobVoice(event: Event) {
		bobVoice = normalizeBobVoice((event.currentTarget as HTMLSelectElement).value);
		document.cookie = `${bobVoiceCookie}=${encodeURIComponent(bobVoice)}; Path=/; Max-Age=31536000; SameSite=Lax`;
		await invalidateAll();
	}
</script>

<div
	class="concept-admin-shell h-screen overflow-hidden bg-[var(--app-bg)] text-[var(--text-base)]"
	style={`--brand-solid:${theme.accent};--brand-solid-hover:${theme.accentHover};--accent-soft:${theme.accentSoft};--accent-border:${theme.accentBorder};--accent-text:${theme.accentText};--accent-solid:${theme.accent};--accent-solid-hover:${theme.accentHover};`}
>
	<div class="flex h-full min-h-0">
		<aside class={`hidden h-screen shrink-0 flex-col overflow-hidden border-r border-[var(--nav-divider)] bg-[var(--nav-bg)] text-white transition-[width] duration-200 lg:flex ${navCollapsed ? 'w-[88px]' : 'w-64'}`}>
			<div class={`relative border-b border-[var(--nav-divider)] ${navCollapsed ? 'px-3 py-5' : 'px-4 py-5'}`}>
				<div class={`flex w-full flex-col gap-4 bg-white shadow-sm ${navCollapsed ? 'p-1.5' : 'px-4 py-3'}`}>
					<img
						src="/turnkeyops-logo.png"
						alt="TurnKeyOps"
						class={`h-auto w-full object-contain ${navCollapsed ? 'max-w-[3.75rem]' : 'max-w-[14rem]'}`}
					/>
					{#if !navCollapsed}
						<p class="border-t border-slate-200 pt-2 text-center text-[0.65rem] font-bold uppercase tracking-[0.16em] text-slate-500">
							{tenantName}
						</p>
					{/if}
				</div>
				<button
					type="button"
					class="absolute right-2 top-2 inline-flex h-7 w-7 items-center justify-center rounded-full bg-slate-950/40 text-white/75 shadow-sm ring-1 ring-white/20 backdrop-blur transition hover:bg-slate-950/55 hover:text-white focus:outline-none focus:ring-white/30"
					title={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}
					aria-label={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}
					onclick={() => (navCollapsed = !navCollapsed)}
				>
					{#if navCollapsed}
						<span class="text-xl leading-none" aria-hidden="true">›</span>
					{:else}
						<span class="text-xl leading-none" aria-hidden="true">‹</span>
					{/if}
				</button>
			</div>

			<nav class="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label="Primary navigation">
				<div class="space-y-1.5">
				{#each navItems as item}
					{@const icon = navIconFor(item.slug)}
					<a
						href={item.href}
						title={item.label}
						class={`group flex min-h-12 items-center ${navCollapsed ? 'justify-center px-2' : 'gap-3 px-3'} rounded-md py-2.5 text-sm font-medium leading-5 transition-colors ${
							isActive(item)
								? 'bg-[var(--nav-active-bg)] text-[var(--nav-active-text)] shadow-sm'
								: 'text-white/70 hover:bg-white/10 hover:text-white'
						}`}
					>
						<span
							class={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-lg ${
								isActive(item) ? 'bg-[var(--accent-soft)]' : 'bg-white/5'
							}`}
							aria-hidden="true"
						>
							{icon}
						</span>
						{#if !navCollapsed}
							<span class="truncate">{item.label}</span>
						{/if}
					</a>
				{/each}
				</div>
			</nav>

			<div class="border-t border-[var(--nav-divider)] p-4">
				<div class={`flex gap-3 ${navCollapsed ? 'flex-col items-center' : 'items-center'}`}>
					<button
						type="button"
						class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--brand-solid)] text-xs font-bold text-white"
						aria-label="Open profile"
						title="Profile"
						onclick={() => (profileOpen = true)}
					>
						{operatorInitials}
					</button>
					{#if !navCollapsed}
						<div class="min-w-0 flex-1">
							<p class="truncate text-sm font-medium">{operatorName}</p>
							<p class="truncate text-xs text-white/50">{workspaceLabel}</p>
						</div>
						<a
							href="/"
							class="inline-flex h-9 w-9 items-center justify-center rounded-lg text-white/50 transition hover:bg-white/10 hover:text-white"
							title="Sign out"
							aria-label="Sign out"
						>
							<span aria-hidden="true">🚪</span>
						</a>
					{/if}
				</div>
			</div>
		</aside>

		{#if sidebarOpen}
			<div class="fixed inset-0 z-50 lg:hidden">
				<button
					type="button"
					class="fixed inset-0 bg-gray-900/50"
					aria-label="Close navigation"
					onclick={() => (sidebarOpen = false)}
				></button>
				<aside class="fixed inset-y-0 left-0 flex w-64 flex-col bg-[var(--nav-bg)] text-white shadow-[var(--shell-shadow)]">
					<div class="relative border-b border-[var(--nav-divider)] px-4 py-5">
						<div class="flex w-full flex-col gap-4 bg-white px-4 py-3 shadow-sm">
							<img
								src="/turnkeyops-logo.png"
								alt="TurnKeyOps"
								class="h-auto w-full max-w-[14rem] object-contain"
							/>
						</div>
						<button
							type="button"
							class="absolute right-2 top-2 inline-flex h-7 w-7 items-center justify-center rounded-full bg-slate-950/40 text-white/75 shadow-sm ring-1 ring-white/20 backdrop-blur transition hover:bg-slate-950/55 hover:text-white focus:outline-none focus:ring-white/30"
							aria-label="Close navigation"
							onclick={() => (sidebarOpen = false)}
						>
							<span aria-hidden="true">✕</span>
						</button>
					</div>

					<nav class="flex-1 overflow-y-auto px-3 py-4" aria-label="Mobile navigation">
						<div class="space-y-1.5">
						{#each navItems as item}
							{@const icon = navIconFor(item.slug)}
							<a
								href={item.href}
								class={`flex min-h-12 items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium leading-5 transition-colors ${
									isActive(item)
										? 'bg-[var(--nav-active-bg)] text-[var(--nav-active-text)] shadow-sm'
										: 'text-white/70 hover:bg-white/10 hover:text-white'
								}`}
								onclick={() => (sidebarOpen = false)}
							>
								<span
									class={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-lg ${
										isActive(item) ? 'bg-[var(--accent-soft)]' : 'bg-white/5'
									}`}
									aria-hidden="true"
								>
									{icon}
								</span>
								<span>{item.label}</span>
							</a>
						{/each}
						</div>
					</nav>
					<div class="border-t border-[var(--nav-divider)] p-3">
						<button
							type="button"
							class="flex min-h-11 w-full items-center gap-3 rounded-md px-3 text-sm font-medium text-white/75 hover:bg-white/10 hover:text-white"
							onclick={() => {
								sidebarOpen = false;
								profileOpen = true;
							}}
						>
							<span class="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--brand-solid)] text-xs font-bold text-white">{operatorInitials}</span>
							Profile & Bob voice
						</button>
					</div>
				</aside>
			</div>
		{/if}

		<div class="flex min-h-0 min-w-0 flex-1 flex-col">
			<header class="flex h-16 shrink-0 items-center justify-between border-b border-[var(--shell-border)] bg-white px-4 lg:hidden">
				<a href={homeHref} class="inline-flex min-w-0 items-center" aria-label={`${workspaceLabel} home`}>
					<img
						src="/turnkeyops-logo.png"
						alt="TurnKeyOps"
						class="h-10 w-auto max-w-[12rem] object-contain object-left"
					/>
				</a>
				<button
					type="button"
					class="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-2xl leading-none text-[var(--text-base)] transition hover:bg-[var(--shell-panel-strong)]"
					aria-label="Open navigation"
					onclick={() => (sidebarOpen = true)}
				>
					<span aria-hidden="true">☰</span>
				</button>
			</header>
			<main
				class={`admin-workarea min-h-0 min-w-0 flex-1 ${
					isBobWorkspace
						? 'overflow-hidden p-0'
						: 'overflow-auto px-4 py-5 lg:px-6 lg:py-7'
				}`}
			>
				{@render children()}
			</main>
		</div>
	</div>
</div>

{#if profileOpen}
	<button
		class="fixed inset-0 z-50 bg-slate-900/30"
		type="button"
		aria-label="Close profile view"
		onclick={() => (profileOpen = false)}
	></button>
	<aside class="fixed inset-y-0 right-0 z-[60] flex w-full max-w-sm flex-col border-l border-[var(--shell-border)] bg-white shadow-xl">
		<div class="flex items-center justify-between border-b border-[var(--shell-border)] px-5 py-4">
			<div>
				<p class="text-sm font-semibold text-[var(--text-muted)]">Profile</p>
				<h2 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">Operator settings</h2>
			</div>
			<button
				type="button"
				class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm font-medium text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]"
				onclick={() => (profileOpen = false)}
			>
				Close
			</button>
		</div>

		<div class="flex-1 space-y-4 overflow-y-auto px-5 py-5">
			<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4 shadow-sm">
				<div class="flex items-center gap-3">
					<div class="flex h-12 w-12 items-center justify-center rounded-full bg-[var(--brand-solid)] text-sm font-semibold text-white">{operatorInitials}</div>
					<div>
						<p class="text-base font-semibold text-[var(--text-strong)]">{operatorName}</p>
						<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{workspaceLabel}</p>
					</div>
				</div>
			</section>

			<section class="border-y border-[var(--shell-border)] py-4">
				<label for="bob-voice" class="text-sm font-semibold text-[var(--text-strong)]">Bob voice</label>
				<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">
					Changes how Bob talks to you, not what he can do.
				</p>
				<select
					id="bob-voice"
					value={bobVoice}
					onchange={updateBobVoice}
					class="mt-3 min-h-11 w-full rounded-md border border-[var(--shell-border-strong)] bg-white px-3 text-sm font-semibold text-[var(--text-strong)] outline-none focus:border-[var(--accent-solid)] focus:ring-2 focus:ring-[var(--focus-ring)]"
				>
					{#each bobVoiceOptions as option}
						<option value={option.id}>{option.label}</option>
					{/each}
				</select>
				<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">{selectedBobVoice.description}</p>
				<blockquote class="mt-3 border-l-2 border-[var(--accent-border)] pl-3 text-sm italic leading-6 text-[var(--text-muted)]">
					“{selectedBobVoice.preview}”
				</blockquote>
			</section>

			<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4 shadow-sm">
				<p class="text-sm font-semibold text-[var(--text-muted)]">Workspace</p>
				<div class="mt-3 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
					<p class="text-base font-semibold text-[var(--text-strong)]">{workspaceLabel}</p>
					<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{workspaceSummary}</p>
				</div>
				<a href={publicHref} class="mt-3 inline-flex text-sm font-semibold text-[var(--accent-text)] hover:underline">
					View public site
				</a>
			</section>
		</div>
	</aside>
{/if}

<style>
	.concept-admin-shell {
		/* Shared External Admin shell tokens for all /bdr/admin/* routes. Reuse
		   admin-workarea for the SPA canvas and the module/shell panel tokens
		   below for route-level cards, rails, drawers, CTA states, and AI accents. */
		--app-bg: #f6f7f9;
		--nav-bg: #1f2933;
		--nav-divider: rgba(255, 255, 255, 0.08);
		--nav-active-bg: #ffffff;
		--nav-active-text: #111827;
		--brand-solid: #f97316;
		--brand-solid-hover: #ea580c;
		--focus-ring: #fdba74;
		--shell-shadow: 0 1px 2px rgba(15, 23, 42, 0.08), 0 12px 28px rgba(15, 23, 42, 0.04);
		--shell-border: #e5e7eb;
		--shell-border-strong: #d1d5db;
		--text-strong: #111827;
		--text-base: #374151;
		--text-muted: #6b7280;
		--muted: #9ca3af;
		--accent-soft: #fff7ed;
		--accent-border: #fed7aa;
		--accent-text: #c2410c;
		--accent-solid: #f97316;
		--accent-solid-hover: #ea580c;
		--accent-solid-text: #ffffff;
		--topbar-bg: rgba(255, 255, 255, 0.95);
		--rail-bg: #ffffff;
		--pane-bg: #ffffff;
		--canvas-bg: #f6f7f9;
		--module-bg: #ffffff;
		--shell-card: #ffffff;
		--shell-panel: #ffffff;
		--shell-panel-strong: #f8fafc;
		--drawer-bg: #ffffff;
		--drawer-card-bg: #ffffff;
	}

	.admin-workarea {
		background:
			radial-gradient(circle at top right, rgba(249, 115, 22, 0.1), transparent 24%),
			linear-gradient(180deg, #fffdf9 0%, #f6f8fb 100%);
	}
</style>
