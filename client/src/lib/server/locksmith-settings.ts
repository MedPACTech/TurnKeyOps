import { getTenantSettings, updateTenantSettings } from '$lib/api/tenant-settings';
import { locksmithLaborDefaults } from '$lib/locksmith';
export type LocksmithSettings = {
 techCapabilities: Record<string, Array<'residential' | 'commercial'>>;
 pricing: { laborRatePerHour: number | null; taxPercent: number | null; maxDiscountPercent: number; approvalAboveTotal: number | null; requireOfficeApproval: boolean; laborHoursByJobType: Record<'residential' | 'commercial', Record<string, number>> };
 catalog: Array<{ id: string; name: string; jobTypes: Array<'residential' | 'commercial'>; unitPrice: number; sample: boolean; externalId?: string }>;
 selfBookingEnabled: boolean;
};
export const defaultLocksmithSettings: LocksmithSettings = {
 techCapabilities: {},
 pricing: { laborRatePerHour: null, taxPercent: null, maxDiscountPercent: 0, approvalAboveTotal: null, requireOfficeApproval: true, laborHoursByJobType: structuredClone(locksmithLaborDefaults) },
 catalog: [],
 selfBookingEnabled: false
};
export const loadLocksmithSettings = async (fetcher: typeof fetch, accessToken?: string | null) => {
 const document = await getTenantSettings<Record<string, unknown> & { locksmith?: LocksmithSettings }>('operational', fetcher, accessToken);
 const stored = document.values.locksmith;
 return { document, settings: stored ? {
  ...structuredClone(defaultLocksmithSettings), ...stored,
  pricing: { ...defaultLocksmithSettings.pricing, ...stored.pricing,
   laborHoursByJobType: {
    residential: { ...locksmithLaborDefaults.residential, ...stored.pricing?.laborHoursByJobType?.residential },
    commercial: { ...locksmithLaborDefaults.commercial, ...stored.pricing?.laborHoursByJobType?.commercial }
   }
  },
  catalog: Array.isArray(stored.catalog) ? stored.catalog : []
 } : structuredClone(defaultLocksmithSettings) };
};
export const saveLocksmithSettings = async (settings: LocksmithSettings, version: string, fetcher: typeof fetch, accessToken?: string | null) => {
 const { document } = await loadLocksmithSettings(fetcher, accessToken);
 if (document.version !== version) throw new Error('Settings changed since you opened this page. Reload before saving.');
 if (document.configuredSecretKeys.length) throw new Error('Operational settings contain secret references; use the full settings editor to preserve them.');
 return updateTenantSettings('operational', { ...document.values, locksmith: settings }, version, fetcher, accessToken);
};
