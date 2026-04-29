<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$api/client';
  import { EmptyState, LoadingSpinner } from '$components';
  import type { CalendarEventDto, EstimateDto, JobDto } from '$lib/api/types';
  import {
    buildHazards,
    buildOutcomeStorageKey,
    buildPrepStorageKey,
    buildReferenceMaterials,
    buildScopeNotes,
    buildVisitObjectives,
    createDefaultPrepChecklist,
    createDefaultSiteVisitOutcome,
    readPrepChecklist,
    readSiteVisitOutcome,
    writePrepChecklist,
    writeSiteVisitOutcome,
    type PrepChecklistField,
    type PrepChecklistState,
    type SiteVisitOutcomeFileMeta,
    type SiteVisitOutcomeRecord
  } from '$lib/site-visit-prep';
  import { formatCurrency, formatDate, formatDateTime, statusColor } from '$lib/utils/format';
  import { currentUser } from '$stores/auth';

  let loading = true;
  let errorMessage = '';
  let event: CalendarEventDto | null = null;
  let job: JobDto | null = null;
  let estimate: EstimateDto | null = null;
  let checklist: PrepChecklistState = createDefaultPrepChecklist();
  let outcome: SiteVisitOutcomeRecord = createDefaultSiteVisitOutcome();
  let outcomeMessage = '';
  let outcomeMessageTone: 'blue' | 'emerald' | 'amber' = 'blue';
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
  $: prepStorageKey = buildPrepStorageKey({
    estimateId: estimate?.id ?? estimateId,
    eventId: event?.id ?? eventId,
    jobId: job?.id ?? jobId
  });
  $: outcomeStorageKey = buildOutcomeStorageKey(prepStorageKey);

  $: if (sourceKey !== loadedSourceKey && (estimateId || eventId || jobId)) {
    loadedSourceKey = sourceKey;
    loadPrep();
  }

  $: completedCount = checklistFields.filter((field) => checklist[field.key]).length;
  $: allPrepChecksComplete = completedCount === checklistFields.length;
  $: hasStructuredMeasurements =
    outcome.measurements.lengthFt !== null ||
    outcome.measurements.widthFt !== null ||
    outcome.measurements.depthIn !== null ||
    outcome.measurements.pourCount !== null ||
    Boolean(outcome.measurements.notes.trim());
  $: completionChecks = [
    { label: 'Findings summary recorded', ok: Boolean(outcome.findings.trim()) },
    { label: 'Measurements captured', ok: hasStructuredMeasurements },
    { label: 'Scope changes recorded', ok: Boolean(outcome.scopeChanges.trim()) },
    { label: 'Follow-up actions recorded', ok: Boolean(outcome.followUpActions.trim()) },
    { label: 'Prep checklist fully confirmed', ok: allPrepChecksComplete }
  ];
  $: visitAlreadyComplete = outcome.requestStatus === 'Visit Complete';
  $: canMarkVisitComplete = completionChecks.every((check) => check.ok) && !visitAlreadyComplete;
  $: visitWindow =
    event?.startUtc || job?.scheduledStart
      ? `${formatDateTime(event?.startUtc ?? job?.scheduledStart)}${event?.endUtc || job?.scheduledEnd ? ` – ${formatDateTime(event?.endUtc ?? job?.scheduledEnd)}` : ''}`
      : '—';
  $: requestStatus = outcome.requestStatus;
  $: requestSummary = [
    { label: 'Customer', value: estimate?.customerName ?? job?.customerName ?? '—' },
    { label: 'Estimate', value: estimate?.estimateNumber ?? job?.estimateNumber ?? '—' },
    { label: 'Request status', value: requestStatus, badge: true },
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
  $: structuredFieldSummary = [
    { label: 'Reinforcement', value: outcome.structuredFields.reinforcementType || 'Not set' },
    { label: 'Finish', value: outcome.structuredFields.finishType || 'Not set' },
    { label: 'Demo required', value: outcome.structuredFields.demoRequired ? 'Yes' : 'No' },
    { label: 'Excavation required', value: outcome.structuredFields.excavationRequired ? 'Yes' : 'No' },
    { label: 'Pump required', value: outcome.structuredFields.pumpRequired ? 'Yes' : 'No' }
  ];
  $: backHref = eventId ? '/app/calendar' : estimate?.id ? `/app/estimates/${estimate.id}` : job?.id ? '/app/jobs' : '/app';

  function outcomeStatusClass(status: string) {
    if (status === 'Visit Complete') return 'badge-green';
    return statusColor(status);
  }

  function actorLabel() {
    const name = [$currentUser?.firstName, $currentUser?.lastName].filter(Boolean).join(' ').trim();
    return name || $currentUser?.email || 'Internal Admin';
  }

  function buildDefaultOutcome(): SiteVisitOutcomeRecord {
    return createDefaultSiteVisitOutcome({
      measurements: {
        lengthFt: estimate?.structuredInput?.lengthFt ?? null,
        widthFt: estimate?.structuredInput?.widthFt ?? null,
        depthIn: estimate?.structuredInput?.depthIn ?? null,
        pourCount: estimate?.structuredInput?.pourCount ?? null,
        notes: ''
      },
      structuredFields: {
        reinforcementType: estimate?.structuredInput?.reinforcementType ?? '',
        finishType: estimate?.structuredInput?.finishType ?? '',
        demoRequired: Boolean(estimate?.structuredInput?.demoRequired),
        excavationRequired: Boolean(estimate?.structuredInput?.excavationRequired),
        pumpRequired: Boolean(estimate?.structuredInput?.pumpRequired)
      }
    });
  }

  function setOutcomeMessage(message: string, tone: 'blue' | 'emerald' | 'amber') {
    outcomeMessage = message;
    outcomeMessageTone = tone;
  }

  async function loadPrep() {
    loading = true;
    errorMessage = '';
    outcomeMessage = '';
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

      const resolvedPrepKey = buildPrepStorageKey({
        estimateId: resolvedEstimateId || estimate?.id,
        eventId: event?.id ?? eventId,
        jobId: resolvedJobId || job?.id
      });

      checklist = readPrepChecklist(resolvedPrepKey);

      const defaultOutcome = buildDefaultOutcome();
      const storedOutcome = readSiteVisitOutcome(buildOutcomeStorageKey(resolvedPrepKey));
      outcome = storedOutcome
        ? createDefaultSiteVisitOutcome({
            ...defaultOutcome,
            ...storedOutcome,
            files: storedOutcome.files,
            measurements: {
              ...defaultOutcome.measurements,
              ...storedOutcome.measurements
            },
            structuredFields: {
              ...defaultOutcome.structuredFields,
              ...storedOutcome.structuredFields
            },
            timeline: storedOutcome.timeline
          })
        : defaultOutcome;

      lastLoadedAt = new Date().toISOString();
    } catch (err: any) {
      errorMessage = err?.message ?? 'Unable to load the site visit prep summary.';
    } finally {
      loading = false;
    }
  }

  function persistOutcome(nextOutcome: SiteVisitOutcomeRecord, message?: string, tone: 'blue' | 'emerald' | 'amber' = 'blue') {
    outcome = {
      ...nextOutcome,
      lastSavedAt: nextOutcome.lastSavedAt ?? new Date().toISOString()
    };
    writeSiteVisitOutcome(outcomeStorageKey, outcome);
    if (message) {
      setOutcomeMessage(message, tone);
    }
  }

  function toggleChecklist(field: PrepChecklistField) {
    checklist = {
      ...checklist,
      [field]: !checklist[field],
      lastUpdatedAt: new Date().toISOString()
    };
    writePrepChecklist(prepStorageKey, checklist);
  }

  function handleFileSelection(target: EventTarget | null) {
    const input = target as HTMLInputElement | null;
    const selected = Array.from(input?.files ?? []);
    const fileMetas: SiteVisitOutcomeFileMeta[] = selected.map((file) => ({
      id: crypto.randomUUID(),
      name: file.name,
      contentType: file.type || 'application/octet-stream',
      sizeBytes: file.size
    }));

    persistOutcome(
      {
        ...outcome,
        files: [...outcome.files, ...fileMetas],
        lastSavedAt: new Date().toISOString()
      },
      fileMetas.length ? `${fileMetas.length} file${fileMetas.length === 1 ? '' : 's'} staged for this visit outcome.` : '',
      'blue'
    );

    if (input) input.value = '';
  }

  function removeFile(id: string) {
    persistOutcome(
      {
        ...outcome,
        files: outcome.files.filter((file) => file.id !== id),
        lastSavedAt: new Date().toISOString()
      },
      'Removed the selected file from the local visit outcome draft.',
      'blue'
    );
  }

  function saveOutcomeDraft() {
    persistOutcome(
      {
        ...outcome,
        requestStatus: outcome.requestStatus === 'Visit Complete' ? 'Visit Complete' : 'In Progress',
        lastSavedAt: new Date().toISOString()
      },
      'Saved the site visit outcome draft locally for this prep record.',
      'blue'
    );
  }

  function markVisitComplete() {
    if (visitAlreadyComplete) {
      setOutcomeMessage('This visit outcome is already marked complete.', 'amber');
      return;
    }

    if (!canMarkVisitComplete) {
      setOutcomeMessage(
        `Complete the remaining required items before marking the visit complete: ${completionChecks.filter((check) => !check.ok).map((check) => check.label).join(' · ')}.`,
        'amber'
      );
      return;
    }

    const completedAt = new Date().toISOString();
    const actor = actorLabel();
    const nextTimeline = [
      ...outcome.timeline,
      {
        id: crypto.randomUUID(),
        type: 'visit-completed' as const,
        occurredAt: completedAt,
        actor,
        label: 'Visit completed',
        note: outcome.followUpActions.trim()
      }
    ];

    persistOutcome(
      {
        ...outcome,
        requestStatus: 'Visit Complete',
        completedAt,
        completedBy: actor,
        lastSavedAt: completedAt,
        timeline: nextTimeline
      },
      `Marked the visit complete locally as ${actor} at ${formatDateTime(completedAt)}.`,
      'emerald'
    );
  }
</script>

<div class="mx-auto max-w-7xl">
  <div class="page-header mb-6 flex-wrap gap-3">
    <div>
      <p class="text-xs font-semibold uppercase tracking-[0.22em] text-brand-600">Internal Admin · Site Visit Prep</p>
      <h1 class="page-title mt-2">Prep the field team before the visit</h1>
      <p class="mt-3 max-w-3xl text-sm leading-6 text-ink-600">
        This view pulls together the request record, visit record, and job context into one concise prep summary for the field team, then converts the visit outcome into structured estimate-ready fields.
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
                <span class={outcomeStatusClass(requestStatus)}>{requestStatus}</span>
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
                  <span class={`mt-2 inline-flex ${outcomeStatusClass(item.value)}`}>{item.value}</span>
                {:else}
                  <p class="mt-1 font-medium text-ink-900">{item.value}</p>
                {/if}
              </div>
            {/each}
          </div>
          {#if outcome.completedAt}
            <div class="mt-4 rounded-2xl border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-900">
              Visit completed by <span class="font-semibold">{outcome.completedBy}</span> on {formatDateTime(outcome.completedAt)}.
            </div>
          {/if}
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

        <div class="card">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Structured site visit outcome</p>
              <h2 class="mt-2 text-lg font-semibold text-ink-950">Capture estimate-ready findings</h2>
              <p class="mt-2 max-w-3xl text-sm leading-6 text-ink-600">
                Record structured findings, measurements, scope changes, files, and follow-up actions. All required completion checks must pass before marking the visit complete.
              </p>
            </div>
            <div class="flex flex-wrap gap-3">
              <button class="btn-secondary" on:click={saveOutcomeDraft}>Save draft outcome</button>
              <button
                class="btn-primary disabled:cursor-not-allowed disabled:opacity-50"
                disabled={!canMarkVisitComplete}
                on:click={markVisitComplete}
              >
                {visitAlreadyComplete ? 'Visit complete' : 'Mark visit complete'}
              </button>
            </div>
          </div>

          {#if outcomeMessage}
            <div class={`mt-4 rounded-2xl px-4 py-3 text-sm ${outcomeMessageTone === 'emerald' ? 'border border-green-200 bg-green-50 text-green-900' : outcomeMessageTone === 'amber' ? 'border border-amber-200 bg-amber-50 text-amber-900' : 'border border-brand-100 bg-brand-50 text-brand-900'}`}>
              {outcomeMessage}
            </div>
          {/if}

          <div class="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_21rem]">
            <div class="space-y-6">
              <div class="grid gap-4">
                <label class="grid gap-2">
                  <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Findings summary *</span>
                  <textarea
                    class="input min-h-[8rem]"
                    bind:value={outcome.findings}
                    placeholder="Summarize the visit findings, observed conditions, and the most important estimate-ready takeaways."
                  ></textarea>
                </label>
              </div>

              <div class="rounded-2xl border border-ink-100 bg-ink-50/70 px-4 py-4">
                <div class="flex items-center justify-between gap-3">
                  <div>
                    <p class="text-sm font-semibold text-ink-900">Measurements *</p>
                    <p class="mt-1 text-sm text-ink-500">Capture reusable field measurements and any measurement notes that should flow into estimating.</p>
                  </div>
                </div>
                <div class="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <label class="grid gap-2">
                    <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Length (ft)</span>
                    <input class="input" type="number" step="0.1" bind:value={outcome.measurements.lengthFt} />
                  </label>
                  <label class="grid gap-2">
                    <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Width (ft)</span>
                    <input class="input" type="number" step="0.1" bind:value={outcome.measurements.widthFt} />
                  </label>
                  <label class="grid gap-2">
                    <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Depth (in)</span>
                    <input class="input" type="number" step="0.5" bind:value={outcome.measurements.depthIn} />
                  </label>
                  <label class="grid gap-2">
                    <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Pours</span>
                    <input class="input" type="number" min="1" bind:value={outcome.measurements.pourCount} />
                  </label>
                </div>
                <label class="mt-4 grid gap-2">
                  <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Measurement notes</span>
                  <textarea
                    class="input min-h-[6rem]"
                    bind:value={outcome.measurements.notes}
                    placeholder="Slope notes, elevation changes, access measurements, or any field caveats that estimating should retain."
                  ></textarea>
                </label>
              </div>

              <div class="grid gap-4 xl:grid-cols-2">
                <label class="grid gap-2">
                  <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Scope changes *</span>
                  <textarea
                    class="input min-h-[8rem]"
                    bind:value={outcome.scopeChanges}
                    placeholder="Describe any changes from the planned scope. Use 'No scope change' if everything matched the original expectation."
                  ></textarea>
                </label>

                <label class="grid gap-2">
                  <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Follow-up actions *</span>
                  <textarea
                    class="input min-h-[8rem]"
                    bind:value={outcome.followUpActions}
                    placeholder="List the next actions for estimating, scheduling, customer follow-up, or document collection."
                  ></textarea>
                </label>
              </div>

              <div class="rounded-2xl border border-ink-100 bg-ink-50/70 px-4 py-4">
                <p class="text-sm font-semibold text-ink-900">Estimate-ready structured fields</p>
                <p class="mt-1 text-sm text-ink-500">These fields are stored in a reusable structured block so estimating can start from facts instead of freeform notes.</p>
                <div class="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                  <label class="grid gap-2">
                    <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Reinforcement type</span>
                    <input class="input" bind:value={outcome.structuredFields.reinforcementType} placeholder="Rebar, mesh, fiber..." />
                  </label>
                  <label class="grid gap-2">
                    <span class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-ink-500">Finish type</span>
                    <input class="input" bind:value={outcome.structuredFields.finishType} placeholder="Broom, smooth, stamped..." />
                  </label>
                  <div class="grid gap-3 rounded-xl border border-ink-100 bg-white px-3 py-3">
                    <label class="flex items-center gap-3 text-sm text-ink-700">
                      <input type="checkbox" bind:checked={outcome.structuredFields.demoRequired} class="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500" />
                      Demo required
                    </label>
                    <label class="flex items-center gap-3 text-sm text-ink-700">
                      <input type="checkbox" bind:checked={outcome.structuredFields.excavationRequired} class="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500" />
                      Excavation required
                    </label>
                    <label class="flex items-center gap-3 text-sm text-ink-700">
                      <input type="checkbox" bind:checked={outcome.structuredFields.pumpRequired} class="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500" />
                      Pump required
                    </label>
                  </div>
                </div>
              </div>

              <div class="rounded-2xl border border-ink-100 bg-ink-50/70 px-4 py-4">
                <p class="text-sm font-semibold text-ink-900">Photos / files</p>
                <p class="mt-1 text-sm text-ink-500">Capture file metadata for photos, sketches, or supporting documents attached during outcome recording.</p>
                <div class="mt-4">
                  <input
                    class="block w-full text-sm text-ink-700 file:mr-4 file:rounded-xl file:border-0 file:bg-brand-600 file:px-4 file:py-2 file:text-sm file:font-semibold file:text-white hover:file:bg-brand-700"
                    type="file"
                    multiple
                    accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,.csv,.txt"
                    on:change={(event) => handleFileSelection(event.currentTarget)}
                  />
                </div>
                <div class="mt-4 space-y-3">
                  {#if outcome.files.length}
                    {#each outcome.files as file}
                      <div class="flex items-center justify-between gap-3 rounded-xl border border-ink-100 bg-white px-3 py-3 text-sm">
                        <div class="min-w-0">
                          <p class="font-medium text-ink-900">{file.name}</p>
                          <p class="mt-1 text-ink-500">{file.contentType} · {Math.max(1, Math.round(file.sizeBytes / 1024))} KB</p>
                        </div>
                        <button class="btn-secondary" type="button" on:click={() => removeFile(file.id)}>Remove</button>
                      </div>
                    {/each}
                  {:else}
                    <div class="rounded-xl border border-dashed border-ink-200 bg-white px-4 py-4 text-sm text-ink-500">
                      No local file metadata captured yet for this visit outcome.
                    </div>
                  {/if}
                </div>
              </div>
            </div>

            <aside class="space-y-4">
              <div class="rounded-2xl border border-ink-100 bg-ink-50/70 px-4 py-4">
                <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Completion gate</p>
                <div class="mt-4 space-y-3">
                  {#each completionChecks as check}
                    <div class={`rounded-xl px-3 py-3 text-sm ${check.ok ? 'border border-green-200 bg-green-50 text-green-900' : 'border border-amber-200 bg-amber-50 text-amber-900'}`}>
                      <span class="font-medium">{check.ok ? 'Ready' : 'Required'}:</span> {check.label}
                    </div>
                  {/each}
                </div>
              </div>

              <div class="rounded-2xl border border-ink-100 bg-ink-50/70 px-4 py-4">
                <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Reusable structured summary</p>
                <div class="mt-4 space-y-3 text-sm">
                  {#each structuredFieldSummary as field}
                    <div class="rounded-xl bg-white px-3 py-3">
                      <p class="text-[11px] uppercase tracking-[0.18em] text-ink-500">{field.label}</p>
                      <p class="mt-1 font-medium text-ink-900">{field.value}</p>
                    </div>
                  {/each}
                </div>
              </div>
            </aside>
          </div>
        </div>

        <div class="card">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p class="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand-600">Visit outcome timeline</p>
              <h2 class="mt-2 text-lg font-semibold text-ink-950">Who completed the visit and when</h2>
            </div>
            {#if outcome.lastSavedAt}
              <p class="text-xs text-ink-500">Last saved {formatDateTime(outcome.lastSavedAt)}</p>
            {/if}
          </div>
          <div class="mt-4 space-y-3">
            {#if outcome.timeline.length}
              {#each [...outcome.timeline].reverse() as entry}
                <div class="rounded-2xl border border-ink-100 bg-ink-50 px-4 py-4 text-sm">
                  <div class="flex flex-wrap items-center justify-between gap-3">
                    <p class="font-semibold text-ink-900">{entry.label}</p>
                    <p class="text-xs text-ink-500">{formatDateTime(entry.occurredAt)}</p>
                  </div>
                  <p class="mt-2 text-ink-700">{entry.actor}</p>
                  {#if entry.note}
                    <p class="mt-2 text-ink-500">{entry.note}</p>
                  {/if}
                </div>
              {/each}
            {:else}
              <div class="rounded-xl border border-dashed border-ink-200 bg-white px-4 py-4 text-sm text-ink-500">
                No completion event recorded yet. Mark the visit complete to stamp who completed it and when.
              </div>
            {/if}
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
