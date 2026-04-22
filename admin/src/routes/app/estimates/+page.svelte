<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { formatCurrency, formatDate, statusColor } from '$lib/utils/format';
  import { LoadingSpinner, EmptyState } from '$components';
  import type { EstimateDto } from '$api/types';

  let estimates: EstimateDto[] = [];
  let loading = true;

  async function load() {
    loading = true;
    try {
      const res = await api.paged<EstimateDto>('/estimates/paged', 50);
      estimates = res.items;
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      loading = false;
    }
  }

  onMount(load);
</script>

<div>
  <div class="page-header">
    <h1 class="page-title">Estimates</h1>
    <a href="/app/estimates/new" class="btn-primary">+ New Estimate</a>
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if estimates.length === 0}
    <EmptyState
      icon="📝"
      title="No estimates yet"
      message="Create your first estimate using our concrete or framing templates."
      actionLabel="Create Estimate"
      actionHref="/app/estimates/new"
    />
  {:else}
    <div class="card p-0 table-wrapper">
      <table class="table">
        <thead><tr>
          <th>Number</th>
          <th>Customer</th>
          <th class="hidden sm:table-cell">Trade</th>
          <th>Status</th>
          <th class="text-right">Total</th>
          <th class="hidden md:table-cell">Date</th>
        </tr></thead>
        <tbody>
          {#each estimates as est}
            <tr on:click={() => window.location.href = `/app/estimates/${est.id}`}>
              <td class="font-medium">{est.estimateNumber ?? '—'}</td>
              <td>{est.customerName ?? '—'}</td>
              <td class="hidden sm:table-cell">
                <span class="badge {est.tradeType === 'Concrete' ? 'bg-gray-200 text-gray-700' : 'bg-amber-100 text-amber-700'}">
                  {est.tradeType}
                </span>
              </td>
              <td><span class={statusColor(est.status)}>{est.status}</span></td>
              <td class="text-right font-medium">{formatCurrency(est.total)}</td>
              <td class="hidden md:table-cell">{formatDate(est.dateCreated)}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
