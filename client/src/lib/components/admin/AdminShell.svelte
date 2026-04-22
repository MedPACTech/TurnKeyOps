<script lang="ts">
	import { onMount } from 'svelte';
	import {
		bdrAdminNavigation,
		bdrAdminNavSections,
		bdrAdminRoleMeta,
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
		context,
		focus,
		canvas,
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
		children: () => unknown;
	}>();

	const roleMeta = $derived(bdrAdminRoleMeta[role as BdrAdminRole]);
	const withRole = (href: string) => `${href}?role=${role}`;
	const groupedNav = $derived(
		bdrAdminNavSections
			.map((section) => ({
				...section,
				items: bdrAdminNavigation.filter((item) => item.section === section.key)
			}))
			.filter((section) => section.items.length > 0)
	);

	const navItems = $derived(
		groupedNav.flatMap((section) =>
			section.items.map((item) => ({
				...item,
				sectionLabel: section.label,
				iconKey: item.slug
			}))
		)
	);

	let theme = $state<'dark' | 'light'>('dark');
	let navCollapsed = $state(false);
	let profileOpen = $state(false);
	const currentUserName = 'Ella Robinson';
	const currentUserEmail = 'ella.robinson@medpactech.com';

	const applyTheme = (value: 'dark' | 'light') => {
		theme = value;
		if (typeof localStorage !== 'undefined') {
			localStorage.setItem('bdr-admin-theme', value);
		}
	};

	const strokeClass = 'fill-none stroke-current stroke-[1.9] stroke-linecap-round stroke-linejoin-round';

	onMount(() => {
		const stored = localStorage.getItem('bdr-admin-theme');
		if (stored === 'light' || stored === 'dark') {
			theme = stored;
			return;
		}

		theme = 'light';
	});
</script>

<div class={`admin-shell min-h-screen ${theme === 'light' ? 'theme-light' : 'theme-dark'}`}>
	<header class="shell-topbar fixed inset-x-0 top-0 z-40 border-b-2 border-[var(--shell-border-strong)] bg-[var(--topbar-bg)]/96 backdrop-blur">
		<div class="mx-auto flex h-14 w-full max-w-[1680px] items-center justify-between gap-4 px-3 lg:px-5">
			<div class="flex min-w-0 items-center gap-3">
				<div class="flex h-8 w-8 items-center justify-center overflow-hidden rounded-md border border-[var(--shell-border)] bg-white p-1">
					<img src="/clientFiles/BDRLogo.jpeg" alt="BDR Construction logo" class="h-full w-full object-contain" />
				</div>
				<div class="min-w-0">
					<div class="flex items-center gap-2">
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.28em] text-[var(--muted)]">BDR Admin</p>
						<span class="rounded-sm border border-[var(--accent-border)] bg-[var(--accent-soft)] px-1.5 py-0.5 text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">Live</span>
					</div>
					<p class="truncate text-sm text-[var(--text-muted)]">Contractor operations desk · {activeNav.label}</p>
				</div>
			</div>


			<button
				type="button"
				class="inline-flex items-center gap-2 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-2.5 py-1.5 text-sm font-medium text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]"
				onclick={() => (profileOpen = true)}
				aria-label="Open profile settings"
			>
				<div class="flex h-8 w-8 items-center justify-center rounded-md border border-[var(--shell-border)] bg-[var(--accent-soft)] text-[11px] font-semibold text-[var(--accent-text)]">IP</div>
				<div class="hidden items-start sm:flex sm:flex-col">
					<span class="text-sm">Profile</span>
					<span class="text-[10px] uppercase tracking-[0.18em] text-[var(--text-muted)]">Settings</span>
				</div>
			</button>
		</div>
	</header>

	<div class="mx-auto flex min-h-screen w-full max-w-[1680px] gap-0 px-0 pt-14">
		<aside class={`sticky top-14 hidden h-[calc(100vh-3.5rem)] shrink-0 border-r border-[var(--shell-border)] bg-[var(--pane-bg)] transition-[width] duration-200 lg:flex lg:flex-col ${navCollapsed ? 'w-[84px]' : 'w-[320px]'}`}>
			<div class="flex items-center justify-between border-b border-[var(--shell-border)] px-3 py-3">
				{#if !navCollapsed}
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Navigation</p>
						<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">BDR Admin</p>
					</div>
				{/if}
				<button
					type="button"
					class="flex h-10 w-10 items-center justify-center rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] text-sm font-semibold text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]"
					onclick={() => (navCollapsed = !navCollapsed)}
					aria-label={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}
				>
					{navCollapsed ? '→' : '←'}
				</button>
			</div>

			<div class="min-h-0 flex-1 overflow-y-auto px-2 py-3">
				<div class="space-y-1">
					{#each navItems as item}
						<a
							href={withRole(item.href)}
							title={`${item.label} · ${item.summary}`}
							class={`flex items-start gap-3 rounded-xl border px-3 py-3 transition ${
								activePath === item.href
									? 'border-[var(--accent-border)] bg-[var(--accent-soft)]'
									: 'border-transparent hover:border-[var(--shell-border)] hover:bg-[var(--shell-panel)]'
							}`}
						>
							<div class={`flex h-12 w-12 shrink-0 items-center justify-center rounded-lg border ${
								activePath === item.href
									? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]'
									: 'border-[var(--shell-border)] bg-[var(--shell-panel)] text-[var(--text-muted)]'
							}`}>
								<svg viewBox="0 0 24 24" class="h-6 w-6" aria-hidden="true">
									{#if item.iconKey === 'dashboard'}
										<path class={strokeClass} d="M3 10.5 12 3l9 7.5" />
										<path class={strokeClass} d="M5 9.5V21h14V9.5" />
										<path class={strokeClass} d="M9.5 21v-6h5v6" />
									{:else if item.iconKey === 'calendar'}
										<rect class={strokeClass} x="3" y="5" width="18" height="16" rx="2" />
										<path class={strokeClass} d="M16 3v4M8 3v4M3 10h18" />
									{:else if item.iconKey === 'estimates'}
										<rect class={strokeClass} x="6" y="4" width="12" height="16" rx="2" />
										<path class={strokeClass} d="M9 4.5h6v3H9z" />
										<path class={strokeClass} d="M9 13l2 2 4-4" />
									{:else if item.iconKey === 'invoices'}
										<circle class={strokeClass} cx="12" cy="12" r="9" />
										<path class={strokeClass} d="M14.5 8.5c-.6-.6-1.5-1-2.5-1-1.7 0-3 1-3 2.4 0 3.2 6 1.6 6 4.5 0 1.4-1.3 2.4-3 2.4-1.1 0-2.2-.4-2.9-1.1" />
										<path class={strokeClass} d="M12 6.5v11" />
									{:else if item.iconKey === 'customers'}
										<path class={strokeClass} d="M16 20v-1a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v1" />
										<circle class={strokeClass} cx="10" cy="8" r="3" />
										<path class={strokeClass} d="M20 20v-1a3.5 3.5 0 0 0-3-3.46" />
										<path class={strokeClass} d="M15 5.2a3 3 0 0 1 0 5.6" />
									{:else if item.iconKey === 'requests'}
										<path class={strokeClass} d="M22 16.9v3a2 2 0 0 1-2.2 2A19.9 19.9 0 0 1 11 18.7 19.5 19.5 0 0 1 5.3 13 19.9 19.9 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7l.4 2.6a2 2 0 0 1-.6 1.8l-1.8 1.8a16 16 0 0 0 6 6l1.8-1.8a2 2 0 0 1 1.8-.6l2.6.4A2 2 0 0 1 22 16.9Z" />
									{:else if item.iconKey === 'content'}
										<circle class={strokeClass} cx="12" cy="12" r="9" />
										<path class={strokeClass} d="M3 12h18M12 3a15 15 0 0 1 0 18M12 3a15 15 0 0 0 0 18" />
									{:else if item.iconKey === 'settings'}
										<circle class={strokeClass} cx="12" cy="12" r="3" />
										<path class={strokeClass} d="M19.4 15a1 1 0 0 0 .2 1.1l.1.1a2 2 0 0 1-2.8 2.8l-.1-.1a1 1 0 0 0-1.1-.2 1 1 0 0 0-.6.9V20a2 2 0 0 1-4 0v-.2a1 1 0 0 0-.7-.9 1 1 0 0 0-1.1.2l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1 1 0 0 0 .2-1.1 1 1 0 0 0-.9-.6H4a2 2 0 0 1 0-4h.2a1 1 0 0 0 .9-.7 1 1 0 0 0-.2-1.1l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1 1 0 0 0 1.1.2H9a1 1 0 0 0 .6-.9V4a2 2 0 0 1 4 0v.2a1 1 0 0 0 .7.9 1 1 0 0 0 1.1-.2l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1 1 0 0 0-.2 1.1V9c0 .4.2.7.6.9h.2a2 2 0 0 1 0 4h-.2a1 1 0 0 0-.9.6Z" />
									{:else}
										<circle class={strokeClass} cx="12" cy="12" r="8" />
									{/if}
								</svg>
							</div>
							{#if !navCollapsed}
								<div class="min-w-0">
									<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{item.label}</p>
									<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.summary}</p>
								</div>
							{/if}
						</a>
					{/each}
				</div>
			</div>

			<div class="border-t border-[var(--shell-border)] px-2 py-3">
				<div class={`flex gap-2 ${navCollapsed ? 'flex-col items-center' : 'items-center'}`}>
					<button
						type="button"
						class={`flex h-10 ${navCollapsed ? 'w-10' : 'flex-1'} items-center justify-center rounded-md border text-[11px] font-semibold uppercase tracking-[0.16em] transition ${theme === 'light' ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]' : 'border-[var(--shell-border)] text-[var(--text-muted)] hover:bg-[var(--shell-panel)]'}`}
						onclick={() => applyTheme('light')}
					>
						L
					</button>
					<button
						type="button"
						class={`flex h-10 ${navCollapsed ? 'w-10' : 'flex-1'} items-center justify-center rounded-md border text-[11px] font-semibold uppercase tracking-[0.16em] transition ${theme === 'dark' ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]' : 'border-[var(--shell-border)] text-[var(--text-muted)] hover:bg-[var(--shell-panel)]'}`}
						onclick={() => applyTheme('dark')}
					>
						D
					</button>
				</div>
			</div>
		</aside>

		<main class="canvas-pane min-w-0 flex-1 bg-[var(--drawer-bg)]">
			<div class="flex h-[calc(100vh-3.5rem)] min-h-0 flex-col">
				

				<section class="min-h-0 flex-1 overflow-y-auto px-4 py-4 lg:px-6 lg:py-5">
					{@render children()}
				</section>
			</div>
		</main>
	</div>
</div>

{#if profileOpen}
	<button
		class="fixed inset-0 z-50 bg-slate-900/30"
		type="button"
		aria-label="Close profile view"
		onclick={() => (profileOpen = false)}
	></button>
	<aside class="fixed inset-y-0 right-0 z-[60] flex w-full max-w-sm flex-col border-l-2 border-[var(--shell-border-strong)] shadow-[0_20px_70px_rgba(15,23,42,0.16)]" style={`background-color: ${theme === 'dark' ? '#020817' : '#ffffff'}`}>
		<div class="flex items-center justify-between border-b-2 border-[var(--shell-border-strong)] px-5 py-4">
			<div>
				<p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Profile</p>
				<h2 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">Operator settings</h2>
			</div>
			<button type="button" class="rounded-md border-2 border-[var(--shell-border-strong)] px-3 py-1.5 text-sm font-medium text-[var(--text-strong)]" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`} onclick={() => (profileOpen = false)}>Close</button>
		</div>

		<div class="flex-1 space-y-4 overflow-y-auto px-5 py-5">
			<section class="rounded-md border-2 border-[var(--shell-border-strong)] p-4" style={`background-color: ${theme === 'dark' ? '#0f172a' : '#ffffff'}`}>
				<div class="flex items-center gap-3">
					<div class="flex h-14 w-14 items-center justify-center rounded-md border border-[var(--shell-border)] bg-[var(--accent-soft)] text-sm font-semibold text-[var(--accent-text)]">IP</div>
					<div>
						<p class="text-base font-semibold text-[var(--text-strong)]">Implementation Profile</p>
						<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">TurnKeyOps admin operator</p>
					</div>
				</div>
			</section>

			<section class="rounded-md border-2 border-[var(--shell-border-strong)] p-4" style={`background-color: ${theme === 'dark' ? '#0f172a' : '#ffffff'}`}>
				<p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Preferences</p>
				<div class="mt-3 grid gap-2">
					<button type="button" class="flex items-center justify-between rounded-md border-2 border-[var(--shell-border-strong)] px-3 py-3 text-sm text-[var(--text-base)]" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`} onclick={() => applyTheme('light')}>
						<span>Light operating mode</span>
						<span class="font-semibold text-[var(--accent-text)]">{theme === 'light' ? 'Active' : 'Set'}</span>
					</button>
					<button type="button" class="flex items-center justify-between rounded-md border-2 border-[var(--shell-border-strong)] px-3 py-3 text-sm text-[var(--text-base)]" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`} onclick={() => applyTheme('dark')}>
						<span>Dark operating mode</span>
						<span class="font-semibold text-[var(--accent-text)]">{theme === 'dark' ? 'Active' : 'Set'}</span>
					</button>
				</div>
			</section>

			<section class="rounded-md border-2 border-[var(--shell-border-strong)] p-4" style={`background-color: ${theme === 'dark' ? '#0f172a' : '#ffffff'}`}>
				<p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Role context</p>
				<div class="mt-3 rounded-md border border-[var(--shell-border)] p-3" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`}>
					<p class="text-base font-semibold text-[var(--text-strong)]">{roleMeta.label}</p>
					<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{roleMeta.description}</p>
				</div>
			</section>
		</div>
	</aside>
{/if}

<style>
	.admin-shell {
		--shell-shadow: 0 14px 34px rgba(15, 23, 42, 0.08);
	}

	.theme-light {
		--shell-border: rgba(15, 23, 42, 0.08);
		--text-strong: #0f172a;
		--text-base: #223046;
		--text-muted: #66758b;
		--muted: #8a97aa;
		--accent-soft: rgba(234, 88, 12, 0.08);
		--accent-border: rgba(234, 88, 12, 0.2);
		--accent-text: #c2410c;
		--accent-solid: #ea580c;
		--accent-solid-text: #fff7ed;
		--topbar-bg: rgba(255, 255, 255, 0.92);
		--rail-bg: #f6f8fb;
		--pane-bg: #fbfcfe;
		--canvas-bg: #f4f7fb;
		--module-bg: #ffffff;
		--shell-card: #ffffff;
		--shell-panel: #ffffff;
		--shell-panel-strong: #f8fafc;
		--drawer-bg: #ffffff;
		--drawer-card-bg: #ffffff;
		color: var(--text-base);
		background: linear-gradient(180deg, #f8fafc 0%, #f3f6fa 100%);
	}

	.theme-dark {
		--shell-border: rgba(255, 255, 255, 0.08);
		--text-strong: #f8fafc;
		--text-base: #e2e8f0;
		--text-muted: #9aa9bc;
		--muted: #7b8a9d;
		--accent-soft: rgba(249, 115, 22, 0.14);
		--accent-border: rgba(249, 115, 22, 0.28);
		--accent-text: #fdba74;
		--accent-solid: #f97316;
		--accent-solid-text: #fff7ed;
		--topbar-bg: rgba(8, 15, 29, 0.94);
		--rail-bg: #09111f;
		--pane-bg: #0c1728;
		--canvas-bg: #111827;
		--module-bg: rgba(15, 23, 42, 0.88);
		--shell-card: rgba(15, 23, 42, 0.9);
		--shell-panel: #1f2937;
		--shell-panel-strong: #273449;
		--drawer-bg: #0f172a;
		--drawer-card-bg: #111827;
		color: var(--text-base);
		background: linear-gradient(180deg, #050b14 0%, #101827 100%);
	}
</style>
