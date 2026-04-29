<script lang="ts">
	import type { Snippet } from 'svelte';
	import { bdrAdminNavigation, type BdrAdminNavItem, type BdrAdminRole } from '$lib/config/platform';

	type ShellMetric = { label: string; value: string; detail: string };
	type ShellNote = { title: string; detail: string };
	type ShellAction = { label: string; href: string; variant?: 'primary' | 'secondary' };

	let {
		role,
		activePath,
		children
	} = $props<{
		role: BdrAdminRole;
		activePath: string;
		activeNav: BdrAdminNavItem;
		title: string;
		description: string;
		context: {
			label: string;
			title: string;
			summary: string;
			metrics: ShellMetric[];
		};
		focus: {
			label: string;
			title: string;
			summary: string;
			notes: ShellNote[];
		};
		canvas: {
			label: string;
			title: string;
			summary: string;
			actions: ShellAction[];
		};
		children: Snippet;
	}>();

	let sidebarOpen = $state(false);
	let navCollapsed = $state(false);
	let profileOpen = $state(false);

	const withRole = (href: string) => `${href}?role=${role}`;
	const navItems = $derived(bdrAdminNavigation);

	const navIconFor = (slug: string) =>
		({
			dashboard: '📊',
			calendar: '📅',
			estimates: '📝',
			invoices: '💰',
			customers: '👥',
			requests: '📥',
			content: '🌐',
			settings: '⚙️'
		})[slug] ?? '⚙️';

	const isActive = (item: BdrAdminNavItem) => activePath === item.href;
</script>

<div class="concept-admin-shell min-h-screen bg-[var(--app-bg)] text-[var(--text-base)]">
	<div class="flex min-h-screen">
		<aside class={`hidden shrink-0 flex-col border-r border-[var(--nav-divider)] bg-[var(--nav-bg)] text-white transition-[width] duration-200 lg:flex ${navCollapsed ? 'w-[88px]' : 'w-64'}`}>
			<div class="flex h-16 items-center gap-3 border-b border-[var(--nav-divider)] px-4">
				<div class={`flex shrink-0 items-center justify-center rounded-md bg-white p-1.5 shadow-sm ${navCollapsed ? 'h-10 w-10' : 'h-12 w-32'}`}>
					<img src="/turnkeyops-logo.png" alt="TurnKeyOps" class="h-full w-full object-contain" />
				</div>
				<button
					type="button"
					class="ml-auto inline-flex h-9 w-9 items-center justify-center rounded-md text-white/60 transition hover:bg-white/10 hover:text-white"
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
						href={withRole(item.href)}
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
						ER
					</button>
					{#if !navCollapsed}
						<div class="min-w-0 flex-1">
							<p class="truncate text-sm font-medium">Ella Robinson</p>
							<p class="truncate text-xs text-white/50">Admin</p>
						</div>
						<a
							href="/"
							class="inline-flex h-9 w-9 items-center justify-center rounded-lg text-white/50 transition hover:bg-white/10 hover:text-white"
							title="Sign out"
							aria-label="Sign out"
						>
							<span aria-hidden="true">↪</span>
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
					<div class="flex h-16 items-center gap-3 border-b border-[var(--nav-divider)] px-4">
						<div class="flex h-12 w-32 items-center justify-center rounded-md bg-white p-1.5 shadow-sm">
							<img src="/turnkeyops-logo.png" alt="TurnKeyOps" class="h-full w-full object-contain" />
						</div>
						<button
							type="button"
							class="ml-auto inline-flex h-9 w-9 items-center justify-center rounded-md text-white/70 hover:bg-white/10 hover:text-white"
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
								href={withRole(item.href)}
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
				</aside>
			</div>
		{/if}

		<div class="flex min-w-0 flex-1 flex-col">
			<main class="admin-workarea min-w-0 flex-1 overflow-auto px-4 py-5 lg:px-6 lg:py-7">
				<button
					type="button"
					class="mb-4 inline-flex h-10 w-10 items-center justify-center rounded-md bg-white/85 text-xl text-[var(--text-base)] shadow-sm lg:hidden"
					aria-label="Open navigation"
					onclick={() => (sidebarOpen = true)}
				>
					<span aria-hidden="true">☰</span>
				</button>
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
					<div class="flex h-12 w-12 items-center justify-center rounded-full bg-[var(--brand-solid)] text-sm font-semibold text-white">ER</div>
					<div>
						<p class="text-base font-semibold text-[var(--text-strong)]">Ella Robinson</p>
						<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">TurnKeyOps admin operator</p>
					</div>
				</div>
			</section>

			<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4 shadow-sm">
				<p class="text-sm font-semibold text-[var(--text-muted)]">Workspace</p>
				<div class="mt-3 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
					<p class="text-base font-semibold text-[var(--text-strong)]">BDR Admin</p>
					<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">Calm contractor office workspace for daily estimating, scheduling, and follow-through.</p>
				</div>
			</section>
		</div>
	</aside>
{/if}

<style>
	.concept-admin-shell {
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
			radial-gradient(circle at 0% 0%, rgba(255, 247, 237, 0.98) 0, rgba(255, 247, 237, 0.52) 21rem, rgba(255, 247, 237, 0) 42rem),
			radial-gradient(circle at 100% 0%, rgba(219, 234, 254, 0.98) 0, rgba(219, 234, 254, 0.58) 22rem, rgba(219, 234, 254, 0) 44rem),
			radial-gradient(circle at 78% 72%, rgba(245, 243, 255, 0.9) 0, rgba(245, 243, 255, 0.48) 20rem, rgba(245, 243, 255, 0) 40rem),
			linear-gradient(135deg, rgba(255, 252, 247, 0.98) 0%, rgba(239, 246, 255, 0.92) 42%, rgba(248, 247, 255, 0.96) 100%);
	}
</style>
