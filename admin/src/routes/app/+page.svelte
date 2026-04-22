<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { formatCurrency, formatDateTime } from '$lib/utils/format';
  import { StatCard, LoadingSpinner, WeatherBadge } from '$components';
  import type { DashboardDto } from '$api/types';

  let dashboard: DashboardDto | null = null;
  let loading = true;

  onMount(async () => {
    try {
      dashboard = await api.get<DashboardDto>('/dashboard');
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      loading = false;
    }
  });
</script>

<div>
  <div class="page-header">
    <h1 class="page-title">Dashboard</h1>
    <a href="/app/estimates" class="btn-primary">+ New Estimate</a>
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if dashboard}
    <!-- Stats Grid -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      <StatCard value={dashboard.activeJobs} label="Active Jobs" icon="🏗️" href="/app/jobs" />
      <StatCard value={dashboard.pendingEstimates} label="Pending Estimates" icon="📝" href="/app/estimates" />
      <StatCard value={dashboard.overdueInvoices} label="Overdue" icon="⚠️"
        trend={dashboard.overdueInvoices > 0 ? 'down' : 'neutral'} href="/app/invoices" />
      <StatCard value={formatCurrency(dashboard.revenueThisMonth)} label="Revenue (Month)" icon="💰" trend="up" />
    </div>

    <!-- Outstanding Balance -->
    {#if dashboard.outstandingBalance > 0}
      <div class="card bg-yellow-50 border-yellow-200 mb-6">
        <div class="flex items-center gap-3">
          <span class="text-2xl">💵</span>
          <div>
            <p class="font-semibold text-yellow-900">{formatCurrency(dashboard.outstandingBalance)} outstanding</p>
            <p class="text-sm text-yellow-700">Across all unpaid invoices</p>
          </div>
          <a href="/app/invoices" class="ml-auto btn-secondary text-xs">View Invoices →</a>
        </div>
      </div>
    {/if}

    <!-- Upcoming Events -->
    <div class="card">
      <h2 class="font-semibold text-gray-900 mb-4">📅 This Week</h2>
      {#if dashboard.upcomingEvents.length === 0}
        <p class="text-sm text-gray-500">No events this week. <a href="/app/calendar" class="text-brand-600 hover:underline">Add one →</a></p>
      {:else}
        <div class="space-y-3">
          {#each dashboard.upcomingEvents as event}
            <div class="flex items-center gap-3 py-2 border-b border-gray-100 last:border-0">
              <div class="w-2 h-2 rounded-full" style="background:{event.color ?? '#5b72f2'}"></div>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium truncate">{event.title}</p>
                <p class="text-xs text-gray-500">{formatDateTime(event.startUtc)}</p>
              </div>
              {#if event.jobSiteName}
                <span class="text-xs text-gray-400 hidden sm:block">{event.jobSiteName}</span>
              {/if}
              <WeatherBadge weather={event.weather} compact />
            </div>
          {/each}
        </div>
      {/if}
    </div>
  {/if}
</div>
