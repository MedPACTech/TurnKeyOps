<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$api/client';
  import { EmptyState, LoadingSpinner } from '$components';
  import type { CalendarEventDto, EstimateDto, JobDto } from '$lib/api/types';
  import {
    buildHazards,
    buildPrepStorageKey,
    buildReferenceMaterials,
    buildScopeNotes,
    buildVisitObjectives,
    createDefaultPrepChecklist,
    readPrepChecklist,
    writePrepChecklist,
    type PrepChecklistField,
    type PrepChecklistState
  } from '$lib/site-visit-prep';
  import { formatCurrency, formatDate, formatDateTime, statusColor } from '$lib/utils/format';

  let loading = true;
  let errorMessage = '';
  let event: CalendarEventDto | null = null;
  let job: JobDto | null = null;
  let estimate: EstimateDto | null = null;
  let checklist: PrepChecklistState = createDefaultPrepChecklist();
  let lastLoadedAt = '';
  let loadedSourceKey = '';

  const checklistFields: Array<{ key: PrepChecklistField; label: string; detail: string }> = [
    {
      key: 'summaryConfirmed',
      label: 'Request summary reviewed',
      detail: 'Customer, scope, and visit timing have been checked against the source record.'
    },
    {
      key: 'attachmentsConfirmed',
      label: 'Reference materials reviewed',
      detail: 'Attachments, estimate artifacts, or supporting references have been checked before dispatch.'
    },
    {
      key: 'hazardsConfirmed',
      label: 'Hazards reviewed',
      detail: 'Access, demolition, excavation, pump, weather, or occupancy risks have been called out.'
    },
    {
      key: 'objectivesConfirmed',
      label: 'Visit objectives confirmed',
      detail: 'The field team knows exactly what must be validated during the visit.'
    }
  ];

  $: estimateId = $page.url.searchParams.get('estimateId') ?? '';
  $: eventId = $page.url.searchParams.get('eventId') ?? '';
  $: jobId = $page.url.searchParams.get('jobId') ?? '';
  $: sourceKey = `${estimateId}|${eventId}|${jobId}`;
  $: storageKey = buildPrepStorageKey({
    estimateId: estimate?.id ?? estimateId,
    eventId: event?.id ?? eventId,
    jobId: job?.id ?? jobId
  });

  $: if (sourceKey !== loadedSourceKey && (estimateId || eventId || jobId)) {
    loadedSourceKey = sourceKey;
    loadPrep();
  }

  $: completedCount = checklistFields.filter((field) => checklist[field.key]).length;
  $: visitWindow =
    event?.startUtc || job?.scheduledStart
      ? `${formatDateTime(event?.startUtc ?? job?.scheduledStart)}${event?.endUtc || job?.scheduledEnd ? ` – ${formatDateTime(event?.endUtc ?? job?.scheduledEnd)}` : ''}`
      : '—';
  $: requestSummary = [
    { label: 'Customer', value: estimate?.customerName ?? job?.customerName ?? '—' },
    { label: 'Estimate', value: estimate?.estimateNumber ?? job?.estimateNumber ?? '—' },
    { label: 'Request status', value: estimate?.status ?? '—', badge: Boolean(estimate?.status) },
    { label: 'Trade', value: estimate?.tradeType ?? job?.tradeType ?? '—' },
    { label: 'Quoted total', value: estimate ? formatCurrency(estimate.total) : job ? formatCurrency(job.estimatedTotal) : '—' },
    { label: 'Request created', value: formatDate(estimate?.dateCreated ?? job?.dateCreated) }
  ];
  $: siteDetails = [
    { label: 'Project', value: estimate?.projectName ?? job?.projectName ?? event?.title ?? '—' },
    { label: 'Site / Address', value: estimate?.projectAddress ?? job?.projectAddress ?? event?.jobSiteName ?? '—' },
    { label: 'Visit window', value: visitWindow },
    { label: 'Estimator', value: estimate?.estimatorName ?? '—' },
    { label: 'Job', value: job?.name ?? event?.jobName ?? '—' },
    { label: 'Visit record', value: event?.eventType ?? (job?.scheduledStart ? 'Scheduled job visit' : '—') }
  ];
  $: scopeNotes = buildScopeNotes({ estimate, job, event });
  $: referenceMaterials = buildReferenceMaterials({ estimate, job });
  $: hazards = buildHazards({ estimate, job, event });
  $: visitObjectives = buildVisitObjectives({ estimate, job, event });
  $: sourceRecords = [
    estimate ? { label: 'Estimate record', value: estimate.estimateNumber ?? estimate.id } : null,
    job ? { label: 'Job record', value: job.name ?? job.id } : null,
    event ? { label: 'Visit record', value: event.title } : null
  ].filter(Boolean) as Array<{ label: string; value: string }>;
  $: backHref = eventId ? '/app/calendar' : estimate?.id ? `/app/estimates/${estimate.id}` : job?.id ? '/app/jobs' : '/app';

  async function loadPrep() {
    loading = true;
    errorMessage = '';
    event = null;
    job = null;
    estimate = null;

    try {
      if (eventId) {
        event = await api.get<CalendarEventDto>(`/calendar/${eventId}`);
      }

      const resolvedJobId = jobId || event?.jobId || '';
      if (resolvedJobId) {
        job = await api.get<JobDto>(`/jobs/${resolvedJobId}`);
      }

      const resolvedEstimateId = estimateId || job?.estimateId || '';
      if (resolvedEstimateId) {
        estimate = await api.get<EstimateDto>(`/estimates/${resolvedEstimateId}`);
      }

      if (!event && !job && !estimate) {
        errorMessage = 'No request or visit record was supplied for this prep view.';
      }

      checklist = readPrepChecklist(
        buildPrepStorageKey({
          estimateId: resolvedEstimateId || estimate?.id,
          eventId: event?.id ?? eventId,
          jobId: resolvedJobId || job?.id
        })
      );
      lastLoadedAt = new Date().toISOString();
    } catch (err: any) {
      errorMessage = err?.message ?? 'Unable to load the site visit prep summary.';
    } finally {
      loading = false;
    }
  }

  function toggleChecklist(field: PrepChecklistField) {
    checklist = {
      ...checklist,
      [field]: !checklist[field],
      lastUpdatedAt: new Date().toISOString()
    };
    writePrepChecklist(storageKey, checklist);
  }
</script>

<div class="mx-auto max-w-7xl">
  <div class="page-header mb-6 flex-wrap gap-3">
    <div>
      <p class="text-xs font-semibold uppercase tracking-[0.22em] text-brand-600">Internal Admin · Site Visit Prep</p>
      <h1 class="page-title mt-2">Prep the field team before the visit</h1>
      <p class="mt-3 max-w-3xl text-sm leading-6 text-ink-600">
        This view pulls together the request record, visit record, and job context into one concise prep summary for the field team.
      </p>
    </div>
    <div class="flex flex-wrap gap-3">
      <button class="btn-secondary" on:click={loadPrep} disabled={loading}>
        {loading ? 'Refreshing…' : 'Refresh source data'}
      </button>
      <a class="btn-secondary" href={backHref}>Back</a>
    </div>
  </div>

  {#if !estimateId && !eventId && !jobId}
    <EmptyState
      icon="🧭"
      title="No prep source selected"
      message="Open this prep view from an estimate record or a calendar visit record."
      actionLabel="Go to calendar"
      actionHref="/app/calendar"
    />
  {:else if loading}
    <LoadingSpinner />
  {:else if errorMessage}
    <div class="card border border-red-200 bg-red-50">
      <p class="text-sm font-semibold text-red-700">Could not load prep data</p>
      <p class="mt-2 text-sm text-red-600">{errorMessage}</p>
    </div>
  {:else}
    <div class="grid gap-6 xl:grid-cols-[minmax(0,1fr)_24rem]">
      <div class="space-y-6">
        <div class="card">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <div class="flex flex-wrap items-center gap-3">
                <h2 class="text-xl font-semibold text-ink-950">
                  {estimate?.customerName ?? job?.customerName ?? event?.title ?? 'Site visit prep'}
                </h2>
                {#if estimate?.status}
                  <span class={statusColor(estimate.status)}>{estimate.status}</span>
                {/if}
                {#if job?.status}
                  <span class={statusColor(job.status)}>{job.status}</span>
                {/if}
              </div>
              <p class="mt-2 text-sm text-ink-600">
                {estimate?.projectName ?? job?.projectName ?? event?.jobName ?? 'No project title on record'}
              </p>
              <p class="mt-1 text-sm text-ink-500">
                {estimate?.projectAddress ?? job?.projectAddress ?? event?.jobSiteName ?? 'Site details not captured yet'}
              </p>
            </div>
            <div class="rounded-2xl bg-brand-50 px-4 py-3 text-sm text-brand-900">
              <p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand-700">Visit window</p>
              <p class="mt-1 font-semibold">{visitWindow}</p>
            </div>
          </div>
          <div class="mt-4 grid gap-3 md:grid-cols-3 text-sm">
            {#each requestSummary as item}
              <div class="rounded-xl bg-ink-50 px-3 py-3">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">{item.label}</p>
                {#if item.badge}
                  <span class={`mt-2 inline-flex ${statusColor(item.value)}`}>{item.value}</span>
                {:else}
                  <p class="mt-1 font-medium text-ink-900">{item.value}</p>
                {/if}
              </div>
            {/each}
          </div>
        </div>

        <div class="card">
          <h2 class="text-lg font-semibold text-ink-950">Site details</h2>
          <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3 text-sm">
            {#each siteDetails as item}
              <div class="rounded-xl bg-ink-50 px-3 py-3">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">{item.label}</p>
                <p class="mt-1 font-medium text-ink-900">{item.value}</p>
              </div>
            {/each}
          </div>
        </div>

        <div class="card">
          <h2 class="text-lg font-semibold text-ink-950">Scope notes</h2>
          <div class="mt-4 space-y-3 text-sm">
            {#each scopeNotes as note}
              <div class="rounded-xl bg-ink-50 px-4 py-3 text-ink-700">{note}</div>
            {/each}
          </div>
        </div>

        <div class="grid gap-6 lg:grid-cols-2">
          <div class="card">
            <h2 class="text-lg font-semibold text-ink-950">Attachments & reference materials</h2>
            <div class="mt-4 space-y-3 text-sm">
              {#each referenceMaterials as item}
                <div class="rounded-xl bg-ink-50 px-4 py-3 text-ink-700">{item}</div>
              {/each}
            </div>
          </div>

          <div class="card">
            <h2 class="text-lg font-semibold text-ink-950">Hazards & prep risks</h2>
            <div class="mt-4 space-y-3 text-sm">
              {#each hazards as hazard}
                <div class="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-amber-900">{hazard}</div>
              {/each}
            </div>
          </div>
        </div>

        <div class="card">
          <h2 class="text-lg font-semibold text-ink-950">Visit objectives</h2>
          <div class="mt-4 grid gap-3 text-sm md:grid-cols-2">
            {#each visitObjectives as objective}
              <div class="rounded-xl bg-brand-50 px-4 py-3 text-brand-900">{objective}</div>
            {/each}
          </div>
        </div>
      </div>

      <aside class="space-y-6">
        <div class="card">
          <div class="flex items-start justify-between gap-4">
            <div>
              <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Required prep checklist</p>
              <h2 class="mt-2 text-lg font-semibold text-ink-950">{completedCount} of {checklistFields.length} confirmed</h2>
            </div>
            <div class="rounded-full bg-brand-50 px-3 py-1 text-sm font-semibold text-brand-700">
              {Math.round((completedCount / checklistFields.length) * 100)}%
            </div>
          </div>
          <div class="mt-4 space-y-3">
            {#each checklistFields as field}
              <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-ink-100 px-3 py-3 transition-colors hover:bg-ink-50">
                <input
                  type="checkbox"
                  class="mt-1 h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500"
                  checked={checklist[field.key]}
                  on:change={() => toggleChecklist(field.key)}
                />
                <div>
                  <p class="text-sm font-medium text-ink-900">{field.label}</p>
                  <p class="mt-1 text-sm leading-6 text-ink-500">{field.detail}</p>
                </div>
              </label>
            {/each}
          </div>
          {#if checklist.lastUpdatedAt}
            <p class="mt-4 text-xs text-ink-500">Last confirmed {formatDateTime(checklist.lastUpdatedAt)}</p>
          {/if}
        </div>

        <div class="card">
          <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Refresh behavior</p>
          <p class="mt-3 text-sm leading-6 text-ink-600">
            This prep view is rebuilt from the latest estimate, job, and visit record whenever you open it or hit refresh, so updates to the source request flow through here.
          </p>
          {#if lastLoadedAt}
            <p class="mt-3 text-xs text-ink-500">Last refreshed {formatDateTime(lastLoadedAt)}</p>
          {/if}
        </div>

        <div class="card">
          <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Source records</p>
          <div class="mt-4 space-y-3 text-sm">
            {#each sourceRecords as source}
              <div class="rounded-xl bg-ink-50 px-3 py-3">
                <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">{source.label}</p>
                <p class="mt-1 font-medium text-ink-900 break-all">{source.value}</p>
              </div>
            {/each}
          </div>
        </div>
      </aside>
    </div>
  {/if}
</div>
