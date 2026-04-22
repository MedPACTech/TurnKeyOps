<script lang="ts">
  import { onMount } from 'svelte';
  import { estimateDefaultsApi } from '$api/estimate-defaults';
  import type { EstimateDefaultsDto } from '$api/types';
  import EstimateDefaultsSection from '$lib/components/field/EstimateDefaultsSection.svelte';
  import { toast } from '$stores/toast';

  type DefaultsField = {
    key: keyof EstimateDefaultsDto;
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

  let loading = true;
  let saving = false;
  let loadedDefaults: EstimateDefaultsDto | null = null;
  let form: EstimateDefaultsDto | null = null;

  $: hasChanges = loadedDefaults && form ? JSON.stringify(loadedDefaults) !== JSON.stringify(form) : false;

  async function loadDefaults() {
    loading = true;
    try {
      const result = await estimateDefaultsApi.get();
      loadedDefaults = result;
      form = structuredClone(result);
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to load estimate defaults.');
    } finally {
      loading = false;
    }
  }

  function updateValue(key: keyof EstimateDefaultsDto, value: number) {
    if (!form) return;
    form = {
      ...form,
      [key]: key === 'defaultCrewSize' ? Math.max(1, Math.round(value)) : Math.max(0, value)
    };
  }

  function resetChanges() {
    if (!loadedDefaults) return;
    form = structuredClone(loadedDefaults);
  }

  async function saveDefaults() {
    if (!form) return;
    saving = true;
    try {
      const saved = await estimateDefaultsApi.update(form);
      loadedDefaults = saved;
      form = structuredClone(saved);
      toast.success('Estimate defaults saved.');
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to save estimate defaults.');
    } finally {
      saving = false;
    }
  }

  onMount(loadDefaults);
</script>

<div class="mx-auto max-w-7xl">
  <div class="page-header mb-6 flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
    <div>
      <p class="text-xs font-semibold uppercase tracking-[0.22em] text-brand-600">Estimator Settings</p>
      <h1 class="page-title mt-2">Concrete Estimate Defaults</h1>
      <p class="mt-3 max-w-3xl text-sm leading-6 text-ink-600">
        Manage the default constants that new concrete estimates inherit across the desktop editor and Bob’s mobile field flow.
      </p>
    </div>

    <div class="flex flex-col gap-3 sm:flex-row">
      <button class="btn-secondary min-h-[3rem] px-5" on:click={resetChanges} disabled={!hasChanges || saving || loading}>
        Reset changes
      </button>
      <button class="btn-primary min-h-[3rem] px-5" on:click={saveDefaults} disabled={!hasChanges || saving || loading || !form}>
        {saving ? 'Saving...' : 'Save defaults'}
      </button>
    </div>
  </div>

  {#if loading}
    <div class="grid gap-4">
      {#each Array(4) as _}
        <div class="card animate-pulse space-y-4">
          <div class="flex items-center gap-3">
            <div class="h-11 w-11 rounded-2xl bg-ink-100"></div>
            <div class="space-y-2">
              <div class="h-5 w-48 rounded bg-ink-100"></div>
              <div class="h-4 w-80 rounded bg-ink-100"></div>
            </div>
          </div>
          <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {#each Array(6) as __}
              <div class="space-y-2">
                <div class="h-4 w-28 rounded bg-ink-100"></div>
                <div class="h-12 rounded-2xl bg-ink-100"></div>
              </div>
            {/each}
          </div>
        </div>
      {/each}
    </div>
  {:else if form}
    <div class="mb-5 grid gap-4 lg:grid-cols-[minmax(0,1fr)_18rem]">
      <div class="card border border-brand-100/70">
        <div class="flex items-start gap-4">
          <div class="flex h-12 w-12 items-center justify-center rounded-2xl bg-brand-100 text-2xl">🏢</div>
          <div>
            <h2 class="text-lg font-semibold text-ink-950">Admin-Controlled Defaults</h2>
            <p class="mt-2 text-sm leading-6 text-ink-600">
              These values are the starting point for new concrete estimates. Estimators can still adjust them on individual jobs.
            </p>
          </div>
        </div>
      </div>

      <aside class="card border border-brand-100/70">
        <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">At a glance</p>
        <div class="mt-4 space-y-3 text-sm">
          <div class="rounded-xl bg-ink-50 px-3 py-2">
            <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Sections</p>
            <p class="mt-1 font-medium text-ink-900">{sections.length} groups</p>
          </div>
          <div class="rounded-xl bg-ink-50 px-3 py-2">
            <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Crew Size</p>
            <p class="mt-1 font-medium text-ink-900">{form.defaultCrewSize} people</p>
          </div>
          <div class="rounded-xl bg-ink-50 px-3 py-2">
            <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Concrete Cost</p>
            <p class="mt-1 font-medium text-ink-900">${form.concreteCostPerYard} / yard</p>
          </div>
          <div class="rounded-xl bg-ink-50 px-3 py-2">
            <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Status</p>
            <p class="mt-1 font-medium text-ink-900">{hasChanges ? 'Unsaved changes' : 'All changes saved'}</p>
          </div>
        </div>
      </aside>
    </div>

    <div class="space-y-4">
      {#each sections as section}
        <EstimateDefaultsSection
          title={section.title}
          subtitle={section.subtitle}
          icon={section.icon}
          fields={section.fields}
          values={form}
          onValueChange={updateValue}
        />
      {/each}
    </div>
  {/if}
</div>
