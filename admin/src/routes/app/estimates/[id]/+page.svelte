<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { estimateWorkflowApi } from '$lib/api/estimate-workflow';
  import type { EstimateDto } from '$lib/api/types';
  import { formatCurrency, formatDate, statusColor } from '$lib/utils/format';
  import { LoadingSpinner } from '$components';
  import { toast } from '$stores/toast';

  let estimate: EstimateDto | null = null;
  let loading = true;
  let actionLoading: 'review' | 'award' | 'reject' | 'revise' | 'convert' | null = null;
  let convertedJobId: string | null = null;
  let estimateId = '';

  $: estimateId = $page.params.id ?? '';
  $: canStartReview = estimate ? ['Submitted', 'Revised'].includes(estimate.status) : false;
  $: canAward = estimate ? ['Submitted', 'UnderReview', 'Revised'].includes(estimate.status) : false;
  $: canReject = estimate ? ['Submitted', 'UnderReview', 'Revised', 'Awarded'].includes(estimate.status) : false;
  $: canRevise = estimate ? ['Submitted', 'UnderReview', 'Rejected'].includes(estimate.status) : false;
  $: canConvertToJob = estimate?.status === 'Awarded';

  async function loadEstimate() {
    if (!estimateId) {
      loading = false;
      toast.error('Missing estimate id.');
      return;
    }

    loading = true;
    try {
      estimate = await estimateWorkflowApi.get(estimateId);
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to load estimate.');
    } finally {
      loading = false;
    }
  }

  async function runAction(action: 'review' | 'award' | 'reject' | 'revise' | 'convert') {
    if (!estimate) return;

    actionLoading = action;
    try {
      if (action === 'review') {
        estimate = await estimateWorkflowApi.startReview(estimate.id);
        toast.success('Estimate moved to under review.');
      } else if (action === 'award') {
        estimate = await estimateWorkflowApi.award(estimate.id);
        toast.success('Estimate marked as awarded.');
      } else if (action === 'reject') {
        estimate = await estimateWorkflowApi.reject(estimate.id);
        toast.success('Estimate marked as rejected.');
      } else if (action === 'revise') {
        estimate = await estimateWorkflowApi.revise(estimate.id);
        toast.success('Estimate moved to revised.');
      } else {
        const job = await estimateWorkflowApi.convertToJob(estimate.id);
        convertedJobId = job.id;
        estimate = await estimateWorkflowApi.get(estimate.id);
        toast.success(`Converted to job ${job.name ?? job.id}.`);
      }
    } catch (err: any) {
      toast.error(err.message ?? 'Workflow action failed.');
    } finally {
      actionLoading = null;
    }
  }

  onMount(loadEstimate);
</script>

<div class="mx-auto max-w-6xl">
  <div class="page-header mb-6">
    <div>
      <p class="text-xs font-semibold uppercase tracking-[0.22em] text-brand-600">Estimate Workflow</p>
      <h1 class="page-title mt-2">{estimate?.estimateNumber ?? 'Estimate Detail'}</h1>
      <p class="mt-3 text-sm leading-6 text-ink-600">
        Review the estimate snapshot and move it through award, reject, revise, or conversion to job.
      </p>
    </div>
    <div class="flex flex-wrap gap-3">
      {#if estimate}
        <a class="btn-primary" href={`/app/site-visit-prep?estimateId=${estimate.id}`}>Open site visit prep</a>
      {/if}
      <button class="btn-secondary" on:click={() => goto('/app/estimates')}>Back to estimates</button>
    </div>
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if estimate}
    <div class="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
      <div class="space-y-6">
        <div class="card">
          <div class="flex items-start justify-between gap-4">
            <div>
              <div class="flex items-center gap-3">
                <h2 class="text-xl font-semibold text-ink-950">{estimate.customerName ?? 'Unassigned customer'}</h2>
                <span class={statusColor(estimate.status)}>{estimate.status}</span>
              </div>
              {#if estimate.projectName}
                <p class="mt-2 text-sm text-ink-600">{estimate.projectName}</p>
              {/if}
              {#if estimate.projectAddress}
                <p class="mt-1 text-sm text-ink-500">{estimate.projectAddress}</p>
              {/if}
            </div>
            <div class="text-right text-sm text-ink-500">
              <p>Created {formatDate(estimate.dateCreated)}</p>
              {#if estimate.convertedToJobDate}
                <p class="mt-1">Converted {formatDate(estimate.convertedToJobDate)}</p>
              {/if}
            </div>
          </div>
        </div>

        <div class="card">
          <h2 class="text-lg font-semibold text-ink-950">Structured Estimate</h2>
          <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3 text-sm">
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Project Type</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.projectType ?? '—'}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Length</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.lengthFt ?? '—'}{estimate.structuredInput?.lengthFt ? ' ft' : ''}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Width</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.widthFt ?? '—'}{estimate.structuredInput?.widthFt ? ' ft' : ''}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Depth</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.depthIn ?? '—'}{estimate.structuredInput?.depthIn ? ' in' : ''}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Pours</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.pourCount ?? '—'}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Reinforcement</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.reinforcementType ?? '—'}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Finish</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.finishType ?? '—'}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Demo</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.demoRequired ? 'Yes' : 'No'}</p></div>
            <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Excavation</p><p class="mt-1 font-medium text-ink-900">{estimate.structuredInput?.excavationRequired ? 'Yes' : 'No'}</p></div>
          </div>
        </div>

        <div class="card">
          <h2 class="text-lg font-semibold text-ink-950">Calculation Snapshot</h2>
          {#if estimate.calculationSnapshot}
            <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3 text-sm">
              <div class="rounded-xl bg-brand-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-brand-700">Final Price</p><p class="mt-1 text-lg font-semibold text-ink-950">{formatCurrency(estimate.calculationSnapshot.finalEstimatedPrice)}</p></div>
              <div class="rounded-xl bg-brand-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-brand-700">Price / Sq Ft</p><p class="mt-1 font-semibold text-ink-950">{formatCurrency(estimate.calculationSnapshot.pricePerSquareFoot)}</p></div>
              <div class="rounded-xl bg-brand-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-brand-700">Price / Yard</p><p class="mt-1 font-semibold text-ink-950">{formatCurrency(estimate.calculationSnapshot.pricePerYard)}</p></div>
              <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Direct Cost</p><p class="mt-1 font-medium text-ink-900">{formatCurrency(estimate.calculationSnapshot.directCost)}</p></div>
              <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Labor</p><p class="mt-1 font-medium text-ink-900">{formatCurrency(estimate.calculationSnapshot.laborSubtotal)}</p></div>
              <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Equipment</p><p class="mt-1 font-medium text-ink-900">{formatCurrency(estimate.calculationSnapshot.equipmentSubtotal)}</p></div>
              <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Concrete Delivered</p><p class="mt-1 font-medium text-ink-900">{formatCurrency(estimate.calculationSnapshot.deliveredConcreteCost)}</p></div>
              <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Square Feet</p><p class="mt-1 font-medium text-ink-900">{estimate.calculationSnapshot.squareFeet}</p></div>
              <div class="rounded-xl bg-ink-50 px-3 py-2"><p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">Cubic Yards</p><p class="mt-1 font-medium text-ink-900">{estimate.calculationSnapshot.cubicYardsWithWaste}</p></div>
            </div>
          {:else}
            <p class="mt-4 text-sm text-ink-500">No calculation snapshot is available for this estimate yet.</p>
          {/if}
        </div>
      </div>

      <aside class="space-y-6">
        <div class="card">
          <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Workflow Actions</p>
          <div class="mt-4 space-y-3">
            <button class="btn-secondary w-full" disabled={!canStartReview || actionLoading !== null} on:click={() => runAction('review')}>
              {actionLoading === 'review' ? 'Updating...' : 'Move to Review'}
            </button>
            <button class="btn-primary w-full" disabled={!canAward || actionLoading !== null} on:click={() => runAction('award')}>
              {actionLoading === 'award' ? 'Updating...' : 'Mark Awarded'}
            </button>
            <button class="btn-secondary w-full" disabled={!canReject || actionLoading !== null} on:click={() => runAction('reject')}>
              {actionLoading === 'reject' ? 'Updating...' : 'Mark Rejected'}
            </button>
            <button class="btn-secondary w-full" disabled={!canRevise || actionLoading !== null} on:click={() => runAction('revise')}>
              {actionLoading === 'revise' ? 'Updating...' : 'Revise'}
            </button>
            <button class="btn-primary w-full" disabled={!canConvertToJob || actionLoading !== null} on:click={() => runAction('convert')}>
              {actionLoading === 'convert' ? 'Converting...' : 'Convert to Job'}
            </button>
          </div>
          {#if convertedJobId || estimate.convertedJobId}
            <div class="mt-4 rounded-xl border border-green-200 bg-green-50 px-3 py-3 text-sm text-green-800">
              Job created: <a class="font-medium underline" href="/app/jobs">{convertedJobId ?? estimate.convertedJobId}</a>
            </div>
          {/if}
        </div>

        <div class="card">
          <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Timeline</p>
          <div class="mt-4 space-y-3 text-sm">
            <div class="rounded-xl bg-ink-50 px-3 py-2"><span class="text-ink-500">Created</span><p class="mt-1 font-medium text-ink-900">{formatDate(estimate.dateCreated)}</p></div>
            {#if estimate.submittedDate}<div class="rounded-xl bg-ink-50 px-3 py-2"><span class="text-ink-500">Submitted</span><p class="mt-1 font-medium text-ink-900">{formatDate(estimate.submittedDate)}</p></div>{/if}
            {#if estimate.revisedDate}<div class="rounded-xl bg-ink-50 px-3 py-2"><span class="text-ink-500">Revised</span><p class="mt-1 font-medium text-ink-900">{formatDate(estimate.revisedDate)}</p></div>{/if}
            {#if estimate.awardedDate}<div class="rounded-xl bg-ink-50 px-3 py-2"><span class="text-ink-500">Awarded</span><p class="mt-1 font-medium text-ink-900">{formatDate(estimate.awardedDate)}</p></div>{/if}
            {#if estimate.rejectedDate}<div class="rounded-xl bg-ink-50 px-3 py-2"><span class="text-ink-500">Rejected</span><p class="mt-1 font-medium text-ink-900">{formatDate(estimate.rejectedDate)}</p></div>{/if}
            {#if estimate.convertedToJobDate}<div class="rounded-xl bg-ink-50 px-3 py-2"><span class="text-ink-500">Converted to Job</span><p class="mt-1 font-medium text-ink-900">{formatDate(estimate.convertedToJobDate)}</p></div>{/if}
          </div>
        </div>
      </aside>
    </div>
  {/if}
</div>
