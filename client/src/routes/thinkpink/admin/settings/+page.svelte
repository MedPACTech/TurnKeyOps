<script lang="ts">
	import { untrack } from 'svelte';
	import type { PageProps } from './$types';

	let { data, form }: PageProps = $props();
	type Settings = PageProps['data']['settings'];
	type NumberKey = {
		[K in keyof Settings]: Settings[K] extends number ? K : never
	}[keyof Settings];
	type Field = { key: NumberKey; label: string; prefix?: string; suffix?: string; step?: string };
	type Section = { title: string; description: string; icon: string; fields: Field[] };

	const sections: Section[] = [
		{
			title: 'Clearing & mulching',
			description: 'Base production rates used when Bob and the estimate workflow build a starting cost.',
			icon: '🌲',
			fields: [
				{ key: 'landClearingPerAcre', label: 'Land clearing / acre', prefix: '$' },
				{ key: 'forestryMulchingPerAcre', label: 'Forestry mulching / acre', prefix: '$' },
				{ key: 'brushClearingPerAcre', label: 'Brush clearing / acre', prefix: '$' },
				{ key: 'grindingPerHour', label: 'Grinding / hour', prefix: '$' }
			]
		},
		{
			title: 'Trees & stumps',
			description: 'Starting rates by tree class and stump count.',
			icon: '🪵',
			fields: [
				{ key: 'treeRemovalSmall', label: 'Small tree removal', prefix: '$' },
				{ key: 'treeRemovalMedium', label: 'Medium tree removal', prefix: '$' },
				{ key: 'treeRemovalLarge', label: 'Large tree removal', prefix: '$' },
				{ key: 'stumpGrindingEach', label: 'Stump grinding / each', prefix: '$' }
			]
		},
		{
			title: 'Mobilization & disposal',
			description: 'Travel, hauling, disposal, grading, and restoration assumptions.',
			icon: '🚛',
			fields: [
				{ key: 'mobilizationFee', label: 'Mobilization fee', prefix: '$' },
				{ key: 'haulOffPerLoad', label: 'Haul-off / load', prefix: '$' },
				{ key: 'disposalPerLoad', label: 'Disposal / load', prefix: '$' },
				{ key: 'gradingPerAcre', label: 'Grading / acre', prefix: '$' },
				{ key: 'restorationPerAcre', label: 'Restoration / acre', prefix: '$' },
				{ key: 'travelCharge', label: 'Travel charge', prefix: '$' }
			]
		},
		{
			title: 'Labor & equipment',
			description: 'Crew and daily equipment defaults for new estimates.',
			icon: '🚜',
			fields: [
				{ key: 'laborRatePerHour', label: 'Labor / hour', prefix: '$' },
				{ key: 'defaultCrewSize', label: 'Default crew size', step: '1', suffix: 'people' },
				{ key: 'overtimeMultiplier', label: 'Overtime multiplier', step: '0.05', suffix: '×' },
				{ key: 'skidSteerPerDay', label: 'Skid steer / day', prefix: '$' },
				{ key: 'excavatorPerDay', label: 'Excavator / day', prefix: '$' },
				{ key: 'forestryMulcherPerDay', label: 'Forestry mulcher / day', prefix: '$' },
				{ key: 'chipperPerDay', label: 'Chipper / day', prefix: '$' },
				{ key: 'dumpTruckPerDay', label: 'Dump truck / day', prefix: '$' }
			]
		},
		{
			title: 'Margin & billing',
			description: 'Company-level pricing protection and the deposit gate for scheduling.',
			icon: '📈',
			fields: [
				{ key: 'overheadPercent', label: 'Overhead', suffix: '%' },
				{ key: 'contingencyPercent', label: 'Contingency', suffix: '%' },
				{ key: 'profitPercent', label: 'Profit', suffix: '%' },
				{ key: 'taxPercent', label: 'Tax', suffix: '%' },
				{ key: 'depositPercentRequired', label: 'Deposit before scheduling', suffix: '%' }
			]
		}
	];

	let saved = $state<Settings>(untrack(() => structuredClone(data.settings)));
	let settings = $state<Settings>(untrack(() => structuredClone(data.settings)));
	const hasChanges = $derived(JSON.stringify(saved) !== JSON.stringify(settings));

	const updateNumber = (key: NumberKey, raw: string) => {
		const number = Number(raw);
		settings = {
			...settings,
			[key]: Number.isFinite(number) ? Math.max(0, number) : 0
		};
	};
	const updateList = (key: 'services' | 'estimateInputs' | 'jobStages', raw: string) => {
		settings = {
			...settings,
			[key]: raw.split(/\r?\n/).map((item) => item.trim()).filter(Boolean)
		};
	};
	const reset = () => (settings = structuredClone(saved));

	$effect(() => {
		if (form?.saved && form.settings) {
			saved = structuredClone(form.settings);
			settings = structuredClone(form.settings);
		}
	});
</script>

<svelte:head><title>Admin · Think Pink</title></svelte:head>

<form method="POST" action="?/save" class="mx-auto max-w-7xl space-y-5 pb-10">
	<header class="sticky top-0 z-10 flex flex-col gap-4 border-b border-[var(--shell-border)] bg-[var(--shell-canvas)]/95 py-5 backdrop-blur sm:flex-row sm:items-end sm:justify-between">
		<div>
			<p class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--accent-text)]">Owner configuration</p>
			<h1 class="mt-2 text-3xl font-black tracking-tight text-[var(--text-strong)]">Land-clearing defaults</h1>
			<p class="mt-2 max-w-3xl text-sm text-[var(--text-muted)]">Set the pricing, operating, estimate, and workflow defaults Think Pink uses. These settings remain isolated from BDR.</p>
		</div>
		<div class="flex gap-3">
			<button type="button" onclick={reset} disabled={!hasChanges} class="min-h-11 rounded-lg border border-[var(--shell-border)] bg-white px-5 text-sm font-semibold disabled:opacity-40">Reset</button>
			<button type="submit" disabled={!hasChanges} class="min-h-11 rounded-lg bg-[var(--accent-text)] px-5 text-sm font-semibold text-white disabled:opacity-40">Save defaults</button>
		</div>
	</header>

	{#if form?.saved}<p class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-800">Think Pink defaults saved.</p>{/if}
	{#if form?.message}<p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-800">{form.message}</p>{/if}

	{#each sections as section}
		<section class="rounded-xl bg-white p-5 shadow-[var(--shell-shadow)]">
			<div class="flex items-start gap-4">
				<div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-xl">{section.icon}</div>
				<div>
					<h2 class="text-lg font-bold text-[var(--text-strong)]">{section.title}</h2>
					<p class="mt-1 text-sm text-[var(--text-muted)]">{section.description}</p>
				</div>
			</div>
			<div class="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
				{#each section.fields as field}
					<label class="block">
						<span class="mb-2 block text-sm font-semibold text-[var(--text-base)]">{field.label}</span>
						<span class="flex min-h-11 items-center rounded-lg border border-[var(--shell-border)] bg-white focus-within:border-[var(--accent-border)]">
							{#if field.prefix}<span class="pl-3 text-sm text-[var(--text-muted)]">{field.prefix}</span>{/if}
							<input name={field.key} type="number" min="0" step={field.step ?? '0.01'} value={settings[field.key]} oninput={(event) => updateNumber(field.key, event.currentTarget.value)} class="min-w-0 flex-1 border-0 bg-transparent px-3 py-2 text-sm outline-none" />
							{#if field.suffix}<span class="pr-3 text-xs text-[var(--text-muted)]">{field.suffix}</span>{/if}
						</span>
					</label>
				{/each}
			</div>
		</section>
	{/each}

	<section class="grid gap-5 lg:grid-cols-3">
		{#each [
			{ key: 'services', title: 'Services', help: 'One service per line. Bob uses these when classifying requests.' },
			{ key: 'estimateInputs', title: 'Estimate inputs', help: 'One required site or pricing input per line.' },
			{ key: 'jobStages', title: 'Job stages', help: 'One production stage per line, in operating order.' }
		] as list}
			<label class="rounded-xl bg-white p-5 shadow-[var(--shell-shadow)]">
				<span class="block text-lg font-bold text-[var(--text-strong)]">{list.title}</span>
				<span class="mt-1 block text-sm text-[var(--text-muted)]">{list.help}</span>
				<textarea
					name={list.key}
					rows="9"
					value={settings[list.key as 'services' | 'estimateInputs' | 'jobStages'].join('\n')}
					oninput={(event) => updateList(list.key as 'services' | 'estimateInputs' | 'jobStages', event.currentTarget.value)}
					class="mt-4 w-full rounded-lg border border-[var(--shell-border)] p-3 text-sm leading-7 outline-none focus:border-[var(--accent-border)]"
				></textarea>
			</label>
		{/each}
	</section>
</form>
