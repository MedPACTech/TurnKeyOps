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
	activity: BdrProductionJobActivity[];
};

export type BdrProductionJobStatus = 'scheduled' | 'in-progress' | 'on-hold' | 'completed' | 'cancelled';

export type BdrProductionJobActivity = {
	id: string;
	type: 'scheduled' | 'status-updated' | 'rescheduled' | 'note';
	label: string;
	occurredAtUtc: string;
	actor: string;
	note?: string;
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

const normalizeJobStatus = (value: unknown): BdrProductionJobStatus => {
	const normalized = String(value ?? '').trim();
	if (normalized === 'in-progress' || normalized === 'on-hold' || normalized === 'completed' || normalized === 'cancelled') {
		return normalized;
	}
	return 'scheduled';
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
		record.type === 'status-updated' || record.type === 'rescheduled' || record.type === 'note'
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
		scheduledDate: String(record.scheduledDate ?? '').trim() || now.slice(0, 10),
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
		activity:
			activity.length > 0
				? activity
				: [
						buildActivity(
							'scheduled',
							`Scheduled for ${String(record.scheduledDate ?? '').trim() || now.slice(0, 10)}`,
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
	invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'>,
	settings: BdrBillingSettings
) => {
	const amountPaid = getBdrInvoiceAmountPaid(invoice);
	const balanceDue = getBdrInvoiceBalanceDue(invoice);
	const requiredDepositAmount = Math.min(invoice.amount, invoice.amount * (settings.depositPercentRequired / 100));
	const paidPercent = invoice.amount > 0 ? Math.min(100, (amountPaid / invoice.amount) * 100) : 0;
	return {
		amountPaid,
		balanceDue,
		requiredDepositAmount,
		paidPercent,
		isReady: amountPaid + 0.01 >= requiredDepositAmount
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
		const nextJob: BdrScheduledJobRecord = {
			...job,
			scheduledDate: input.scheduledDate,
			windowStart: normalizeTime(input.windowStart, job.windowStart),
			windowEnd: normalizeTime(input.windowEnd, job.windowEnd),
			crew: input.crew.trim() || job.crew,
			notes: input.note?.trim() || job.notes,
			updatedAtUtc: now,
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
