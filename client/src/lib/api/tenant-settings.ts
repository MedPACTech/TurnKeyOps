import { apiRequest } from './client';

export type TenantSettingKind = 'public-content' | 'billing' | 'operational' | 'brand';

export type TenantSettingsDocument<T extends object> = {
	kind: TenantSettingKind;
	schemaVersion: number;
	isPublic: boolean;
	values: T;
	configuredSecretKeys: string[];
	version: string;
	updatedUtc: string;
};

export type ContactAccessRole = 'none' | 'field' | 'office-admin' | 'owner';

export type ContactAccessGrant = {
	contactId: string;
	role: ContactAccessRole;
	enabled: boolean;
	version: string;
	updatedUtc: string;
};

export const getPublicTenantContent = <T extends object>(
	tenantId: string,
	fetcher?: typeof globalThis.fetch
) =>
	apiRequest<TenantSettingsDocument<T>>(
		`/api/public/tenant-settings/${encodeURIComponent(tenantId)}/content`,
		{ method: 'GET' },
		fetcher
	);

export const getTenantSettings = <T extends object>(
	kind: Exclude<TenantSettingKind, 'public-content'>,
	fetcher?: typeof globalThis.fetch,
	accessToken?: string | null
) =>
	apiRequest<TenantSettingsDocument<T>>(
		`/api/admin/tenant-settings/${kind}`,
		{ method: 'GET' },
		fetcher,
		accessToken
	);

export const updateTenantSettings = <T extends object>(
	kind: TenantSettingKind,
	values: T,
	expectedVersion: string | null | undefined,
	fetcher?: typeof globalThis.fetch,
	accessToken?: string | null,
	secretReferences: Record<string, string> = {}
) =>
	apiRequest<TenantSettingsDocument<T>>(
		`/api/admin/tenant-settings/${kind}`,
		{
			method: 'PUT',
			body: JSON.stringify({
				schemaVersion: 1,
				values,
				secretReferences,
				expectedVersion: expectedVersion || null
			})
		},
		fetcher,
		accessToken
	);

export const listContactAccessGrants = (
	fetcher?: typeof globalThis.fetch,
	accessToken?: string | null
) =>
	apiRequest<ContactAccessGrant[]>(
		'/api/admin/contact-access',
		{ method: 'GET' },
		fetcher,
		accessToken
	);

export const updateContactAccessGrant = (
	contactId: string,
	role: ContactAccessRole,
	expectedVersion: string | null | undefined,
	fetcher?: typeof globalThis.fetch,
	accessToken?: string | null
) =>
	apiRequest<ContactAccessGrant>(
		`/api/admin/contact-access/${encodeURIComponent(contactId)}`,
		{
			method: 'PUT',
			body: JSON.stringify({ role, enabled: role !== 'none', expectedVersion: expectedVersion || null })
		},
		fetcher,
		accessToken
	);
