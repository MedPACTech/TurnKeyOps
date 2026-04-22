<script lang="ts">
  import { browser } from '$app/environment';
  import { goto } from '$app/navigation';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
  import AppointmentContextCard from '$lib/components/field/AppointmentContextCard.svelte';
  import EstimateReviewSectionCard from '$lib/components/field/EstimateReviewSectionCard.svelte';
  import MobileActionBar from '$lib/components/field/MobileActionBar.svelte';
  import { estimateWorkflowApi } from '$lib/api/estimate-workflow';
  import type {
    CreateEstimateFromAppointmentRequestDto,
    EstimateCalculationSnapshotDto,
    EstimateDto,
    StructuredEstimateInputDto,
    UpdateEstimateStructuredRequestDto
  } from '$lib/api/types';
  import { fieldEstimate } from '$lib/stores/field-estimate';
  import { fieldIntake } from '$lib/stores/field-intake';
  import { formatCurrency } from '$lib/utils/format';
  import { toast } from '$stores/toast';

  $: context = $fieldEstimate;
  $: intake = $fieldIntake;
  $: values = intake.values;

  let snapshot: EstimateCalculationSnapshotDto | null = null;
  let currentEstimate: EstimateDto | null = null;
  let calculating = false;
  let savingDraft = false;
  let submitting = false;
  let loadingSavedEstimate = false;

  $: hasRequiredFields = Boolean(
    values.projectType &&
      values.lengthFt &&
      values.widthFt &&
      values.depthIn &&
      values.pourCount &&
      values.reinforcementType &&
      values.finishType
  );

  $: if (browser && (!context || intake.messages.length === 0)) {
    goto('/field/start');
  }

  $: if (context?.details.estimateId && !currentEstimate && !loadingSavedEstimate) {
    loadExistingEstimate(context.details.estimateId);
  }

  function yesNo(value: boolean | undefined) {
    if (value === undefined) return 'Pending';
    return value ? 'Yes' : 'No';
  }

  function buildStructuredInput(): StructuredEstimateInputDto {
    return {
      projectType: values.projectType,
      lengthFt: values.lengthFt,
      widthFt: values.widthFt,
      depthIn: values.depthIn,
      wastePercent: 10,
      pourCount: values.pourCount,
      demoRequired: values.demoRequired,
      excavationRequired: values.excavationRequired,
      pumpRequired: values.pumpRequired,
      reinforcementType: values.reinforcementType,
      finishType: values.finishType
    };
  }

  function buildCreatePayload(): CreateEstimateFromAppointmentRequestDto {
    if (!context) {
      throw new Error('Missing estimate context');
    }

    return {
      appointmentId: context.details.appointmentId,
      customerName: context.details.customerName,
      customerCompany: context.details.customerCompany,
      projectAddress: context.details.projectAddress,
      estimatorName: context.details.estimatorName,
      projectName: context.details.projectName,
      estimateNumber: context.details.estimateNumber,
      structuredInput: buildStructuredInput()
    };
  }

  function buildUpdatePayload(): UpdateEstimateStructuredRequestDto {
    if (!context) {
      throw new Error('Missing estimate context');
    }

    return {
      appointmentId: context.details.appointmentId,
      customerName: context.details.customerName,
      customerCompany: context.details.customerCompany,
      projectAddress: context.details.projectAddress,
      estimatorName: context.details.estimatorName,
      projectName: context.details.projectName,
      structuredInput: buildStructuredInput()
    };
  }

  function syncEstimateContext(estimate: EstimateDto) {
    if (!context) return;

    fieldEstimate.setDetails({
      ...context.details,
      estimateId: estimate.id,
      estimateNumber: estimate.estimateNumber ?? context.details.estimateNumber,
      customerName: estimate.customerName ?? context.details.customerName,
      customerCompany: estimate.customerCompany ?? context.details.customerCompany,
      projectAddress: estimate.projectAddress ?? context.details.projectAddress,
      estimatorName: estimate.estimatorName ?? context.details.estimatorName,
      projectName: estimate.projectName ?? context.details.projectName
    });
  }

  async function loadExistingEstimate(id: string) {
    loadingSavedEstimate = true;
    try {
      currentEstimate = await estimateWorkflowApi.get(id);
      snapshot = currentEstimate.calculationSnapshot ?? null;
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to load the saved estimate.');
    } finally {
      loadingSavedEstimate = false;
    }
  }

  async function calculateEstimate() {
    calculating = true;
    try {
      snapshot = await estimateWorkflowApi.calculate(buildStructuredInput());
      toast.success('Calculation snapshot updated.');
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to calculate estimate.');
    } finally {
      calculating = false;
    }
  }

  async function saveDraft() {
    if (!context) return;

    savingDraft = true;
    try {
      const estimate = context.details.estimateId
        ? await estimateWorkflowApi.update(context.details.estimateId, buildUpdatePayload())
        : await estimateWorkflowApi.createDraft(buildCreatePayload());

      currentEstimate = estimate;
      snapshot = estimate.calculationSnapshot ?? snapshot;
      syncEstimateContext(estimate);
      toast.success('Draft estimate saved.');
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to save draft.');
    } finally {
      savingDraft = false;
    }
  }

  async function submitEstimate() {
    if (!hasRequiredFields || !context) return;

    submitting = true;
    try {
      if (!context.details.estimateId) {
        await saveDraft();
      }

      const estimateId = context.details.estimateId ?? currentEstimate?.id;
      if (!estimateId) {
        throw new Error('Draft estimate was not created.');
      }

      const submitted = await estimateWorkflowApi.submit(estimateId);
      currentEstimate = submitted;
      snapshot = submitted.calculationSnapshot ?? snapshot;
      syncEstimateContext(submitted);
      toast.success(`Estimate ${submitted.estimateNumber ?? ''} submitted.`.trim());
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to submit estimate.');
    } finally {
      submitting = false;
    }
  }

  function openEstimator() {
    goto('/app/estimates/new');
  }
</script>

{#if context}
  <div class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.18),transparent_22%),linear-gradient(180deg,#fffdf9_0%,#f4f7fb_100%)]">
    <div class="mx-auto flex min-h-screen w-full max-w-xl flex-col px-4 pb-40 pt-6 sm:px-6">
      <div class="mb-6 flex items-center justify-center">
        <BrandLogo compact className="items-center" />
      </div>

      <div class="card">
        <div class="flex items-start gap-4">
          <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-3xl">
            👷‍♂️
          </div>
          <div>
            <p class="text-[11px] font-semibold uppercase tracking-[0.26em] text-brand-600">Bob</p>
            <h1 class="mt-2 text-2xl font-bold text-ink-950">Review Estimate</h1>
            <p class="mt-3 text-sm leading-6 text-ink-600">
              Review the structured estimate, run pricing, save your draft, and submit when the job is ready.
            </p>
          </div>
        </div>
      </div>

      <div class="mt-4 space-y-4">
        <EstimateReviewSectionCard title="Estimate Context" subtitle="Appointment and customer details" icon="📍">
          <AppointmentContextCard details={context.details} compact />
        </EstimateReviewSectionCard>

        <EstimateReviewSectionCard title="Dimensions" subtitle="Core slab details" icon="📐">
          <div class="grid grid-cols-2 gap-2 text-sm">
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Job Type</p>
              <p class="mt-1 font-medium text-ink-900">{values.projectType ?? 'Pending'}</p>
            </div>
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Pours</p>
              <p class="mt-1 font-medium text-ink-900">{values.pourCount ?? 'Pending'}</p>
            </div>
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Length</p>
              <p class="mt-1 font-medium text-ink-900">{values.lengthFt !== undefined ? `${values.lengthFt} ft` : 'Pending'}</p>
            </div>
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Width</p>
              <p class="mt-1 font-medium text-ink-900">{values.widthFt !== undefined ? `${values.widthFt} ft` : 'Pending'}</p>
            </div>
            <div class="rounded-xl bg-ink-50 px-3 py-2 col-span-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Depth</p>
              <p class="mt-1 font-medium text-ink-900">{values.depthIn !== undefined ? `${values.depthIn} in` : 'Pending'}</p>
            </div>
          </div>
        </EstimateReviewSectionCard>

        <EstimateReviewSectionCard title="Site Conditions" subtitle="Access and prep items Bob captured" icon="🚧">
          <div class="grid grid-cols-1 gap-2 text-sm">
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Demo Required</p>
              <p class="mt-1 font-medium text-ink-900">{yesNo(values.demoRequired)}</p>
            </div>
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Excavation Required</p>
              <p class="mt-1 font-medium text-ink-900">{yesNo(values.excavationRequired)}</p>
            </div>
            <div class="rounded-xl bg-ink-50 px-3 py-2">
              <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Pump Required</p>
              <p class="mt-1 font-medium text-ink-900">{yesNo(values.pumpRequired)}</p>
            </div>
          </div>
        </EstimateReviewSectionCard>

        <EstimateReviewSectionCard title="Reinforcement" subtitle="Structural support selections" icon="🧱">
          <div class="rounded-xl bg-ink-50 px-3 py-2 text-sm">
            <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Reinforcement Type</p>
            <p class="mt-1 font-medium text-ink-900">{values.reinforcementType ?? 'Pending'}</p>
          </div>
        </EstimateReviewSectionCard>

        <EstimateReviewSectionCard title="Finish" subtitle="Surface finish captured by Bob" icon="✨">
          <div class="rounded-xl bg-ink-50 px-3 py-2 text-sm">
            <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Finish Type</p>
            <p class="mt-1 font-medium text-ink-900">{values.finishType ?? 'Pending'}</p>
          </div>
        </EstimateReviewSectionCard>

        <EstimateReviewSectionCard title="Calculation Snapshot" subtitle="Server-side pricing based on structured estimate fields" icon="🧮">
          {#if snapshot}
            <div class="grid grid-cols-2 gap-2 text-sm">
              <div class="rounded-xl bg-brand-50 px-3 py-2">
                <p class="text-[11px] uppercase tracking-[0.18em] text-brand-700">Square Feet</p>
                <p class="mt-1 font-semibold text-ink-950">{snapshot.squareFeet} sqft</p>
              </div>
              <div class="rounded-xl bg-brand-50 px-3 py-2">
                <p class="text-[11px] uppercase tracking-[0.18em] text-brand-700">Cubic Yards</p>
                <p class="mt-1 font-semibold text-ink-950">{snapshot.cubicYardsWithWaste} CY</p>
              </div>
              <div class="rounded-xl bg-ink-50 px-3 py-2">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Direct Cost</p>
                <p class="mt-1 font-medium text-ink-900">{formatCurrency(snapshot.directCost)}</p>
              </div>
              <div class="rounded-xl bg-ink-50 px-3 py-2">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Labor</p>
                <p class="mt-1 font-medium text-ink-900">{formatCurrency(snapshot.laborSubtotal)}</p>
              </div>
              <div class="rounded-xl bg-ink-50 px-3 py-2">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Materials Delivered</p>
                <p class="mt-1 font-medium text-ink-900">{formatCurrency(snapshot.deliveredConcreteCost)}</p>
              </div>
              <div class="rounded-xl bg-ink-50 px-3 py-2">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Price / Sq Ft</p>
                <p class="mt-1 font-medium text-ink-900">{formatCurrency(snapshot.pricePerSquareFoot)}</p>
              </div>
              <div class="col-span-2 rounded-xl bg-green-50 px-3 py-3">
                <p class="text-[11px] uppercase tracking-[0.18em] text-green-700">Final Estimated Price</p>
                <p class="mt-1 text-lg font-semibold text-green-900">{formatCurrency(snapshot.finalEstimatedPrice)}</p>
              </div>
            </div>
          {:else}
            <div class="rounded-2xl border border-dashed border-ink-200 bg-white px-4 py-4 text-sm text-ink-500">
              Run pricing to generate the normalized calculation snapshot for this estimate.
            </div>
          {/if}
        </EstimateReviewSectionCard>
      </div>

      <MobileActionBar>
        <div class="space-y-3">
          <button class="btn-secondary w-full min-h-[3.25rem] rounded-2xl text-base" on:click={() => goto('/field/intake')}>
            Edit details
          </button>
          <button class="btn-secondary w-full min-h-[3.25rem] rounded-2xl text-base" on:click={openEstimator}>
            Open full estimator form
          </button>
          <button class="btn-secondary w-full min-h-[3.25rem] rounded-2xl text-base" disabled={savingDraft} on:click={saveDraft}>
            {savingDraft ? 'Saving draft...' : 'Save draft'}
          </button>
          <button class="btn-primary w-full min-h-[3.25rem] rounded-2xl text-base" disabled={calculating} on:click={calculateEstimate}>
            {calculating ? 'Calculating...' : 'Continue to pricing/calculation'}
          </button>
          <button
            class="btn-primary w-full min-h-[3.25rem] rounded-2xl text-base disabled:cursor-not-allowed disabled:opacity-50"
            disabled={!hasRequiredFields || submitting}
            on:click={submitEstimate}
          >
            {submitting ? 'Submitting...' : 'Submit estimate'}
          </button>
          {#if !hasRequiredFields}
            <p class="px-1 text-sm text-ink-500">
              Fill in the remaining required intake details before submission.
            </p>
          {/if}
          {#if currentEstimate}
            <p class="px-1 text-sm text-ink-500">
              Working estimate: <span class="font-medium text-ink-800">{currentEstimate.estimateNumber ?? currentEstimate.id}</span>
            </p>
          {/if}
        </div>
      </MobileActionBar>
    </div>
  </div>
{/if}
