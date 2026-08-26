import type { QuoteRequest } from '$lib/quote-requests';
import type { BdrBillingSettings } from '$lib/server/bdr-billing-settings';
import {
	getBdrInvoiceAmountPaid,
	getBdrInvoiceBalanceDue,
	type BdrInvoiceRecord
} from '$lib/server/bdr-invoices';

const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
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
};

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
	schedule: {
		targetDate: string;
		prepDate?: string;
		pourDate?: string;
		cleanupDate?: string;
	};
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

const getStoreDir = () => `${getCwd()}/.svelte-kit`;
const getStorePath = () => `${getStoreDir()}/local-bdr-scheduled-jobs.json`;
const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const normalizeTime = (value: unknown, fallback: string) => {
	const normalized = String(value ?? '').trim();
	return /^\d{2}:\d{2}$/.test(normalized) ? normalized : fallback;
};

const normalizeDate = (value: unknown) => {
	const normalized = String(value ?? '').trim();
	return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : '';
};

const normalizeJobStatus = (value: unknown): BdrProductionJobStatus => {
	const normalized = String(value ?? '').trim();
	if (normalized === 'in-progress' || normalized === 'on-hold' || normalized === 'completed' || normalized === 'cancelled') {
		return normalized;
	}
	return 'scheduled';
};

const normalizeConfirmationStatus = (value: unknown): BdrProductionJobConfirmationStatus => {
	const normalized = String(value ?? '').trim();
	if (normalized === 'confirmed' || normalized === 'needs-reschedule') return normalized;
	return 'pending';
};

const normalizeOrderStatus = (value: unknown): BdrProductionJobOrderStatus => {
	const normalized = String(value ?? '').trim();
	if (normalized === 'requested' || normalized === 'ordered' || normalized === 'confirmed' || normalized === 'delivered') {
		return normalized;
	}
	return 'not-started';
};

const checklistKeys: BdrProductionJobChecklistKey[] = [
	'customer-confirmed',
	'site-access',
	'utility-locate',
	'base-material-ordered',
	'equipment-reserved',
	'concrete-ordered',
	'forms-reinforcement',
	'weather-check',
	'pour-confirmed',
	'cleanup-walkthrough'
];

const defaultChecklist = (): Record<BdrProductionJobChecklistKey, boolean> =>
	Object.fromEntries(checklistKeys.map((key) => [key, false])) as Record<BdrProductionJobChecklistKey, boolean>;

const normalizeChecklist = (value: unknown): Record<BdrProductionJobChecklistKey, boolean> => {
	const checklist = defaultChecklist();
	if (value && typeof value === 'object') {
		for (const key of checklistKeys) {
			checklist[key] = Boolean((value as Partial<Record<BdrProductionJobChecklistKey, boolean>>)[key]);
		}
	}
	return checklist;
};

const normalizeOptionalNumber = (value: unknown) => {
	const amount = typeof value === 'number' ? value : Number.parseFloat(String(value ?? '').trim());
	return Number.isFinite(amount) && amount > 0 ? amount : undefined;
};

const buildDefaultPlanning = (scheduledDate: string, actor = 'Office admin'): BdrProductionJobPlanning => ({
	customer: {
		confirmationStatus: 'pending'
	},
	schedule: {
		targetDate: scheduledDate,
		prepDate: scheduledDate,
		pourDate: scheduledDate
	},
	materials: {
		baseMaterialStatus: 'not-started',
		reinforcementStatus: 'not-started',
		equipmentStatus: 'not-started',
		concreteStatus: 'not-started',
		pumpNeeded: false
	},
	checklist: defaultChecklist(),
	updatedAtUtc: new Date().toISOString(),
	updatedBy: actor
});

const normalizePlanning = (
	value: unknown,
	fallback: { scheduledDate: string; actor?: string }
): BdrProductionJobPlanning => {
	if (!value || typeof value !== 'object') return buildDefaultPlanning(fallback.scheduledDate, fallback.actor);
	const record = value as Partial<BdrProductionJobPlanning>;
	const defaultPlanning = buildDefaultPlanning(fallback.scheduledDate, fallback.actor);
	const materials = (record.materials ?? {}) as Partial<BdrProductionJobPlanning['materials']>;
	return {
		customer: {
			confirmationStatus: normalizeConfirmationStatus(record.customer?.confirmationStatus),
			confirmedAtUtc: record.customer?.confirmedAtUtc?.trim() || undefined,
			confirmationNote: record.customer?.confirmationNote?.trim() || undefined,
			accessNotes: record.customer?.accessNotes?.trim() || undefined
		},
		schedule: {
			targetDate: normalizeDate(record.schedule?.targetDate) || fallback.scheduledDate,
			prepDate: normalizeDate(record.schedule?.prepDate) || undefined,
			pourDate: normalizeDate(record.schedule?.pourDate) || undefined,
			cleanupDate: normalizeDate(record.schedule?.cleanupDate) || undefined
		},
		materials: {
			baseMaterialStatus: normalizeOrderStatus(materials.baseMaterialStatus),
			baseMaterialSupplier: materials.baseMaterialSupplier?.trim() || undefined,
			baseMaterialDeliveryDate: normalizeDate(materials.baseMaterialDeliveryDate) || undefined,
			baseMaterialDeliveryWindow: materials.baseMaterialDeliveryWindow?.trim() || undefined,
			reinforcementStatus: normalizeOrderStatus(materials.reinforcementStatus),
			reinforcementSupplier: materials.reinforcementSupplier?.trim() || undefined,
			equipmentStatus: normalizeOrderStatus(materials.equipmentStatus),
			equipmentVendor: materials.equipmentVendor?.trim() || undefined,
			equipmentDeliveryDate: normalizeDate(materials.equipmentDeliveryDate) || undefined,
			equipmentDeliveryWindow: materials.equipmentDeliveryWindow?.trim() || undefined,
			concreteStatus: normalizeOrderStatus(materials.concreteStatus),
			concreteSupplier: materials.concreteSupplier?.trim() || undefined,
			concreteDeliveryDate: normalizeDate(materials.concreteDeliveryDate) || undefined,
			concreteDeliveryWindow: materials.concreteDeliveryWindow?.trim() || undefined,
			concreteYards: normalizeOptionalNumber(materials.concreteYards),
			concreteMix: materials.concreteMix?.trim() || undefined,
			pumpNeeded: Boolean(materials.pumpNeeded),
			notes: materials.notes?.trim() || undefined
		},
		checklist: {
			...defaultPlanning.checklist,
			...normalizeChecklist(record.checklist)
		},
		updatedAtUtc: record.updatedAtUtc?.trim() || undefined,
		updatedBy: record.updatedBy?.trim() || undefined
	};
};

const buildActivity = (
	type: BdrProductionJobActivity['type'],
	label: string,
	actor: string,
	note?: string,
	occurredAtUtc = new Date().toISOString()
): BdrProductionJobActivity => ({
	id: `${type}-${occurredAtUtc}-${Math.random().toString(16).slice(2)}`,
	type,
	label,
	occurredAtUtc,
	actor: actor.trim() || 'Office admin',
	note: note?.trim() || undefined
});

const normalizeActivity = (value: unknown): BdrProductionJobActivity | null => {
	if (!value || typeof value !== 'object') return null;
	const record = value as Partial<BdrProductionJobActivity>;
	const type =
		record.type === 'status-updated' ||
		record.type === 'rescheduled' ||
		record.type === 'planning-updated' ||
		record.type === 'note'
			? record.type
			: 'scheduled';
	const occurredAtUtc = String(record.occurredAtUtc ?? '').trim();
	return {
		id: String(record.id ?? `${type}-${occurredAtUtc || Date.now()}`).trim(),
		type,
		label: String(record.label ?? '').trim() || 'Job updated',
		occurredAtUtc: occurredAtUtc || new Date().toISOString(),
		actor: String(record.actor ?? 'Office admin').trim(),
		note: record.note?.trim() || undefined
	};
};

const normalizeScheduledJob = (value: unknown): BdrScheduledJobRecord | null => {
	if (!value || typeof value !== 'object') return null;
	const record = value as Partial<BdrScheduledJobRecord>;
	const invoiceId = String(record.invoiceId ?? '').trim();
	if (!invoiceId) return null;
	const now = new Date().toISOString();
	const scheduledAtUtc = String(record.scheduledAtUtc ?? '').trim() || now;
	const scheduledDate = String(record.scheduledDate ?? '').trim() || now.slice(0, 10);
	const activity = Array.isArray(record.activity)
		? record.activity.map(normalizeActivity).filter((item): item is BdrProductionJobActivity => Boolean(item))
		: [];
	return {
		id: String(record.id ?? `job-${invoiceId}`).trim(),
		invoiceId,
		sourceRequestId: String(record.sourceRequestId ?? '').trim(),
		invoiceNumber: String(record.invoiceNumber ?? '').trim(),
		customerName: String(record.customerName ?? '').trim(),
		siteName: String(record.siteName ?? '').trim(),
		serviceSummary: String(record.serviceSummary ?? '').trim(),
		serviceAddress: String(record.serviceAddress ?? '').trim(),
		contactName: String(record.contactName ?? '').trim(),
		phone: String(record.phone ?? '').trim(),
		email: String(record.email ?? '').trim(),
		amount: Number.isFinite(Number(record.amount)) ? Number(record.amount) : 0,
		amountPaidAtScheduling: Number.isFinite(Number(record.amountPaidAtScheduling)) ? Number(record.amountPaidAtScheduling) : 0,
		depositPercentRequired: Number.isFinite(Number(record.depositPercentRequired)) ? Number(record.depositPercentRequired) : 50,
		scheduledDate,
		windowStart: normalizeTime(record.windowStart, '08:00'),
		windowEnd: normalizeTime(record.windowEnd, '12:00'),
		crew: String(record.crew ?? 'Production crew').trim(),
		notes: record.notes?.trim() || undefined,
		status: normalizeJobStatus(record.status),
		scheduledAtUtc,
		scheduledBy: String(record.scheduledBy ?? 'Office admin').trim(),
		updatedAtUtc: String(record.updatedAtUtc ?? '').trim() || scheduledAtUtc,
		completedAtUtc: record.completedAtUtc?.trim() || undefined,
		cancelledAtUtc: record.cancelledAtUtc?.trim() || undefined,
		holdReason: record.holdReason?.trim() || undefined,
		planning: normalizePlanning(record.planning, {
			scheduledDate,
			actor: String(record.scheduledBy ?? 'Office admin')
		}),
		activity:
			activity.length > 0
				? activity
				: [
						buildActivity(
							'scheduled',
							`Scheduled for ${scheduledDate}`,
							String(record.scheduledBy ?? 'Office admin'),
							record.notes,
							scheduledAtUtc
						)
					]
	};
};

const findRequest = (requests: QuoteRequest[], invoice: Pick<BdrInvoiceRecord, 'sourceRequestId'>) =>
	requests.find((request) => request.id === invoice.sourceRequestId) ?? null;

export const getBdrInvoiceSchedulingEligibility = (
	invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'> &
		Partial<Pick<BdrInvoiceRecord, 'amountPaid' | 'balanceDue' | 'jobRelease'>>,
	settings: BdrBillingSettings
) => {
	const amountPaid = getBdrInvoiceAmountPaid(invoice);
	const balanceDue = getBdrInvoiceBalanceDue(invoice);
	const requiredDepositAmount = invoice.jobRelease
		? invoice.jobRelease.requiredDepositAmount
		: Math.min(invoice.amount, invoice.amount * (settings.depositPercentRequired / 100));
	const paidPercent = invoice.amount > 0 ? Math.min(100, (amountPaid / invoice.amount) * 100) : 0;
	return {
		amountPaid,
		balanceDue,
		requiredDepositAmount,
		paidPercent,
		isReady: invoice.jobRelease?.isEligible ?? amountPaid + 0.01 >= requiredDepositAmount
	};
};

export const loadBdrScheduledJobs = async () => {
	try {
		const fs = await getFs();
		const raw = await fs.readFile(getStorePath(), 'utf-8');
		const parsed = JSON.parse(raw) as unknown[];
		if (!Array.isArray(parsed)) return [];
		return parsed.map(normalizeScheduledJob).filter((record): record is BdrScheduledJobRecord => Boolean(record));
	} catch {
		return [];
	}
};

const saveBdrScheduledJobs = async (jobs: BdrScheduledJobRecord[]) => {
	const fs = await getFs();
	await fs.mkdir(getStoreDir(), { recursive: true });
	await fs.writeFile(getStorePath(), JSON.stringify(jobs, null, 2));
};

export const buildBdrScheduleReadyJobs = (
	invoices: BdrInvoiceRecord[],
	requests: QuoteRequest[],
	settings: BdrBillingSettings,
	scheduledJobs: BdrScheduledJobRecord[]
): BdrScheduleReadyJob[] => {
	const scheduledByInvoiceId = new Map(scheduledJobs.map((job) => [job.invoiceId, job]));
	return invoices
		.filter((invoice) => invoice.state !== 'draft')
		.map((invoice) => {
			const request = findRequest(requests, invoice);
			const eligibility = getBdrInvoiceSchedulingEligibility(invoice, settings);
			const scheduledJob = scheduledByInvoiceId.get(invoice.id);
			return {
				invoiceId: invoice.id,
				sourceRequestId: invoice.sourceRequestId,
				invoiceNumber: invoice.invoiceNumber,
				customerName: invoice.customerName,
				siteName: invoice.siteName,
				serviceSummary: invoice.serviceSummary,
				serviceAddress: request?.serviceAddress ?? invoice.siteName,
				contactName: request?.contactName ?? invoice.customerName,
				phone: request?.phone ?? invoice.customerPhone,
				email: request?.email ?? invoice.customerEmail,
				amount: invoice.amount,
				amountPaid: eligibility.amountPaid,
				balanceDue: eligibility.balanceDue,
				requiredDepositAmount: eligibility.requiredDepositAmount,
				depositPercentRequired: settings.depositPercentRequired,
				paidPercent: eligibility.paidPercent,
				isReady: eligibility.isReady,
				isScheduled: Boolean(scheduledJob),
				scheduledJob
			};
		})
		.filter((job) => job.isReady)
		.sort((a, b) => Number(a.isScheduled) - Number(b.isScheduled) || b.paidPercent - a.paidPercent);
};

export const scheduleBdrJobFromInvoice = async (
	invoice: BdrInvoiceRecord,
	request: QuoteRequest | null,
	settings: BdrBillingSettings,
	input: {
		scheduledDate: string;
		windowStart: string;
		windowEnd: string;
		crew: string;
		notes?: string;
		scheduledBy?: string;
	}
) => {
	const now = new Date().toISOString();
	const eligibility = getBdrInvoiceSchedulingEligibility(invoice, settings);
	if (!eligibility.isReady) {
		throw new Error('Invoice has not met the scheduling deposit gate.');
	}
	const job: BdrScheduledJobRecord = {
		id: `job-${invoice.id}`,
		invoiceId: invoice.id,
		sourceRequestId: invoice.sourceRequestId,
		invoiceNumber: invoice.invoiceNumber,
		customerName: invoice.customerName,
		siteName: invoice.siteName,
		serviceSummary: invoice.serviceSummary,
		serviceAddress: request?.serviceAddress ?? invoice.siteName,
		contactName: request?.contactName ?? invoice.customerName,
		phone: request?.phone ?? invoice.customerPhone,
		email: request?.email ?? invoice.customerEmail,
		amount: invoice.amount,
		amountPaidAtScheduling: eligibility.amountPaid,
		depositPercentRequired: settings.depositPercentRequired,
		scheduledDate: input.scheduledDate,
		windowStart: normalizeTime(input.windowStart, '08:00'),
		windowEnd: normalizeTime(input.windowEnd, '12:00'),
		crew: input.crew.trim() || 'Production crew',
		notes: input.notes?.trim() || undefined,
		status: 'scheduled',
		scheduledAtUtc: now,
		scheduledBy: input.scheduledBy?.trim() || 'Office admin',
		updatedAtUtc: now,
		planning: buildDefaultPlanning(input.scheduledDate, input.scheduledBy?.trim() || 'Office admin'),
		activity: [
			buildActivity(
				'scheduled',
				`Scheduled for ${input.scheduledDate}`,
				input.scheduledBy?.trim() || 'Office admin',
				input.notes
			)
		]
	};
	const existing = await loadBdrScheduledJobs();
	const next = [job, ...existing.filter((item) => item.invoiceId !== invoice.id)];
	await saveBdrScheduledJobs(next);
	return job;
};

export const updateBdrScheduledJobStatus = async (
	jobId: string,
	input: {
		status: BdrProductionJobStatus;
		note?: string;
		actor?: string;
	}
): Promise<BdrScheduledJobRecord | null> => {
	const existing = await loadBdrScheduledJobs();
	const now = new Date().toISOString();
	let updatedJob: BdrScheduledJobRecord | null = null;
	const next = existing.map((job) => {
		if (job.id !== jobId) return job;
		const nextJob: BdrScheduledJobRecord = {
			...job,
			status: input.status,
			updatedAtUtc: now,
			completedAtUtc: input.status === 'completed' ? now : job.completedAtUtc,
			cancelledAtUtc: input.status === 'cancelled' ? now : job.cancelledAtUtc,
			holdReason: input.status === 'on-hold' ? input.note?.trim() || job.holdReason : undefined,
			activity: [
				buildActivity('status-updated', `Status changed to ${input.status.replace('-', ' ')}`, input.actor ?? 'Office admin', input.note),
				...job.activity
			]
		};
		updatedJob = nextJob;
		return nextJob;
	});
	await saveBdrScheduledJobs(next);
	return updatedJob;
};

export const rescheduleBdrScheduledJob = async (
	jobId: string,
	input: {
		scheduledDate: string;
		windowStart: string;
		windowEnd: string;
		crew: string;
		note?: string;
		actor?: string;
	}
): Promise<BdrScheduledJobRecord | null> => {
	const existing = await loadBdrScheduledJobs();
	const now = new Date().toISOString();
	let updatedJob: BdrScheduledJobRecord | null = null;
	const next = existing.map((job) => {
		if (job.id !== jobId) return job;
		const planning = normalizePlanning(job.planning, { scheduledDate: job.scheduledDate, actor: job.scheduledBy });
		const nextJob: BdrScheduledJobRecord = {
			...job,
			scheduledDate: input.scheduledDate,
			windowStart: normalizeTime(input.windowStart, job.windowStart),
			windowEnd: normalizeTime(input.windowEnd, job.windowEnd),
			crew: input.crew.trim() || job.crew,
			notes: input.note?.trim() || job.notes,
			updatedAtUtc: now,
			planning: {
				...planning,
				schedule: {
					...planning.schedule,
					targetDate: input.scheduledDate,
					prepDate: planning.schedule.prepDate || input.scheduledDate,
					pourDate: planning.schedule.pourDate || input.scheduledDate
				},
				updatedAtUtc: now,
				updatedBy: input.actor?.trim() || 'Office admin'
			},
			activity: [
				buildActivity('rescheduled', `Rescheduled for ${input.scheduledDate}`, input.actor ?? 'Office admin', input.note),
				...job.activity
			]
		};
		updatedJob = nextJob;
		return nextJob;
	});
	await saveBdrScheduledJobs(next);
	return updatedJob;
};

const materialIsOrdered = (status: BdrProductionJobOrderStatus) =>
	status === 'ordered' || status === 'confirmed' || status === 'delivered';

export const updateBdrScheduledJobPlanning = async (
	jobId: string,
	input: BdrProductionJobPlanningUpdateInput
): Promise<BdrScheduledJobRecord | null> => {
	const existing = await loadBdrScheduledJobs();
	const now = new Date().toISOString();
	const actor = input.actor?.trim() || 'Office admin';
	let updatedJob: BdrScheduledJobRecord | null = null;
	const next = existing.map((job) => {
		if (job.id !== jobId) return job;
		const checklist = defaultChecklist();
		for (const key of input.checklist) {
			checklist[key] = true;
		}
		checklist['customer-confirmed'] = checklist['customer-confirmed'] || input.customerConfirmationStatus === 'confirmed';
		checklist['base-material-ordered'] =
			checklist['base-material-ordered'] || materialIsOrdered(input.baseMaterialStatus);
		checklist['equipment-reserved'] = checklist['equipment-reserved'] || materialIsOrdered(input.equipmentStatus);
		checklist['concrete-ordered'] = checklist['concrete-ordered'] || materialIsOrdered(input.concreteStatus);

		const planning: BdrProductionJobPlanning = {
			customer: {
				confirmationStatus: input.customerConfirmationStatus,
				confirmedAtUtc:
					input.customerConfirmationStatus === 'confirmed'
						? job.planning.customer.confirmedAtUtc ?? now
						: undefined,
				confirmationNote: input.customerConfirmationNote?.trim() || undefined,
				accessNotes: input.accessNotes?.trim() || undefined
			},
			schedule: {
				targetDate: normalizeDate(input.targetDate) || job.scheduledDate,
				prepDate: normalizeDate(input.prepDate) || undefined,
				pourDate: normalizeDate(input.pourDate) || undefined,
				cleanupDate: normalizeDate(input.cleanupDate) || undefined
			},
			materials: {
				baseMaterialStatus: input.baseMaterialStatus,
				baseMaterialSupplier: input.baseMaterialSupplier?.trim() || undefined,
				baseMaterialDeliveryDate: normalizeDate(input.baseMaterialDeliveryDate) || undefined,
				baseMaterialDeliveryWindow: input.baseMaterialDeliveryWindow?.trim() || undefined,
				reinforcementStatus: input.reinforcementStatus,
				reinforcementSupplier: input.reinforcementSupplier?.trim() || undefined,
				equipmentStatus: input.equipmentStatus,
				equipmentVendor: input.equipmentVendor?.trim() || undefined,
				equipmentDeliveryDate: normalizeDate(input.equipmentDeliveryDate) || undefined,
				equipmentDeliveryWindow: input.equipmentDeliveryWindow?.trim() || undefined,
				concreteStatus: input.concreteStatus,
				concreteSupplier: input.concreteSupplier?.trim() || undefined,
				concreteDeliveryDate: normalizeDate(input.concreteDeliveryDate) || undefined,
				concreteDeliveryWindow: input.concreteDeliveryWindow?.trim() || undefined,
				concreteYards: normalizeOptionalNumber(input.concreteYards),
				concreteMix: input.concreteMix?.trim() || undefined,
				pumpNeeded: Boolean(input.pumpNeeded),
				notes: input.materialNotes?.trim() || undefined
			},
			checklist,
			updatedAtUtc: now,
			updatedBy: actor
		};
		const completeCount = checklistKeys.filter((key) => checklist[key]).length;
		const nextJob: BdrScheduledJobRecord = {
			...job,
			planning,
			updatedAtUtc: now,
			activity: [
				buildActivity(
					'planning-updated',
					`Job plan updated (${completeCount}/${checklistKeys.length})`,
					actor,
					input.materialNotes || input.customerConfirmationNote
				),
				...job.activity
			]
		};
		updatedJob = nextJob;
		return nextJob;
	});
	await saveBdrScheduledJobs(next);
	return updatedJob;
};

export const addBdrScheduledJobNote = async (
	jobId: string,
	input: {
		note: string;
		actor?: string;
	}
): Promise<BdrScheduledJobRecord | null> => {
	const note = input.note.trim();
	if (!note) return null;
	const existing = await loadBdrScheduledJobs();
	const now = new Date().toISOString();
	let updatedJob: BdrScheduledJobRecord | null = null;
	const next = existing.map((job) => {
		if (job.id !== jobId) return job;
		const nextJob: BdrScheduledJobRecord = {
			...job,
			notes: note,
			updatedAtUtc: now,
			activity: [buildActivity('note', 'Job note added', input.actor ?? 'Office admin', note), ...job.activity]
		};
		updatedJob = nextJob;
		return nextJob;
	});
	await saveBdrScheduledJobs(next);
	return updatedJob;
};
