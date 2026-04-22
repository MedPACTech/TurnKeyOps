<script lang="ts">
	import { onMount } from 'svelte';
	import type { TurnkeyOpsAdminNavItem } from '$lib/config/platform';

	type ShellMetric = { label: string; value: string; detail: string };
	type ShellNote = { title: string; detail: string; tone?: string };
	type ShellAction = { label: string; href: string; variant?: string };

	const navGlyphs: Record<string, string> = {
		dashboard: '◫',
		tenants: '◧',
		playbooks: '☰',
		health: '◌',
		access: '⌘'
	};


	let theme = $state<'light' | 'dark'>('light');
	let profileOpen = $state(false);
	const currentUserName = 'Ella Robinson';
	const currentUserEmail = 'ella.robinson@medpactech.com';

	const applyTheme = (value: 'light' | 'dark') => {
		theme = value;
		if (typeof localStorage !== 'undefined') {
			localStorage.setItem('turnkeyops-admin-theme', value);
		}
	};

	onMount(() => {
		const stored = localStorage.getItem('turnkeyops-admin-theme');
		if (stored === 'light' || stored === 'dark') {
			theme = stored;
		}
	});

	let {
		activePath,
		activeNav,
		title,
		description,
		badge = 'Platform console',
		context,
		focus,
		canvas,
		children
	} = $props<{
		activePath: string;
		activeNav: TurnkeyOpsAdminNavItem;
		title: string;
		description: string;
		badge?: string;
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
</script>

<div class={`platform-shell min-h-screen text-[var(--text-base)] ${theme === 'dark' ? 'theme-dark' : 'theme-light'}`}>
	<header class="fixed inset-x-0 top-0 z-30 border-b-2 border-[var(--shell-border-strong)] bg-[rgba(248,250,252,0.92)] backdrop-blur">
		<div class="mx-auto flex h-14 w-full max-w-[1800px] items-center justify-between gap-4 px-4 lg:px-6">
			<div class="flex min-w-0 items-center gap-3">
				<div class="flex h-8 w-8 items-center justify-center rounded-md border border-[var(--shell-border-strong)] bg-white text-[0.72rem] font-semibold tracking-[0.16em] text-[var(--text-strong)]">
					TK
				</div>
				<div class="min-w-0">
					<p class="text-[0.62rem] uppercase tracking-[0.24em] text-[var(--muted)]">TurnKeyOps / Admin</p>
					<p class="truncate text-base font-semibold text-[var(--text-strong)]">{title}</p>
				</div>
			</div>

			<div class="hidden items-center gap-2 xl:flex">
				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] [background:var(--shell-panel)] px-3 py-1.5 text-[0.7rem] uppercase tracking-[0.18em] text-[var(--muted)]">
					{activeNav.contextLabel}
				</div>
				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] [background:var(--shell-panel)] px-3 py-1.5 text-[0.7rem] uppercase tracking-[0.18em] text-[var(--muted)]">
					{activeNav.focusLabel}
				</div>
				<div class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1.5 text-[0.7rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">
					{badge}
				</div>
			</div>

			<button class="inline-flex items-center gap-2 rounded-md border border-[var(--shell-border-strong)] bg-[var(--module-bg)] px-2.5 py-1.5 text-sm font-medium text-[var(--text-strong)] shadow-[0_1px_2px_rgba(15,23,42,0.05)]" onclick={() => (profileOpen = true)}><span class="flex h-7 w-7 items-center justify-center rounded-[999px] bg-[var(--accent-solid)] text-xs font-semibold text-white">ER</span><span class="hidden sm:inline">Profile</span></button>
		</div>
	</header>

	<div class="mx-auto flex w-full max-w-[1800px] gap-0 px-2 pb-2 pt-14 lg:px-3">
		<aside class="sticky top-14 hidden h-[calc(100vh-3.75rem)] w-[72px] shrink-0 flex-col justify-between border-r border-[var(--shell-border)] bg-[var(--rail-bg)] py-3 lg:flex">
			<div class="space-y-2 px-3">
				{#each activeNav.allNav as item}
					<a
						href={item.href}
						class={`flex h-11 w-11 items-center justify-center rounded-md border text-sm font-semibold transition ${
							activePath === item.href
								? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-solid)]'
								: 'border-transparent bg-transparent text-[var(--muted-strong)] hover:border-[var(--shell-border)] hover:bg-white hover:text-[var(--text-strong)]'
						}`}
						title={item.label}
					>
						{navGlyphs[item.slug] ?? item.label.slice(0, 1)}
					</a>
				{/each}
			</div>

			<div class="px-3">
				<a href="/bdr/admin" class="flex h-11 w-11 items-center justify-center rounded-md border border-transparent text-[0.68rem] font-semibold tracking-[0.16em] text-[var(--muted-strong)] transition hover:border-[var(--shell-border)] hover:bg-white hover:text-[var(--text-strong)]" title="BDR tenant admin">BDR</a>
			</div>
		</aside>

		<aside class="sticky top-14 hidden h-[calc(100vh-3.75rem)] w-[350px] shrink-0 border-r border-[var(--shell-border)] bg-[var(--pane-bg)] xl:block">
			<div class="flex h-full flex-col">
				<div class="border-b-2 border-[var(--shell-border-strong)] px-5 py-4">
					<p class="text-[0.63rem] uppercase tracking-[0.22em] text-[var(--muted)]">Route queue</p>
					<h1 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">{activeNav.label}</h1>
					<p class="mt-2 text-sm leading-5 text-[var(--text-muted)]">{activeNav.summary}</p>
				</div>

				<div class="flex-1 space-y-4 overflow-y-auto px-4 py-4">
					<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
						<p class="text-[0.62rem] uppercase tracking-[0.22em] text-[var(--muted)]">{context.label}</p>
						<h2 class="mt-2 text-base font-semibold text-[var(--text-strong)]">{context.title}</h2>
						<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{context.summary}</p>
					</section>

					<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
						<div class="flex items-center justify-between gap-3">
							<div>
								<p class="text-[0.62rem] uppercase tracking-[0.22em] text-[var(--muted)]">{focus.label}</p>
								<h2 class="mt-1 text-base font-semibold text-[var(--text-strong)]">{focus.title}</h2>
							</div>
							<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-2 py-1 text-[0.66rem] uppercase tracking-[0.18em] text-[var(--muted)]">Active</span>
						</div>
						<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{focus.summary}</p>

						<div class="mt-4 space-y-2.5">
							{#each focus.notes as note}
								<div class={`rounded-md border px-3 py-3 ${note.tone === 'accent' ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)]'}`}>
									<p class="text-base font-semibold text-[var(--text-strong)]">{note.title}</p>
									<p class="mt-1.5 text-sm leading-5 text-[var(--text-muted)]">{note.detail}</p>
								</div>
							{/each}
						</div>
					</section>

					<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
						<p class="text-[0.62rem] uppercase tracking-[0.22em] text-[var(--muted)]">Live signals</p>
						<div class="mt-3 space-y-2.5">
							{#each context.metrics as metric}
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] [background:var(--shell-panel)] px-3 py-3">
									<div class="flex items-baseline justify-between gap-3">
										<p class="text-[0.68rem] uppercase tracking-[0.18em] text-[var(--muted)]">{metric.label}</p>
										<p class="text-base font-semibold text-[var(--text-strong)]">{metric.value}</p>
									</div>
									<p class="mt-1.5 text-sm leading-5 text-[var(--text-muted)]">{metric.detail}</p>
								</div>
							{/each}
						</div>
					</section>
				</div>
			</div>
		</aside>

		<main class="min-w-0 flex-1 bg-[var(--shell-bg)]">
			<div class="min-h-[calc(100vh-3.75rem)] px-3 py-4 lg:px-5 lg:py-5">
				<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--drawer-bg)] shadow-[0_18px_50px_rgba(15,23,42,0.06)]">

					<div class="min-h-0 px-4 py-4 lg:px-6 lg:py-5">
						{@render children()}
					</div>
				</div>
			</div>
		</main>
	</div>
</div>

{#if profileOpen}
	<button class="fixed inset-0 z-40 bg-slate-900/30" type="button" aria-label="Close profile view" onclick={() => (profileOpen = false)}></button>
	<aside class="fixed inset-y-0 right-0 z-50 flex w-full max-w-sm flex-col border-l-2 border-[var(--shell-border-strong)] shadow-[0_20px_70px_rgba(15,23,42,0.16)]" style={`background-color: ${theme === 'dark' ? '#020817' : '#ffffff'}`}>
		<div class="flex items-center justify-between border-b-2 border-[var(--shell-border-strong)] px-5 py-4">
			<div><p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Profile</p><h2 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">{currentUserName}</h2></div>
			<button type="button" class="rounded-md border-2 border-[var(--shell-border-strong)] px-3 py-1.5 text-sm font-medium text-[var(--text-strong)]" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`} onclick={() => (profileOpen = false)}>Close</button>
		</div>
		<div class="flex-1 space-y-4 overflow-y-auto px-5 py-5">
			<section class="rounded-md border-2 border-[var(--shell-border-strong)] p-4" style={`background-color: ${theme === 'dark' ? '#0f172a' : '#ffffff'}`}>
				<p class="text-base font-semibold text-[var(--text-strong)]">{currentUserName}</p>
				<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{currentUserEmail}</p>
			</section>
			<section class="rounded-md border-2 border-[var(--shell-border-strong)] p-4" style={`background-color: ${theme === 'dark' ? '#0f172a' : '#ffffff'}`}>
				<p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Appearance</p>
				<div class="mt-3 grid gap-2">
					<button type="button" class="flex items-center justify-between rounded-md border-2 border-[var(--shell-border-strong)] px-3 py-3 text-sm text-[var(--text-base)]" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`} onclick={() => applyTheme('light')}><span>Light mode</span><span class="font-semibold text-[var(--accent-text)]">{theme === 'light' ? 'Active' : 'Set'}</span></button>
					<button type="button" class="flex items-center justify-between rounded-md border-2 border-[var(--shell-border-strong)] px-3 py-3 text-sm text-[var(--text-base)]" style={`background-color: ${theme === 'dark' ? '#1f2937' : '#ffffff'}`} onclick={() => applyTheme('dark')}><span>Dark mode</span><span class="font-semibold text-[var(--accent-text)]">{theme === 'dark' ? 'Active' : 'Set'}</span></button>
				</div>
			</section>
		</div>
	</aside>
{/if}

<style>
	.platform-shell {

		--rail-bg: #eef2f7;
		--pane-bg: #f7f9fc;
		--canvas-bg: #fcfdff;
		--shell-panel: #f6f8fb;
		--shell-border: #d9e2ec;
		--shell-border-strong: #94a3b8;
		--text-strong: #152334;
		--text-base: #2e4358;
		--text-muted: #475569;
		--muted: #7f91a5;
		--muted-strong: #6a7d92;
		--accent-soft: #e7f0ff;
		--accent-border: #bfd4fb;
		--accent-text: #31558a;
	}

	.theme-light {
		--shell-bg: #f3f6fb;
		--rail-bg: #eef2f7;
		--pane-bg: #f7f9fc;
		--canvas-bg: #fcfdff;
		--module-bg: #ffffff;
		--shell-panel: #f6f8fb;
		--shell-border: #d9e2ec;
		--shell-border-strong: #94a3b8;
		--text-strong: #152334;
		--text-base: #2e4358;
		--text-muted: #475569;
		--muted: #7f91a5;
		--muted-strong: #6a7d92;
		--accent-soft: #e7f0ff;
		--accent-border: #bfd4fb;
		--accent-text: #31558a;
		--accent-solid: #3b82f6;
		--drawer-bg: #ffffff;
		--drawer-card-bg: #ffffff;
		background: #f3f6fb;
	}

	.theme-dark {
		--shell-bg: #0b1220;
		--rail-bg: #0f172a;
		--pane-bg: #111827;
		--canvas-bg: #0f172a;
		--module-bg: #111827;
		--shell-panel: #1f2937;
		--shell-border: rgba(255,255,255,0.09);
		--shell-border-strong: rgba(255,255,255,0.32);
		--text-strong: #f8fafc;
		--text-base: #e5e7eb;
		--text-muted: #cbd5e1;
		--muted: #64748b;
		--muted-strong: #cbd5e1;
		--accent-soft: rgba(59,130,246,0.16);
		--accent-border: rgba(96,165,250,0.28);
		--accent-text: #93c5fd;
		--accent-solid: #3b82f6;
		--drawer-bg: #020817;
		--drawer-card-bg: #0f172a;
		background: linear-gradient(180deg, #020617 0%, #0f172a 100%);
	}
</style>
