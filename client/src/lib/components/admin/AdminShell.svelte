<script lang="ts">
	import {
		CalendarDays,
		FileText,
		Globe,
		Building2,
		LayoutDashboard,
		LogOut,
		Menu,
		PanelLeftClose,
		PanelLeftOpen,
		Receipt,
		Settings2,
		SquareUserRound,
		ClipboardList,
		X
	} from 'lucide-svelte';
	import type { Snippet } from 'svelte';
	import {
		bdrAdminNavigation,
		bdrAdminNavSections,
		type BdrAdminNavItem,
		type BdrAdminRole
	} from '$lib/config/platform';

	type ShellMetric = { label: string; value: string; detail: string };
	type ShellNote = { title: string; detail: string };
	type ShellAction = { label: string; href: string; variant?: 'primary' | 'secondary' };

	let {
		role,
		activePath,
		activeNav,
		title,
		description,
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
	const groupedNavItems = $derived(
		bdrAdminNavSections
			.map((section) => ({
				...section,
				items: navItems.filter((item) => item.section === section.key)
			}))
			.filter((section) => section.items.length)
	);

	const navIconFor = (slug: string) =>
		({
			dashboard: LayoutDashboard,
			calendar: CalendarDays,
			estimates: FileText,
			invoices: Receipt,
			customers: SquareUserRound,
			requests: ClipboardList,
			content: Globe,
			settings: Settings2
		})[slug] ?? Settings2;

	const isActive = (item: BdrAdminNavItem) => activePath === item.href;
	const roleLabel = $derived(
		role === 'owner' ? 'Owner' : role === 'office-admin' ? 'Office Admin' : 'Estimator Crew Lite'
	);
</script>

<div class="concept-admin-shell min-h-screen bg-[var(--app-bg)] text-[var(--text-base)]">
	<div class="flex min-h-screen">
		<aside class={`hidden shrink-0 flex-col border-r border-[var(--nav-divider)] bg-[var(--nav-bg)] text-white transition-[width] duration-200 lg:flex ${navCollapsed ? 'w-[88px]' : 'w-64'}`}>
			<div class="flex h-16 items-center gap-3 border-b border-[var(--nav-divider)] px-4">
				<div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-[var(--brand-solid)] text-white shadow-sm">
					<Building2 class="h-5 w-5" aria-hidden="true" />
				</div>
				{#if !navCollapsed}
					<div class="min-w-0">
						<p class="truncate text-base font-semibold leading-5">TurnKeyOps</p>
						<p class="truncate text-xs text-white/55">Contractor workspace</p>
					</div>
				{/if}
				<button
					type="button"
					class="ml-auto inline-flex h-9 w-9 items-center justify-center rounded-md text-white/60 transition hover:bg-white/10 hover:text-white"
					title={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}
					aria-label={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}
					onclick={() => (navCollapsed = !navCollapsed)}
				>
					{#if navCollapsed}
						<PanelLeftOpen class="h-5 w-5" aria-hidden="true" />
					{:else}
						<PanelLeftClose class="h-5 w-5" aria-hidden="true" />
					{/if}
				</button>
			</div>

			<nav class="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label="Primary navigation">
				{#each groupedNavItems as section, sectionIndex}
					{#if !navCollapsed}
						<div class={sectionIndex === 0 ? '' : 'mt-5'}>
							<div class="mb-2 px-3">
								<p class="text-[0.68rem] font-semibold text-white/45">{section.label}</p>
							</div>
							<div class="space-y-1">
								{#each section.items as item}
									{@const Icon = navIconFor(item.slug)}
									<a
										href={withRole(item.href)}
										title={item.label}
										class={`group flex min-h-11 items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium leading-5 transition-colors ${
											isActive(item)
												? 'bg-[var(--nav-active-bg)] text-[var(--nav-active-text)] shadow-sm'
												: 'text-white/70 hover:bg-white/10 hover:text-white'
										}`}
									>
										<span class="flex h-5 w-5 shrink-0 items-center justify-center text-current" aria-hidden="true">
											<Icon class="h-4 w-4" aria-hidden="true" />
										</span>
										<span class="truncate">{item.label}</span>
									</a>
								{/each}
							</div>
						</div>
					{:else}
						<div class={sectionIndex === 0 ? 'space-y-1' : 'mt-4 space-y-1'}>
							{#if sectionIndex > 0}
								<div class="mx-auto mb-2 h-px w-8 bg-white/10"></div>
							{/if}
							{#each section.items as item}
								{@const Icon = navIconFor(item.slug)}
								<a
									href={withRole(item.href)}
									title={`${section.label}: ${item.label}`}
									class={`group flex min-h-11 items-center justify-center rounded-md px-3 py-2.5 text-sm font-medium leading-5 transition-colors ${
										isActive(item)
											? 'bg-[var(--nav-active-bg)] text-[var(--nav-active-text)] shadow-sm'
											: 'text-white/70 hover:bg-white/10 hover:text-white'
										}`}
								>
									<span class="flex h-5 w-5 shrink-0 items-center justify-center" aria-hidden="true">
										<Icon class="h-4 w-4" aria-hidden="true" />
									</span>
								</a>
							{/each}
						</div>
					{/if}
				{/each}
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
							<LogOut class="h-4 w-4" aria-hidden="true" />
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
						<div class="flex h-9 w-9 items-center justify-center rounded-md bg-[var(--brand-solid)]">
							<Building2 class="h-5 w-5" aria-hidden="true" />
						</div>
						<div>
							<p class="text-base font-semibold leading-5">TurnKeyOps</p>
							<p class="text-xs text-white/55">Contractor workspace</p>
						</div>
						<button
							type="button"
							class="ml-auto inline-flex h-9 w-9 items-center justify-center rounded-md text-white/70 hover:bg-white/10 hover:text-white"
							aria-label="Close navigation"
							onclick={() => (sidebarOpen = false)}
						>
							<X class="h-5 w-5" aria-hidden="true" />
						</button>
					</div>

					<nav class="flex-1 overflow-y-auto px-3 py-4" aria-label="Mobile navigation">
						{#each groupedNavItems as section, sectionIndex}
							<div class={sectionIndex === 0 ? '' : 'mt-5'}>
								<div class="mb-2 px-3">
									<p class="text-[0.68rem] font-semibold text-white/45">{section.label}</p>
								</div>
								<div class="space-y-1">
									{#each section.items as item}
										{@const Icon = navIconFor(item.slug)}
										<a
											href={withRole(item.href)}
											class={`flex min-h-11 items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium leading-5 transition-colors ${
												isActive(item)
													? 'bg-[var(--nav-active-bg)] text-[var(--nav-active-text)] shadow-sm'
													: 'text-white/70 hover:bg-white/10 hover:text-white'
											}`}
											onclick={() => (sidebarOpen = false)}
										>
											<span class="flex h-5 w-5 shrink-0 items-center justify-center" aria-hidden="true">
												<Icon class="h-4 w-4" aria-hidden="true" />
											</span>
											<span>{item.label}</span>
										</a>
									{/each}
								</div>
							</div>
						{/each}
					</nav>
				</aside>
			</div>
		{/if}

		<div class="flex min-w-0 flex-1 flex-col">
			<header class="sticky top-0 z-30 border-b border-[var(--shell-border)] bg-[var(--topbar-bg)] backdrop-blur">
				<div class="flex min-h-16 items-center justify-between gap-3 px-4 lg:px-6">
					<div class="flex min-w-0 items-center gap-3">
						<button
							type="button"
							class="inline-flex h-10 w-10 items-center justify-center rounded-md border border-[var(--shell-border)] bg-white text-[var(--text-base)] shadow-sm lg:hidden"
							aria-label="Open navigation"
							onclick={() => (sidebarOpen = true)}
						>
							<Menu class="h-5 w-5" aria-hidden="true" />
						</button>
						<div class="min-w-0">
							<p class="truncate text-[0.7rem] font-medium text-[var(--text-muted)]">{activeNav.label}</p>
							<p class="truncate text-sm font-semibold leading-5 text-[var(--text-strong)]">{title}</p>
						</div>
					</div>

					<div class="hidden items-center gap-2 sm:flex">
						<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1 text-[0.7rem] font-semibold text-[var(--text-base)]">
							{roleLabel}
						</span>
					</div>
				</div>
			</header>

			<main class="min-w-0 flex-1 overflow-auto px-4 py-4 lg:px-6 lg:py-6">
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
</style>
