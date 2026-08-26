import { getTenantSettings, updateTenantSettings } from '$lib/api/tenant-settings';

export type BdrBillingSettings = {
	depositPercentRequired: number;
};

export const defaultBdrBillingSettings: BdrBillingSettings = {
	depositPercentRequired: 50
};

const normalizeBillingSettings = (value: unknown): BdrBillingSettings => {
	if (!value || typeof value !== 'object') return { ...defaultBdrBillingSettings };
	const rawPercent = Number((value as Partial<BdrBillingSettings>).depositPercentRequired);
	return {
		depositPercentRequired: Number.isFinite(rawPercent)
			? Math.min(100, Math.max(0, rawPercent))
			: defaultBdrBillingSettings.depositPercentRequired
	};
};

export const loadBdrBillingSettings = async (
	fetcher: typeof globalThis.fetch = globalThis.fetch,
	accessToken?: string | null
): Promise<BdrBillingSettings> => {
	const document = await getTenantSettings<BdrBillingSettings>('billing', fetcher, accessToken);
	return normalizeBillingSettings(document.values);
};

export const saveBdrBillingSettings = async (
	value: unknown,
	fetcher: typeof globalThis.fetch = globalThis.fetch,
	accessToken?: string | null
): Promise<BdrBillingSettings> => {
	const current = await getTenantSettings<BdrBillingSettings>('billing', fetcher, accessToken);
	const saved = await updateTenantSettings(
		'billing',
		normalizeBillingSettings(value),
		current.version,
		fetcher,
		accessToken
	);
	return normalizeBillingSettings(saved.values);
};
