<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { LoadingSpinner, EmptyState, WeatherBadge, Modal } from '$components';
  import { formatDateTime } from '$lib/utils/format';
  import type { CalendarEventDto } from '$api/types';

  let events: CalendarEventDto[] = [];
  let loading = true;
  let viewMode: 'month' | 'week' | 'list' = 'week';
  let selectedEvent: CalendarEventDto | null = null;
  let showEventModal = false;

  // Current date range
  let viewStart = startOfWeek(new Date());
  let viewEnd = endOfWeek(new Date());

  function startOfWeek(d: Date): Date {
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1);
    return new Date(d.getFullYear(), d.getMonth(), diff);
  }
  function endOfWeek(d: Date): Date {
    const s = startOfWeek(d);
    return new Date(s.getFullYear(), s.getMonth(), s.getDate() + 7);
  }
  function startOfMonth(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), 1);
  }
  function endOfMonth(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth() + 1, 1);
  }

  async function loadEvents() {
    loading = true;
    try {
      events = await api.get<CalendarEventDto[]>('/calendar', {
        start: viewStart.toISOString(),
        end: viewEnd.toISOString()
      });
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      loading = false;
    }
  }

  function navigate(direction: -1 | 1) {
    if (viewMode === 'week') {
      viewStart = new Date(viewStart.getTime() + direction * 7 * 86400000);
      viewEnd = new Date(viewEnd.getTime() + direction * 7 * 86400000);
    } else {
      const m = viewStart.getMonth() + direction;
      viewStart = new Date(viewStart.getFullYear(), m, 1);
      viewEnd = endOfMonth(viewStart);
    }
    loadEvents();
  }

  function openEvent(event: CalendarEventDto) {
    selectedEvent = event;
    showEventModal = true;
  }

  // Week days for header
  function getWeekDays(): Date[] {
    const days: Date[] = [];
    for (let i = 0; i < 7; i++) {
      days.push(new Date(viewStart.getTime() + i * 86400000));
    }
    return days;
  }

  onMount(loadEvents);
</script>

<div>
  <!-- Header -->
  <div class="page-header flex-wrap gap-3">
    <h1 class="page-title">Calendar</h1>
    <div class="flex items-center gap-2">
      <div class="flex bg-gray-100 rounded-lg p-0.5">
        <button class="px-3 py-1.5 text-xs font-medium rounded-md transition-colors
          {viewMode === 'week' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700'}"
          on:click={() => { viewMode = 'week'; viewStart = startOfWeek(viewStart); viewEnd = endOfWeek(viewStart); loadEvents(); }}>
          Week
        </button>
        <button class="px-3 py-1.5 text-xs font-medium rounded-md transition-colors
          {viewMode === 'month' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700'}"
          on:click={() => { viewMode = 'month'; viewStart = startOfMonth(viewStart); viewEnd = endOfMonth(viewStart); loadEvents(); }}>
          Month
        </button>
        <button class="px-3 py-1.5 text-xs font-medium rounded-md transition-colors
          {viewMode === 'list' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700'}"
          on:click={() => { viewMode = 'list'; }}>
          List
        </button>
      </div>
      <button class="btn-icon" on:click={() => navigate(-1)}>←</button>
      <span class="text-sm font-medium min-w-[140px] text-center">
        {viewStart.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} –
        {new Date(viewEnd.getTime() - 86400000).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
      </span>
      <button class="btn-icon" on:click={() => navigate(1)}>→</button>
    </div>
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if viewMode === 'list' || viewMode === 'week'}
    <!-- List / Week view -->
    <div class="card">
      {#if events.length === 0}
        <EmptyState icon="📅" title="No events" message="Schedule a job or add a reminder to get started." />
      {:else}
        <div class="divide-y divide-gray-100">
          {#each events as event}
            <button class="w-full flex items-center gap-3 py-3 px-2 hover:bg-gray-50 rounded-lg text-left transition-colors"
              on:click={() => openEvent(event)}>
              <div class="w-1.5 h-10 rounded-full flex-shrink-0" style="background:{event.color ?? '#5b72f2'}"></div>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium truncate">{event.title}</p>
                <p class="text-xs text-gray-500">
                  {formatDateTime(event.startUtc)}
                  {#if event.jobSiteName} · {event.jobSiteName}{/if}
                </p>
              </div>
              <WeatherBadge weather={event.weather} />
            </button>
          {/each}
        </div>
      {/if}
    </div>
  {:else}
    <!-- Month grid (simplified) -->
    <div class="card">
      <div class="grid grid-cols-7 gap-px bg-gray-200 rounded-lg overflow-hidden">
        {#each ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'] as day}
          <div class="bg-gray-50 py-2 text-center text-xs font-medium text-gray-500">{day}</div>
        {/each}
        <!-- Simplified month cells — full implementation would calculate day offsets -->
        {#each getWeekDays() as day}
          <div class="bg-white min-h-[60px] p-1">
            <span class="text-xs text-gray-400">{day.getDate()}</span>
          </div>
        {/each}
      </div>
    </div>
  {/if}
</div>

<!-- Event Detail Modal -->
<Modal bind:open={showEventModal} title={selectedEvent?.title ?? 'Event'}>
  {#if selectedEvent}
    <div class="space-y-3">
      <div>
        <span class="label">When</span>
        <p class="text-sm">{formatDateTime(selectedEvent.startUtc)} — {formatDateTime(selectedEvent.endUtc)}</p>
      </div>
      {#if selectedEvent.jobName}
        <div>
          <span class="label">Job</span>
          <p class="text-sm">{selectedEvent.jobName}</p>
        </div>
      {/if}
      {#if selectedEvent.jobSiteName}
        <div>
          <span class="label">Site</span>
          <p class="text-sm">{selectedEvent.jobSiteName}</p>
        </div>
      {/if}
      {#if selectedEvent.weather}
        <div>
          <span class="label">Weather</span>
          <div class="flex items-center gap-2 mt-1">
            <WeatherBadge weather={selectedEvent.weather} />
            <span class="text-sm text-gray-600">{selectedEvent.weather.summary}</span>
          </div>
        </div>
      {/if}
      {#if selectedEvent.description}
        <div>
          <span class="label">Notes</span>
          <p class="text-sm text-gray-600">{selectedEvent.description}</p>
        </div>
      {/if}
    </div>
  {/if}
</Modal>
