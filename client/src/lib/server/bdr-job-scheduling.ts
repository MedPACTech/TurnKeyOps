import type { QuoteRequest } from '$lib/quote-requests';
import type { BdrBillingSettings } from '$lib/server/bdr-billing-settings';
import {
	getBdrInvoiceAmountPaid,
	getBdrInvoiceBalanceDue,
	type BdrInvoiceRecord
} from '$lib/server/bdr-invoices';
import { getTurnKeyApiBaseUrl, getTurnKeyApiHeaders, unwrapTurnKeyApiEnvelope } from '$lib/server/turnkey-api';

export type BdrProductionJobStatus = 'scheduled' | 'in-progress' | 'on-hold' | 'completed' | 'cancelled';
export type BdrProductionJobConfirmationStatus = 'pending' | 'confirmed' | 'needs-reschedule';
export type BdrProductionJobOrderStatus = 'not-started' | 'requested' | 'ordered' | 'confirmed' | 'delivered';
export type BdrProductionJobChecklistKey =
	| 'customer-confirmed'
	| 'site-access'
	| 'utility-locate'
	| 'base-material-ordered'
	| 'equipment-reserved'
	| 'concrete-ordered'
	| 'forms-reinforcement'
	| 'weather-check'
	| 'pour-confirmed'
	| 'cleanup-walkthrough';

export type BdrProductionJobPlanning = {
	customer: {
		confirmationStatus: BdrProductionJobConfirmationStatus;
		confirmedAtUtc?: string;
		confirmationNote?: string;
		accessNotes?: string;
	};
	schedule: { targetDate: string; prepDate?: string; pourDate?: string; cleanupDate?: string };
	materials: {
		baseMaterialStatus: BdrProductionJobOrderStatus;
		baseMaterialSupplier?: string;
		baseMaterialDeliveryDate?: string;
		baseMaterialDeliveryWindow?: string;
		reinforcementStatus: BdrProductionJobOrderStatus;
		reinforcementSupplier?: string;
		equipmentStatus: BdrProductionJobOrderStatus;
		equipmentVendor?: string;
		equipmentDeliveryDate?: string;
		equipmentDeliveryWindow?: string;
		concreteStatus: BdrProductionJobOrderStatus;
		concreteSupplier?: string;
		concreteDeliveryDate?: string;
		concreteDeliveryWindow?: string;
		concreteYards?: number;
		concreteMix?: string;
		pumpNeeded: boolean;
		notes?: string;
	};
	checklist: Record<BdrProductionJobChecklistKey, boolean>;
	updatedAtUtc?: string;
	updatedBy?: string;
};

export type BdrProductionJobActivity = {
	id: string;
	type: 'scheduled' | 'status-updated' | 'rescheduled' | 'planning-updated' | 'note';
	label: string;
	occurredAtUtc: string;
	actor: string;
	note?: string;
};

export type BdrScheduledJobRecord = {
	id: string;
	invoiceId: string;
	sourceRequestId: string;
	invoiceNumber: string;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	serviceAddress: string;
	contactName: string;
	phone: string;
	email: string;
	amount: number;
	amountPaidAtScheduling: number;
	depositPercentRequired: number;
	scheduledDate: string;
	windowStart: string;
	windowEnd: string;
	crew: string;
	notes?: string;
	status: BdrProductionJobStatus;
	scheduledAtUtc: string;
	scheduledBy: string;
	updatedAtUtc: string;
	completedAtUtc?: string;
	cancelledAtUtc?: string;
	holdReason?: string;
	planning: BdrProductionJobPlanning;
	activity: BdrProductionJobActivity[];
	version: string;
};

export type BdrProductionJobPlanningUpdateInput = {
	customerConfirmationStatus: BdrProductionJobConfirmationStatus;
	customerConfirmationNote?: string;
	accessNotes?: string;
	targetDate?: string;
	prepDate?: string;
	pourDate?: string;
	cleanupDate?: string;
	baseMaterialStatus: BdrProductionJobOrderStatus;
	baseMaterialSupplier?: string;
	baseMaterialDeliveryDate?: string;
	baseMaterialDeliveryWindow?: string;
	reinforcementStatus: BdrProductionJobOrderStatus;
	reinforcementSupplier?: string;
	equipmentStatus: BdrProductionJobOrderStatus;
	equipmentVendor?: string;
	equipmentDeliveryDate?: string;
	equipmentDeliveryWindow?: string;
	concreteStatus: BdrProductionJobOrderStatus;
	concreteSupplier?: string;
	concreteDeliveryDate?: string;
	concreteDeliveryWindow?: string;
	concreteYards?: number;
	concreteMix?: string;
	pumpNeeded?: boolean;
	materialNotes?: string;
	checklist: BdrProductionJobChecklistKey[];
	actor?: string;
};

export type BdrScheduleReadyJob = {
	invoiceId: string;
	sourceRequestId: string;
	invoiceNumber: string;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	serviceAddress: string;
	contactName: string;
	phone: string;
	email: string;
	amount: number;
	amountPaid: number;
	balanceDue: number;
	requiredDepositAmount: number;
	depositPercentRequired: number;
	paidPercent: number;
	isReady: boolean;
	isScheduled: boolean;
	scheduledJob?: BdrScheduledJobRecord;
};

type JobApiMaterial = {
	id?: string;
	kind: string;
	status: string;
	supplier?: string | null;
	deliveryDate?: string | null;
	deliveryWindow?: string | null;
	quantity?: number | null;
	unit?: string | null;
	specification?: string | null;
	notes?: string | null;
};

type JobApiPlanning = {
	customerConfirmationStatus?: string;
	customerConfirmedAtUtc?: string | null;
	customerConfirmationNote?: string | null;
	accessNotes?: string | null;
	targetDate?: string | null;
	prepDate?: string | null;
	pourDate?: string | null;
	cleanupDate?: string | null;
	materials?: JobApiMaterial[];
	checklist?: Record<string, boolean>;
	updatedAtUtc?: string | null;
	updatedBy?: string | null;
};

type JobApiRecord = {
	id: string;
	name: string;
	description?: string | null;
	status: string;
	invoiceId?: string | null;
	quoteRequestId?: string | null;
	invoiceNumber?: string | null;
	customerName?: string | null;
	jobSiteName?: string | null;
	projectAddress?: string | null;
	projectName?: string | null;
	contactName?: string | null;
	contactPhone?: string | null;
	contactEmail?: string | null;
	scheduledStart?: string | null;
	scheduledEnd?: string | null;
	actualEnd?: string | null;
	crew?: string | null;
	estimatedTotal?: number;
	paidTotal?: number;
	requiredDepositPercent?: number;
	notes?: string | null;
	planning?: JobApiPlanning;
	activity?: Array<{ id: string; type: string; label: string; occurredAtUtc: string; actor: string; note?: string | null }>;
	version: string;
	dateCreated?: string | null;
	dateUpdated?: string | null;
};

type JobPageEnvelope = { data: JobApiRecord[]; success: boolean; continuationToken?: string | null };

const checklistKeys: BdrProductionJobChecklistKey[] = [
	'customer-confirmed', 'site-access', 'utility-locate', 'base-material-ordered', 'equipment-reserved',
	'concrete-ordered', 'forms-reinforcement', 'weather-check', 'pour-confirmed', 'cleanup-walkthrough'
];

const defaultChecklist = () =>
	Object.fromEntries(checklistKeys.map((key) => [key, false])) as Record<BdrProductionJobChecklistKey, boolean>;

const api = (path: string, init?: RequestInit, fetcher: typeof globalThis.fetch = fetch) =>
	fetcher(`${getTurnKeyApiBaseUrl()}${path}`, {
		...init,
		headers: { ...getTurnKeyApiHeaders(init?.body !== undefined), ...(init?.headers ?? {}) }
	});

const time = (value?: string | null) => value?.slice(11, 16) || '08:00';
const date = (value?: string | null) => value?.slice(0, 10) || '';
const iso = (day: string, value: string) => `${day}T${value}:00.000Z`;
const orderStatus = (value?: string | null): BdrProductionJobOrderStatus => {
	const normalized = value?.toLowerCase();
	return normalized === 'requested' || normalized === 'ordered' || normalized === 'confirmed' || normalized === 'delivered'
		? normalized
		: 'not-started';
};
const jobStatus = (value: string): BdrProductionJobStatus => {
	const normalized = value.replaceAll('_', '').replaceAll('-', '').toLowerCase();
	if (normalized === 'inprogress') return 'in-progress';
	if (normalized === 'onhold') return 'on-hold';
	if (normalized === 'completed' || normalized === 'closed') return 'completed';
	if (normalized === 'cancelled') return 'cancelled';
	return 'scheduled';
};
const apiStatus = (value: BdrProductionJobStatus) =>
	value === 'in-progress' ? 'InProgress' : value === 'on-hold' ? 'OnHold' : value[0].toUpperCase() + value.slice(1);
const material = (planning: JobApiPlanning | undefined, kind: string) =>
	planning?.materials?.find((item) => item.kind.toLowerCase() === kind) ?? null;

const mapJob = (job: JobApiRecord): BdrScheduledJobRecord => {
	const start = job.scheduledStart || job.dateCreated || new Date(0).toISOString();
	const end = job.scheduledEnd || start;
	const planning = job.planning ?? {};
	const base = material(planning, 'base-material');
	const reinforcement = material(planning, 'reinforcement');
	const equipment = material(planning, 'equipment');
	const concrete = material(planning, 'concrete');
	const checklist = { ...defaultChecklist(), ...(planning.checklist ?? {}) } as Record<BdrProductionJobChecklistKey, boolean>;
	const activity: BdrProductionJobActivity[] = (job.activity ?? []).map((item) => ({
		id: item.id,
		type:
			item.type === 'status_updated' ? 'status-updated' :
			item.type === 'planning_updated' ? 'planning-updated' :
			item.type === 'job_created' ? 'scheduled' :
			(item.type.replaceAll('_', '-') as BdrProductionJobActivity['type']),
		label: item.label,
		occurredAtUtc: item.occurredAtUtc,
		actor: item.actor,
		note: item.note || undefined
	}));
	return {
		id: job.id,
		invoiceId: job.invoiceId ?? '',
		sourceRequestId: job.quoteRequestId ?? '',
		invoiceNumber: job.invoiceNumber ?? '',
		customerName: job.customerName ?? '',
		siteName: job.jobSiteName ?? job.name,
		serviceSummary: job.description ?? '',
		serviceAddress: job.projectAddress ?? '',
		contactName: job.contactName ?? job.customerName ?? '',
		phone: job.contactPhone ?? '',
		email: job.contactEmail ?? '',
		amount: job.estimatedTotal ?? 0,
		amountPaidAtScheduling: job.paidTotal ?? 0,
		depositPercentRequired: job.requiredDepositPercent ?? 50,
		scheduledDate: date(start), windowStart: time(start), windowEnd: time(end), crew: job.crew ?? '',
		notes: job.notes || undefined,
		status: jobStatus(job.status),
		scheduledAtUtc: job.dateCreated || start,
		scheduledBy: activity.at(-1)?.actor ?? 'Office admin',
		updatedAtUtc: job.dateUpdated || start,
		completedAtUtc: jobStatus(job.status) === 'completed' ? job.actualEnd || job.dateUpdated || undefined : undefined,
		cancelledAtUtc: jobStatus(job.status) === 'cancelled' ? job.dateUpdated || undefined : undefined,
		holdReason: jobStatus(job.status) === 'on-hold' ? activity[0]?.note : undefined,
		planning: {
			customer: {
				confirmationStatus: (planning.customerConfirmationStatus === 'confirmed' || planning.customerConfirmationStatus === 'needs-reschedule'
					? planning.customerConfirmationStatus : 'pending'),
				confirmedAtUtc: planning.customerConfirmedAtUtc || undefined,
				confirmationNote: planning.customerConfirmationNote || undefined,
				accessNotes: planning.accessNotes || undefined
			},
			schedule: {
				targetDate: planning.targetDate || date(start), prepDate: planning.prepDate || undefined,
				pourDate: planning.pourDate || undefined, cleanupDate: planning.cleanupDate || undefined
			},
			materials: {
				baseMaterialStatus: orderStatus(base?.status), baseMaterialSupplier: base?.supplier || undefined,
				baseMaterialDeliveryDate: base?.deliveryDate || undefined, baseMaterialDeliveryWindow: base?.deliveryWindow || undefined,
				reinforcementStatus: orderStatus(reinforcement?.status), reinforcementSupplier: reinforcement?.supplier || undefined,
				equipmentStatus: orderStatus(equipment?.status), equipmentVendor: equipment?.supplier || undefined,
				equipmentDeliveryDate: equipment?.deliveryDate || undefined, equipmentDeliveryWindow: equipment?.deliveryWindow || undefined,
				concreteStatus: orderStatus(concrete?.status), concreteSupplier: concrete?.supplier || undefined,
				concreteDeliveryDate: concrete?.deliveryDate || undefined, concreteDeliveryWindow: concrete?.deliveryWindow || undefined,
				concreteYards: concrete?.quantity || undefined, concreteMix: concrete?.specification || undefined,
				pumpNeeded: concrete?.unit === 'pump', notes: concrete?.notes || undefined
			},
			checklist, updatedAtUtc: planning.updatedAtUtc || undefined, updatedBy: planning.updatedBy || undefined
		},
		activity,
		version: job.version
	};
};

const getJob = async (jobId: string, fetcher: typeof globalThis.fetch) => {
	const response = await api(`/api/jobs/${encodeURIComponent(jobId)}`, undefined, fetcher);
	if (response.status === 404) return null;
	return unwrapTurnKeyApiEnvelope<JobApiRecord>(response, 'Load job');
};

export const getBdrInvoiceSchedulingEligibility = (
	invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'> &
		Partial<Pick<BdrInvoiceRecord, 'amountPaid' | 'balanceDue' | 'jobRelease'>>,
	settings: BdrBillingSettings
) => {
	const amountPaid = getBdrInvoiceAmountPaid(invoice);
	const balanceDue = getBdrInvoiceBalanceDue(invoice);
	const requiredDepositAmount = invoice.jobRelease?.requiredDepositAmount ?? Math.min(invoice.amount, invoice.amount * (settings.depositPercentRequired / 100));
	return {
		amountPaid, balanceDue, requiredDepositAmount,
		paidPercent: invoice.amount > 0 ? Math.min(100, (amountPaid / invoice.amount) * 100) : 0,
		isReady: invoice.jobRelease?.isEligible ?? amountPaid + 0.01 >= requiredDepositAmount
	};
};

export const loadBdrScheduledJobs = async (fetcher: typeof globalThis.fetch = fetch) => {
	const jobs: JobApiRecord[] = [];
	let continuationToken: string | null = null;
	do {
		const query = new URLSearchParams({ pageSize: '100' });
		if (continuationToken) query.set('continuationToken', continuationToken);
		const response = await api(`/api/jobs/paged?${query}`, undefined, fetcher);
		if (!response.ok) throw new Error(`Load jobs failed with ${response.status}.`);
		const page = (await response.json()) as JobPageEnvelope;
		if (!page.success || !Array.isArray(page.data)) throw new Error('Load jobs returned an invalid response.');
		jobs.push(...page.data);
		continuationToken = page.continuationToken ?? null;
	} while (continuationToken);
	return jobs.map(mapJob);
};

export const buildBdrScheduleReadyJobs = (
	invoices: BdrInvoiceRecord[], requests: QuoteRequest[], settings: BdrBillingSettings, scheduledJobs: BdrScheduledJobRecord[]
): BdrScheduleReadyJob[] => {
	const scheduledByInvoiceId = new Map(scheduledJobs.map((job) => [job.invoiceId, job]));
	return invoices.filter((invoice) => invoice.state !== 'draft').map((invoice) => {
		const request = requests.find((item) => item.id === invoice.sourceRequestId) ?? null;
		const eligibility = getBdrInvoiceSchedulingEligibility(invoice, settings);
		const scheduledJob = scheduledByInvoiceId.get(invoice.id);
		return {
			invoiceId: invoice.id, sourceRequestId: invoice.sourceRequestId, invoiceNumber: invoice.invoiceNumber,
			customerName: invoice.customerName, siteName: invoice.siteName, serviceSummary: invoice.serviceSummary,
			serviceAddress: request?.serviceAddress ?? invoice.siteName, contactName: request?.contactName ?? invoice.customerName,
			phone: request?.phone ?? invoice.customerPhone, email: request?.email ?? invoice.customerEmail, amount: invoice.amount,
			amountPaid: eligibility.amountPaid, balanceDue: eligibility.balanceDue,
			requiredDepositAmount: eligibility.requiredDepositAmount, depositPercentRequired: settings.depositPercentRequired,
			paidPercent: eligibility.paidPercent, isReady: eligibility.isReady, isScheduled: Boolean(scheduledJob), scheduledJob
		};
	}).filter((job) => job.isReady)
		.sort((a, b) => Number(a.isScheduled) - Number(b.isScheduled) || b.paidPercent - a.paidPercent);
};

export const scheduleBdrJobFromInvoice = async (
	invoice: BdrInvoiceRecord, request: QuoteRequest | null, settings: BdrBillingSettings,
	input: { scheduledDate: string; windowStart: string; windowEnd: string; crew: string; notes?: string; scheduledBy?: string },
	fetcher: typeof globalThis.fetch = fetch
) => {
	const eligibility = getBdrInvoiceSchedulingEligibility(invoice, settings);
	if (!eligibility.isReady) throw new Error('Invoice has not met the scheduling deposit gate.');
	const body = {
		id: invoice.id, name: invoice.siteName || invoice.customerName, description: invoice.serviceSummary,
		status: 'Scheduled', invoiceId: invoice.id, quoteRequestId: invoice.sourceRequestId,
		invoiceNumber: invoice.invoiceNumber, customerName: invoice.customerName, jobSiteName: invoice.siteName,
		projectAddress: request?.serviceAddress ?? invoice.siteName, projectName: invoice.serviceSummary,
		contactName: request?.contactName ?? invoice.customerName, contactPhone: request?.phone ?? invoice.customerPhone,
		contactEmail: request?.email ?? invoice.customerEmail, scheduledStart: iso(input.scheduledDate, input.windowStart),
		scheduledEnd: iso(input.scheduledDate, input.windowEnd), crew: input.crew, estimatedTotal: invoice.amount,
		paidTotal: eligibility.amountPaid, requiredDepositPercent: settings.depositPercentRequired, notes: input.notes,
		planning: { customerConfirmationStatus: 'pending', targetDate: input.scheduledDate, prepDate: input.scheduledDate, pourDate: input.scheduledDate }
	};
	return mapJob(await unwrapTurnKeyApiEnvelope<JobApiRecord>(
		await api('/api/jobs', { method: 'POST', body: JSON.stringify(body) }, fetcher), 'Create job'));
};

export const updateBdrScheduledJobStatus = async (
	jobId: string, input: { status: BdrProductionJobStatus; note?: string; actor?: string },
	fetcher: typeof globalThis.fetch = fetch
): Promise<BdrScheduledJobRecord | null> => {
	const current = await getJob(jobId, fetcher); if (!current) return null;
	return mapJob(await unwrapTurnKeyApiEnvelope<JobApiRecord>(await api(`/api/jobs/${encodeURIComponent(jobId)}/status`, {
		method: 'PUT', body: JSON.stringify({ status: apiStatus(input.status), note: input.note, expectedVersion: current.version })
	}, fetcher), 'Update job status'));
};

export const rescheduleBdrScheduledJob = async (
	jobId: string, input: { scheduledDate: string; windowStart: string; windowEnd: string; crew: string; note?: string; actor?: string },
	fetcher: typeof globalThis.fetch = fetch
): Promise<BdrScheduledJobRecord | null> => {
	const current = await getJob(jobId, fetcher); if (!current) return null;
	return mapJob(await unwrapTurnKeyApiEnvelope<JobApiRecord>(await api(`/api/jobs/${encodeURIComponent(jobId)}/schedule`, {
		method: 'PUT', body: JSON.stringify({ scheduledStart: iso(input.scheduledDate, input.windowStart),
			scheduledEnd: iso(input.scheduledDate, input.windowEnd), crew: input.crew, note: input.note, expectedVersion: current.version })
	}, fetcher), 'Reschedule job'));
};

const apiMaterial = (kind: string, status: BdrProductionJobOrderStatus, supplier?: string, deliveryDate?: string,
	deliveryWindow?: string, quantity?: number, unit?: string, specification?: string, notes?: string): JobApiMaterial =>
	({ kind, status, supplier, deliveryDate, deliveryWindow, quantity, unit, specification, notes });

export const updateBdrScheduledJobPlanning = async (
	jobId: string, input: BdrProductionJobPlanningUpdateInput, fetcher: typeof globalThis.fetch = fetch
): Promise<BdrScheduledJobRecord | null> => {
	const current = await getJob(jobId, fetcher); if (!current) return null;
	const checklist = Object.fromEntries(checklistKeys.map((key) => [key, input.checklist.includes(key)]));
	const planning = {
		customerConfirmationStatus: input.customerConfirmationStatus, customerConfirmationNote: input.customerConfirmationNote,
		accessNotes: input.accessNotes, targetDate: input.targetDate, prepDate: input.prepDate, pourDate: input.pourDate,
		cleanupDate: input.cleanupDate, checklist,
		materials: [
			apiMaterial('base-material', input.baseMaterialStatus, input.baseMaterialSupplier, input.baseMaterialDeliveryDate, input.baseMaterialDeliveryWindow),
			apiMaterial('reinforcement', input.reinforcementStatus, input.reinforcementSupplier),
			apiMaterial('equipment', input.equipmentStatus, input.equipmentVendor, input.equipmentDeliveryDate, input.equipmentDeliveryWindow),
			apiMaterial('concrete', input.concreteStatus, input.concreteSupplier, input.concreteDeliveryDate,
				input.concreteDeliveryWindow, input.concreteYards, input.pumpNeeded ? 'pump' : 'yard', input.concreteMix, input.materialNotes)
		]
	};
	return mapJob(await unwrapTurnKeyApiEnvelope<JobApiRecord>(await api(`/api/jobs/${encodeURIComponent(jobId)}/planning`, {
		method: 'PUT', body: JSON.stringify({ planning, expectedVersion: current.version })
	}, fetcher), 'Update job planning'));
};

export const addBdrScheduledJobNote = async (
	jobId: string, input: { note: string; actor?: string }, fetcher: typeof globalThis.fetch = fetch
): Promise<BdrScheduledJobRecord | null> => {
	const current = await getJob(jobId, fetcher); if (!current) return null;
	return mapJob(await unwrapTurnKeyApiEnvelope<JobApiRecord>(await api(`/api/jobs/${encodeURIComponent(jobId)}/notes`, {
		method: 'POST', body: JSON.stringify({ note: input.note, expectedVersion: current.version })
	}, fetcher), 'Add job note'));
};
