<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { formatCurrency, formatDate, statusColor } from '$lib/utils/format';
  import { LoadingSpinner, EmptyState } from '$components';
  import type { JobDto } from '$api/types';

  let jobs: JobDto[] = [];
  let loading = true;

  onMount(async () => {
    try {
      const res = await api.paged<JobDto>('/jobs/paged', 50);
      jobs = res.items;
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      loading = false;
    }
  });
</script>

<div>
  <div class="page-header">
    <h1 class="page-title">Jobs</h1>
    <button class="btn-primary">+ New Job</button>
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if jobs.length === 0}
    <EmptyState icon="🏗️" title="No jobs yet" message="Create a job when you win an estimate." />
  {:else}
    <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {#each jobs as job}
        <div class="card hover:shadow-md transition-shadow">
          <div class="flex items-start justify-between mb-2">
            <h3 class="font-semibold text-gray-900 truncate">{job.name}</h3>
            <span class={statusColor(job.status)}>{job.status}</span>
          </div>
          <p class="text-sm text-gray-500 mb-3">{job.customerName ?? '—'}</p>
          <div class="flex items-center justify-between text-sm">
            <span class="badge {job.tradeType === 'Concrete' ? 'bg-gray-200 text-gray-700' : 'bg-amber-100 text-amber-700'}">
              {job.tradeType}
            </span>
            <span class="font-medium">{formatCurrency(job.estimatedTotal)}</span>
          </div>
          {#if job.scheduledStart}
            <p class="text-xs text-gray-400 mt-2">📅 {formatDate(job.scheduledStart)}</p>
          {/if}
        </div>
      {/each}
    </div>
  {/if}
</div>
