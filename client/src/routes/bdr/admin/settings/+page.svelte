<script lang="ts">
	import AdminContextRail from '$lib/components/admin/AdminContextRail.svelte';
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { untrack } from 'svelte';
	import type { PageProps } from './$types';

	type EstimateDefaults = PageProps['data']['estimateDefaults'];
	type DefaultsField = {
		key: keyof EstimateDefaults;
		label: string;
		help?: string;
		prefix?: string;
		suffix?: string;
		step?: string;
		min?: number;
	};
	type DefaultsSection = {
		title: string;
		subtitle: string;
		icon: string;
		fields: DefaultsField[];
	};

	let { data, form }: PageProps = $props();

	const sections: DefaultsSection[] = [
		{
			title: 'Concrete Pricing Defaults',
			subtitle: 'Base concrete and material charges inherited by new estimates.',
			icon: '🪨',
			fields: [
				{ key: 'concreteCostPerYard', label: 'Concrete Cost Per Yard', prefix: '$' },
				{ key: 'minimumLoadFee', label: 'Minimum Load Fee', prefix: '$' },
				{ key: 'shortLoadFee', label: 'Short Load Fee', prefix: '$' },
				{ key: 'deliveryFee', label: 'Delivery Fee', prefix: '$' },
				{ key: 'fuelSurcharge', label: 'Fuel Surcharge', prefix: '$' },
				{ key: 'defaultPumpFee', label: 'Default Pump Fee', prefix: '$' },
				{ key: 'additiveCost', label: 'Additive Cost', prefix: '$' },
				{ key: 'fiberMeshCost', label: 'Fiber Mesh Cost', prefix: '$' },
				{ key: 'colorCost', label: 'Color Cost', prefix: '$' },
				{ key: 'sealerCost', label: 'Sealer Cost', prefix: '$' }
			]
		},
		{
			title: 'Site Prep Defaults',
			subtitle: 'Prep assumptions for demolition, excavation, grading, and access.',
			icon: '🚧',
			fields: [
				{ key: 'demoCostRate', label: 'Demo Cost / Rate', prefix: '$' },
				{ key: 'excavationCostRate', label: 'Excavation Cost / Rate', prefix: '$' },
				{ key: 'haulOffFee', label: 'Haul Off Fee', prefix: '$' },
				{ key: 'baseMaterialUnitCost', label: 'Base Material Unit Cost', prefix: '$' },
				{ key: 'compactionCost', label: 'Compaction Cost', prefix: '$' },
				{ key: 'vaporBarrierCost', label: 'Vapor Barrier Cost', prefix: '$' },
				{ key: 'gradingCost', label: 'Grading Cost', prefix: '$' },
				{ key: 'accessDifficultyEasyPercent', label: 'Easy Access Adjustment', suffix: '%' },
				{ key: 'accessDifficultyModeratePercent', label: 'Moderate Access Adjustment', suffix: '%' },
				{ key: 'accessDifficultyHardPercent', label: 'Hard Access Adjustment', suffix: '%' }
			]
		},
		{
			title: 'Reinforcement Defaults',
			subtitle: 'Reusable reinforcement pricing assumptions.',
			icon: '🧱',
			fields: [
				{ key: 'rebarCostPerFoot', label: 'Rebar Cost Per Foot', prefix: '$' },
				{ key: 'meshCost', label: 'Mesh Cost', prefix: '$' },
				{ key: 'chairsCost', label: 'Chairs Cost', prefix: '$' },
				{ key: 'dowelsCost', label: 'Dowels Cost', prefix: '$' },
				{ key: 'anchorBoltsCost', label: 'Anchor Bolts Cost', prefix: '$' }
			]
		},
		{
			title: 'Formwork Defaults',
			subtitle: 'Default form material, complexity, and labor assumptions.',
			icon: '🪵',
			fields: [
				{ key: 'formMaterialCost', label: 'Form Material Cost', prefix: '$' },
				{ key: 'formComplexitySimpleMultiplier', label: 'Simple Complexity Multiplier', step: '0.05' },
				{ key: 'formComplexityStandardMultiplier', label: 'Standard Complexity Multiplier', step: '0.05' },
				{ key: 'formComplexityComplexMultiplier', label: 'Complex Complexity Multiplier', step: '0.05' },
				{ key: 'formLaborHoursPerLinearFoot', label: 'Form Labor Hours / Linear Ft', suffix: 'hrs', step: '0.05' }
			]
		},
		{
			title: 'Finish Defaults',
			subtitle: 'Surface finish and add-on pricing defaults.',
			icon: '✨',
			fields: [
				{ key: 'sawCutCost', label: 'Saw Cut Cost', prefix: '$' },
				{ key: 'jointMaterialCost', label: 'Joint Material Cost', prefix: '$' },
				{ key: 'expansionJointCost', label: 'Expansion Joint Cost', prefix: '$' },
				{ key: 'curingCompoundCost', label: 'Curing Compound Cost', prefix: '$' },
				{ key: 'stampPatternCost', label: 'Stamp Pattern Cost', prefix: '$' },
				{ key: 'decorativePremium', label: 'Decorative Premium', prefix: '$' }
			]
		},
		{
			title: 'Labor Defaults',
			subtitle: 'Crew assumptions and task-hour defaults used by new estimates.',
			icon: '👷',
			fields: [
				{ key: 'laborRatePerHour', label: 'Labor Rate Per Hour', prefix: '$' },
				{ key: 'overtimeMultiplier', label: 'Overtime Multiplier', step: '0.05' },
				{ key: 'defaultCrewSize', label: 'Default Crew Size', step: '1', min: 1 },
				{ key: 'demoHoursPer100SqFt', label: 'Demo Hours / 100 Sq Ft', suffix: 'hrs', step: '0.1' },
				{ key: 'prepHoursPer100SqFt', label: 'Prep Hours / 100 Sq Ft', suffix: 'hrs', step: '0.1' },
				{ key: 'formHoursPer100LinearFt', label: 'Form Hours / 100 Linear Ft', suffix: 'hrs', step: '0.1' },
				{ key: 'reinforcementHoursPer100SqFt', label: 'Reinforcement Hours / 100 Sq Ft', suffix: 'hrs', step: '0.1' },
				{ key: 'pourHoursPer100SqFt', label: 'Pour Hours / 100 Sq Ft', suffix: 'hrs', step: '0.1' },
				{ key: 'finishHoursPer100SqFt', label: 'Finish Hours / 100 Sq Ft', suffix: 'hrs', step: '0.1' }
			]
		},
		{
			title: 'Equipment Defaults',
			subtitle: 'Reusable equipment charges loaded into estimate calculations.',
			icon: '🚚',
			fields: [
				{ key: 'skidSteerCost', label: 'Skid Steer Cost', prefix: '$' },
				{ key: 'excavatorCost', label: 'Excavator Cost', prefix: '$' },
				{ key: 'compactorCost', label: 'Compactor Cost', prefix: '$' },
				{ key: 'sawEquipmentCost', label: 'Saw Equipment Cost', prefix: '$' },
				{ key: 'powerTrowelCost', label: 'Power Trowel Cost', prefix: '$' },
				{ key: 'trailerTruckCost', label: 'Trailer Truck Cost', prefix: '$' },
				{ key: 'generatorCost', label: 'Generator Cost', prefix: '$' },
				{ key: 'buggyCost', label: 'Buggy Cost', prefix: '$' },
				{ key: 'otherEquipmentCost', label: 'Other Equipment Cost', prefix: '$' }
			]
		},
		{
			title: 'Margin Defaults',
			subtitle: 'Overhead, margin, and risk settings for estimate rollups.',
			icon: '📈',
			fields: [
				{ key: 'overheadPercent', label: 'Overhead Percent', suffix: '%' },
				{ key: 'contingencyPercent', label: 'Contingency Percent', suffix: '%' },
				{ key: 'profitPercent', label: 'Profit Percent', suffix: '%' },
				{ key: 'taxPercent', label: 'Tax Percent', suffix: '%' },
				{ key: 'travelCharge', label: 'Travel Charge', prefix: '$' },
				{ key: 'rushFee', label: 'Rush Fee', prefix: '$' },
				{ key: 'weatherRiskAllowance', label: 'Weather Risk Allowance', prefix: '$' }
			]
		}
	];

	let savedDefaults = $state<EstimateDefaults>(untrack(() => structuredClone(data.estimateDefaults)));
	let defaultsForm = $state<EstimateDefaults>(untrack(() => structuredClone(data.estimateDefaults)));

	const hasChanges = $derived(JSON.stringify(savedDefaults) !== JSON.stringify(defaultsForm));
	const metrics = $derived([
		{ label: 'Sections', value: `${sections.length} groups` },
		{ label: 'Crew size', value: `${defaultsForm.defaultCrewSize} people` },
		{ label: 'Concrete cost', value: `$${defaultsForm.concreteCostPerYard} / yard` },
		{ label: 'Status', value: hasChanges ? 'Unsaved changes' : 'All changes saved' }
	]);

	const displayValue = (key: keyof EstimateDefaults) => {
		const value = defaultsForm[key];
		return Number.isFinite(value) ? String(value) : '';
	};

	function updateValue(key: keyof EstimateDefaults, raw: string) {
		const numeric = raw === '' ? 0 : Number(raw);
		const value = Number.isFinite(numeric) ? numeric : 0;
		defaultsForm = {
			...defaultsForm,
			[key]: key === 'defaultCrewSize' ? Math.max(1, Math.round(value)) : Math.max(0, value)
		};
	}

	function resetChanges() {
		defaultsForm = structuredClone(savedDefaults);
	}

	function saveDefaults() {
		savedDefaults = structuredClone(defaultsForm);
	}

	$effect(() => {
		if (form?.defaultsSaved && form.estimateDefaults) {
			savedDefaults = structuredClone(form.estimateDefaults);
			defaultsForm = structuredClone(form.estimateDefaults);
		}
	});
</script>

<AdminWorkspace
	kicker="Admin"
	title="Admin"
	description="Manage defaults and public-site controls from one admin utility area."
	contextLabel="Admin"
>
	{#snippet context()}
		<AdminContextRail active="defaults" />
	{/snippet}

	{#snippet work()}
		<form method="POST" action="?/saveDefaults" class="space-y-5">
			<div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
				<div>
					<p class="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Estimator Settings</p>
					<h2 class="mt-2 text-2xl font-semibold leading-8 text-[var(--text-strong)]">Concrete Estimate Defaults</h2>
					<p class="mt-3 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">
						Manage the default constants that new concrete estimates inherit across the desktop editor and Bob's mobile field flow.
					</p>
				</div>

				<div class="flex flex-col gap-3 sm:flex-row">
					<button
						type="button"
						class="min-h-12 rounded-lg bg-white px-5 text-sm font-semibold text-[var(--text-strong)] shadow-[var(--shell-shadow)] transition disabled:opacity-50"
						disabled={!hasChanges}
						onclick={resetChanges}
					>
						Reset changes
					</button>
					<button
						type="submit"
						class="min-h-12 rounded-lg bg-[var(--accent-text)] px-5 text-sm font-semibold text-white shadow-sm transition disabled:opacity-50"
						disabled={!hasChanges}
					>
						Save defaults
					</button>
				</div>
			</div>
			{#if form?.message}
				<p class="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{form.message}</p>
			{/if}

			<div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_18rem]">
				<section class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
					<div class="flex items-start gap-4">
						<div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-2xl">🏢</div>
						<div>
							<h3 class="text-lg font-semibold text-[var(--text-strong)]">Admin-Controlled Defaults</h3>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">
								These values are the starting point for new concrete estimates. Estimators can still adjust them on individual jobs.
							</p>
						</div>
					</div>
				</section>

				<aside class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
					<p class="text-[0.68rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">At a glance</p>
					<div class="mt-4 space-y-3 text-sm">
						{#each metrics as metric}
							<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2">
								<p class="text-[0.68rem] uppercase tracking-[0.18em] text-[var(--muted)]">{metric.label}</p>
								<p class="mt-1 font-medium text-[var(--text-strong)]">{metric.value}</p>
							</div>
						{/each}
					</div>
				</aside>
			</div>

			<div class="space-y-4">
				{#each sections as section}
					<section class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<div class="flex items-start gap-4">
							<div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-2xl">
								{section.icon}
							</div>
							<div>
								<h3 class="text-lg font-semibold text-[var(--text-strong)]">{section.title}</h3>
								<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{section.subtitle}</p>
							</div>
						</div>

						<div class="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
							{#each section.fields as field}
								<label class="block">
									<span class="mb-2 block text-sm font-medium text-[var(--text-base)]">{field.label}</span>
									<span class="relative block">
										{#if field.prefix}
											<span class="pointer-events-none absolute inset-y-0 left-4 flex items-center text-sm font-medium text-[var(--text-muted)]">
												{field.prefix}
											</span>
										{/if}
										<input
											class={`min-h-12 w-full rounded-lg border border-[var(--shell-border)] bg-white px-4 text-sm text-[var(--text-base)] shadow-sm outline-none transition focus:border-[var(--accent-border)] focus:ring-2 focus:ring-[var(--accent-border)]/30 ${field.prefix ? 'pl-8' : ''} ${field.suffix ? 'pr-14' : ''}`}
											type="number"
											name={field.key}
											min={field.min ?? 0}
											step={field.step ?? '0.01'}
											value={displayValue(field.key)}
											oninput={(event) => updateValue(field.key, event.currentTarget.value)}
										/>
										{#if field.suffix}
											<span class="pointer-events-none absolute inset-y-0 right-4 flex items-center text-sm font-medium text-[var(--text-muted)]">
												{field.suffix}
											</span>
										{/if}
									</span>
									{#if field.help}
										<span class="mt-2 block text-xs leading-5 text-[var(--text-muted)]">{field.help}</span>
									{/if}
								</label>
							{/each}
						</div>
					</section>
				{/each}
			</div>
		</form>
	{/snippet}
</AdminWorkspace>
