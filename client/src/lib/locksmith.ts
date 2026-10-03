/** Shared doors/locksmith trade vocabulary. Prices below are illustrative, never issued pricing. */
export type JobType = 'residential' | 'commercial';
export const jobTypeLabels: Record<JobType, string> = { residential: 'Residential', commercial: 'Commercial' };
export const locksmithServices = ['Door and frame replacement', 'Lock and hardware installation', 'Repairs', 'Rekeying'];
/** Provisional per-opening hours shown until Carl Zipf confirms its service standards. */
export const locksmithLaborDefaults: Record<JobType, Record<string, number>> = {
	residential: { 'Door and frame replacement': 4, 'Lock and hardware installation': 1.5, Repairs: 2, Rekeying: 1 },
	commercial: { 'Door and frame replacement': 6, 'Lock and hardware installation': 2, Repairs: 3, Rekeying: 1.5 }
};

export type LocksmithCatalogItem = {
	id: string;
	name: string;
	category: 'door' | 'frame' | 'lock' | 'hardware' | 'service';
	jobTypes: JobType[];
	unitPrice: number;
	unit: 'each' | 'service';
	sample: true;
	externalId?: string;
};

export const locksmithCatalog: LocksmithCatalogItem[] = [
	{ id: 'sample-res-entry', name: 'Residential entry door', category: 'door', jobTypes: ['residential'], unitPrice: 450, unit: 'each', sample: true },
	{ id: 'sample-res-frame', name: 'Residential replacement frame', category: 'frame', jobTypes: ['residential'], unitPrice: 165, unit: 'each', sample: true },
	{ id: 'sample-deadbolt', name: 'Single-cylinder deadbolt', category: 'lock', jobTypes: ['residential'], unitPrice: 65, unit: 'each', sample: true },
	{ id: 'sample-entry-lever', name: 'Residential entry lever', category: 'lock', jobTypes: ['residential'], unitPrice: 80, unit: 'each', sample: true },
	{ id: 'sample-commercial-door', name: 'Commercial hollow-metal door', category: 'door', jobTypes: ['commercial'], unitPrice: 650, unit: 'each', sample: true },
	{ id: 'sample-commercial-frame', name: 'Commercial steel frame', category: 'frame', jobTypes: ['commercial'], unitPrice: 280, unit: 'each', sample: true },
	{ id: 'sample-commercial-lever', name: 'Commercial cylindrical lockset', category: 'lock', jobTypes: ['commercial'], unitPrice: 195, unit: 'each', sample: true },
	{ id: 'sample-closer', name: 'Surface-mounted closer', category: 'hardware', jobTypes: ['commercial'], unitPrice: 175, unit: 'each', sample: true },
	{ id: 'sample-exit-device', name: 'Rim exit device', category: 'hardware', jobTypes: ['commercial'], unitPrice: 325, unit: 'each', sample: true },
	{ id: 'sample-hinge', name: 'Replacement hinge', category: 'hardware', jobTypes: ['residential', 'commercial'], unitPrice: 18, unit: 'each', sample: true },
	{ id: 'sample-rekey', name: 'Rekey one cylinder', category: 'service', jobTypes: ['residential', 'commercial'], unitPrice: 25, unit: 'service', sample: true }
];

export const isJobType = (value: unknown): value is JobType => value === 'residential' || value === 'commercial';
export const catalogForJobType = (jobType: JobType) => locksmithCatalog.filter((item) => item.jobTypes.includes(jobType));
export const canHandleJobType = (capabilities: readonly JobType[], jobType: JobType) => capabilities.includes(jobType);

export type TechnicianContext = { tenantId: string; userId: string; capabilities: JobType[] };

/** Validate untrusted API context before it can scope browser drafts or unlock a quoting surface. */
export const parseTechnicianContext = (value: unknown, expectedTenantId: string): TechnicianContext | null => {
	if (!value || typeof value !== 'object') return null;
	const candidate = value as Record<string, unknown>;
	if (candidate.tenantId !== expectedTenantId || typeof candidate.userId !== 'string' || !candidate.userId.trim()) return null;
	if (!Array.isArray(candidate.capabilities) || !candidate.capabilities.every(isJobType)) return null;
	return { tenantId: expectedTenantId, userId: candidate.userId, capabilities: [...new Set(candidate.capabilities)] };
};
