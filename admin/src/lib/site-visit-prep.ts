import { browser } from '$app/environment';
import type { CalendarEventDto, EstimateDto, JobDto } from '$lib/api/types';
import { formatCurrency } from '$lib/utils/format';

export type PrepChecklistField =
  | 'summaryConfirmed'
  | 'attachmentsConfirmed'
  | 'hazardsConfirmed'
  | 'objectivesConfirmed';

export type PrepChecklistState = Record<PrepChecklistField, boolean> & {
  lastUpdatedAt?: string;
};

export type SiteVisitOutcomeFileMeta = {
  id: string;
  name: string;
  contentType: string;
  sizeBytes: number;
};

export type SiteVisitOutcomeMeasurements = {
  lengthFt: number | null;
  widthFt: number | null;
  depthIn: number | null;
  pourCount: number | null;
  notes: string;
};

export type SiteVisitOutcomeStructuredFields = {
  reinforcementType: string;
  finishType: string;
  demoRequired: boolean;
  excavationRequired: boolean;
  pumpRequired: boolean;
};

export type SiteVisitOutcomeTimelineEntry = {
  id: string;
  type: 'visit-completed';
  occurredAt: string;
  actor: string;
  label: string;
  note?: string;
};

export type SiteVisitOutcomeRecord = {
  findings: string;
  scopeChanges: string;
  followUpActions: string;
  files: SiteVisitOutcomeFileMeta[];
  measurements: SiteVisitOutcomeMeasurements;
  structuredFields: SiteVisitOutcomeStructuredFields;
  requestStatus: 'In Progress' | 'Visit Complete';
  completedAt?: string;
  completedBy?: string;
  lastSavedAt?: string;
  timeline: SiteVisitOutcomeTimelineEntry[];
};

const hasText = (value?: string | null): value is string => Boolean(value?.trim());

const uniq = (values: string[]) => Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)));

export const createDefaultPrepChecklist = (): PrepChecklistState => ({
  summaryConfirmed: false,
  attachmentsConfirmed: false,
  hazardsConfirmed: false,
  objectivesConfirmed: false
});

export const buildPrepStorageKey = (params: {
  estimateId?: string | null;
  eventId?: string | null;
  jobId?: string | null;
}) =>
  `turnkeyops_site_visit_prep_${params.estimateId ?? 'none'}_${params.eventId ?? 'none'}_${params.jobId ?? 'none'}`;

export const readPrepChecklist = (storageKey: string): PrepChecklistState => {
  if (!browser) return createDefaultPrepChecklist();

  const raw = localStorage.getItem(storageKey);
  if (!raw) return createDefaultPrepChecklist();

  try {
    const parsed = JSON.parse(raw) as Partial<PrepChecklistState>;
    return {
      ...createDefaultPrepChecklist(),
      ...parsed
    };
  } catch {
    localStorage.removeItem(storageKey);
    return createDefaultPrepChecklist();
  }
};

export const writePrepChecklist = (storageKey: string, value: PrepChecklistState) => {
  if (!browser) return;
  localStorage.setItem(storageKey, JSON.stringify(value));
};

export const buildOutcomeStorageKey = (prepStorageKey: string) => `${prepStorageKey}_outcome`;

export const createDefaultSiteVisitOutcome = (
  seed?: Partial<SiteVisitOutcomeRecord>
): SiteVisitOutcomeRecord => {
  const base: SiteVisitOutcomeRecord = {
    findings: '',
    scopeChanges: '',
    followUpActions: '',
    files: [],
    measurements: {
      lengthFt: null,
      widthFt: null,
      depthIn: null,
      pourCount: null,
      notes: ''
    },
    structuredFields: {
      reinforcementType: '',
      finishType: '',
      demoRequired: false,
      excavationRequired: false,
      pumpRequired: false
    },
    requestStatus: 'In Progress',
    timeline: []
  };

  return {
    ...base,
    ...seed,
    files: seed?.files ?? base.files,
    measurements: {
      ...base.measurements,
      ...seed?.measurements
    },
    structuredFields: {
      ...base.structuredFields,
      ...seed?.structuredFields
    },
    timeline: seed?.timeline ?? base.timeline
  };
};

export const readSiteVisitOutcome = (storageKey: string): SiteVisitOutcomeRecord | null => {
  if (!browser) return null;

  const raw = localStorage.getItem(storageKey);
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as Partial<SiteVisitOutcomeRecord>;
    return createDefaultSiteVisitOutcome(parsed);
  } catch {
    localStorage.removeItem(storageKey);
    return null;
  }
};

export const writeSiteVisitOutcome = (storageKey: string, value: SiteVisitOutcomeRecord) => {
  if (!browser) return;
  localStorage.setItem(storageKey, JSON.stringify(value));
};

export const buildScopeNotes = (params: {
  estimate?: EstimateDto | null;
  job?: JobDto | null;
  event?: CalendarEventDto | null;
}) => {
  const { estimate, job, event } = params;
  const notes: string[] = [];

  if (hasText(job?.description)) {
    notes.push(job.description);
  }

  if (hasText(job?.notes)) {
    notes.push(`Job notes: ${job.notes}`);
  }

  if (hasText(estimate?.notes)) {
    notes.push(`Estimate notes: ${estimate.notes}`);
  }

  const structured: string[] = [];
  if (hasText(estimate?.structuredInput?.projectType)) structured.push(`${estimate.structuredInput.projectType} scope`);
  if (estimate?.structuredInput?.lengthFt && estimate.structuredInput?.widthFt) {
    structured.push(`${estimate.structuredInput.lengthFt} ft × ${estimate.structuredInput.widthFt} ft footprint`);
  }
  if (estimate?.structuredInput?.depthIn) structured.push(`${estimate.structuredInput.depthIn}" depth`);
  if (estimate?.structuredInput?.pourCount) structured.push(`${estimate.structuredInput.pourCount} pour${estimate.structuredInput.pourCount === 1 ? '' : 's'}`);
  if (hasText(estimate?.structuredInput?.reinforcementType)) {
    structured.push(`${estimate.structuredInput.reinforcementType} reinforcement`);
  }
  if (hasText(estimate?.structuredInput?.finishType)) {
    structured.push(`${estimate.structuredInput.finishType} finish`);
  }
  if (structured.length) {
    notes.push(`Structured scope: ${structured.join(' · ')}.`);
  }

  if (estimate?.lineItems?.length) {
    notes.push(`${estimate.lineItems.length} estimate line item${estimate.lineItems.length === 1 ? '' : 's'} available for prep review.`);
  }

  if (hasText(event?.description)) {
    notes.push(`Visit record notes: ${event.description}`);
  }

  if (!notes.length) {
    notes.push('No extra scope notes are captured yet. Use the estimate, job, and visit record details below as the prep baseline.');
  }

  return uniq(notes);
};

export const buildReferenceMaterials = (params: {
  estimate?: EstimateDto | null;
  job?: JobDto | null;
}) => {
  const { estimate, job } = params;
  const materials: string[] = [];

  if (estimate?.lineItems?.length) {
    materials.push(`${estimate.lineItems.length} estimate line item${estimate.lineItems.length === 1 ? '' : 's'} on the request record.`);
  }

  if (estimate?.calculationSnapshot) {
    materials.push(
      `Estimate pricing snapshot available (${formatCurrency(estimate.calculationSnapshot.finalEstimatedPrice)} final estimate).`
    );
  }

  if (job?.estimateSnapshot) {
    materials.push(
      `Job estimate snapshot available (${formatCurrency(job.estimateSnapshot.finalEstimatedPrice)} final estimate).`
    );
  }

  if (estimate?.signatureDataUrl) {
    materials.push('Signed estimate packet is stored on the estimate record.');
  }

  if (!materials.length) {
    materials.push('No synced attachments or reference files are linked on this Internal Admin record yet.');
  }

  return uniq(materials);
};

export const buildHazards = (params: {
  estimate?: EstimateDto | null;
  job?: JobDto | null;
  event?: CalendarEventDto | null;
}) => {
  const { estimate, job, event } = params;
  const hazards: string[] = [];
  const structured = estimate?.structuredInput;
  const searchable = [estimate?.notes, job?.description, job?.notes, event?.description]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();

  if (structured?.demoRequired) {
    hazards.push('Existing concrete/demo area should be checked for debris staging, disposal flow, and safe customer access.');
  }

  if (structured?.excavationRequired) {
    hazards.push('Confirm grade, utilities, and excavation access before the field visit begins.');
  }

  if (structured?.pumpRequired) {
    hazards.push('Verify pump-truck access, washout location, and reach limitations on arrival.');
  }

  if (searchable.includes('gate') || searchable.includes('parking') || searchable.includes('access')) {
    hazards.push('Parking, gate, or general site access constraints were mentioned in the source record.');
  }

  if (searchable.includes('after-hours') || searchable.includes('tenant')) {
    hazards.push('Coordinate after-hours or occupied-site access so the visit does not disrupt tenants or operations.');
  }

  if (searchable.includes('roof') || searchable.includes('ladder')) {
    hazards.push('Roof or ladder access should be reviewed with the field resource before the visit starts.');
  }

  if (!hazards.length) {
    hazards.push('No explicit hazards are recorded yet. Confirm utilities, access, weather, and pedestrian safety before dispatch.');
  }

  return uniq(hazards);
};

export const buildVisitObjectives = (params: {
  estimate?: EstimateDto | null;
  job?: JobDto | null;
  event?: CalendarEventDto | null;
}) => {
  const { estimate, job, event } = params;
  const objectives: string[] = [];
  const structured = estimate?.structuredInput;

  objectives.push('Confirm the field resource has the latest customer, site, and visit-window context.');

  if (hasText(estimate?.projectName) || hasText(job?.projectName)) {
    objectives.push(`Walk the planned scope for ${estimate?.projectName ?? job?.projectName} and confirm no handoff details are missing.`);
  }

  if (structured?.demoRequired) {
    objectives.push('Validate demolition extent, haul-off assumptions, and any customer protection needs.');
  }

  if (structured?.pumpRequired) {
    objectives.push('Check pump placement, truck approach, and washout constraints.');
  }

  if (structured?.excavationRequired) {
    objectives.push('Verify excavation depth, grade conditions, and whether utility marking is needed.');
  }

  if (estimate?.calculationSnapshot || job?.estimateSnapshot) {
    objectives.push('Review the estimate assumptions on site so pricing and production prep stay aligned.');
  } else {
    objectives.push('Capture the measurements and site conditions needed to finalize estimate assumptions.');
  }

  if (event?.eventType === 'Inspection') {
    objectives.push('Leave the visit with a clear go/no-go summary for the next scheduling or estimating step.');
  }

  return uniq(objectives);
};
