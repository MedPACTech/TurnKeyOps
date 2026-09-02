import {
	listContactAccessGrants,
	updateContactAccessGrant,
	type ContactAccessRole
} from '$lib/api/tenant-settings';

export const bdrContactAccessRoles = ['none', 'field', 'office-admin', 'owner'] as const;
export type BdrContactAccessRole = (typeof bdrContactAccessRoles)[number];

export const isBdrContactAccessRole = (
	value: string | null | undefined
): value is BdrContactAccessRole => bdrContactAccessRoles.includes(value as BdrContactAccessRole);

export const isBdrAdminContactAccessRole = (
	value: string | null | undefined
): value is 'office-admin' | 'owner' => value === 'office-admin' || value === 'owner';

export const loadBdrContactAccessRoles = async (
	fetcher: typeof globalThis.fetch = globalThis.fetch,
	accessToken?: string | null
): Promise<Record<string, BdrContactAccessRole>> => {
	const grants = await listContactAccessGrants(fetcher, accessToken);
	return Object.fromEntries(
		grants
			.filter((grant) => grant.enabled && isBdrContactAccessRole(grant.role))
			.map((grant) => [grant.contactId, grant.role])
	);
};

export const saveBdrContactAccessRole = async (
	contactId: string,
	role: BdrContactAccessRole,
	fetcher: typeof globalThis.fetch = globalThis.fetch,
	accessToken?: string | null
) => {
	const trimmedContactId = contactId.trim();
	if (!trimmedContactId) throw new Error('Contact id is required.');

	const grants = await listContactAccessGrants(fetcher, accessToken);
	const current = grants.find((grant) => grant.contactId === trimmedContactId);
	await updateContactAccessGrant(
		trimmedContactId,
		role as ContactAccessRole,
		current?.version,
		fetcher,
		accessToken
	);
	return loadBdrContactAccessRoles(fetcher, accessToken);
};

export const getPersistedBdrAdminRole = async (
	contactId: string | undefined,
	fetcher: typeof globalThis.fetch = globalThis.fetch,
	accessToken?: string | null
) => {
	if (!contactId || !accessToken) return null;
	const roles = await loadBdrContactAccessRoles(fetcher, accessToken);
	const role = roles[contactId];
	return isBdrAdminContactAccessRole(role) ? role : null;
};
