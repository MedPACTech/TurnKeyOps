import { getAuthApiBaseUrl } from './auth-session';

export type ManagedTenantUser = {
	membershipId: string;
	inviteId?: string | null;
	userId?: string | null;
	email?: string | null;
	phone?: string | null;
	role: string;
	status: string;
	invitedAtUtc?: string | null;
	joinedAtUtc?: string | null;
};

export type ManagedTenantUsers = {
	tenantKey: string;
	displayName: string;
	tenantId: string;
	users: ManagedTenantUser[];
};

export type ManagedInviteResult = {
	tenantKey: string;
	tenantDisplayName: string;
	invite: {
		id: string;
		invitedEmail?: string | null;
		invitedPhone?: string | null;
		role: string;
		status: string;
		inviteToken?: string | null;
		expiresAtUtc: string;
	};
};

export type InviteAcceptanceContext = {
	inviteId: string;
	tenantId: string;
	tenantName: string;
	role: string;
	status: string;
	expiresAtUtc: string;
	invitedEmailMasked?: string | null;
	invitedPhoneMasked?: string | null;
	isAuthenticated: boolean;
	requiresAuthentication: boolean;
	canRedeem: boolean;
	nextStep: string;
	authenticatedUserMatchesInvite: boolean;
	authenticatedUserAlreadyMember: boolean;
};

type ApiEnvelope<T> = {
	success?: boolean;
	data?: T;
	errors?: Array<{ message?: string }>;
};

const request = async <T>(
	fetcher: typeof globalThis.fetch,
	path: string,
	accessToken: string | null | undefined,
	init?: RequestInit
): Promise<T> => {
	if (!accessToken) throw new Error('An authenticated admin session is required.');
	const isAnonymous = accessToken === 'anonymous';
	const response = await fetcher(`${getAuthApiBaseUrl()}${path}`, {
		...init,
		headers: {
			Accept: 'application/json',
			...(!isAnonymous ? { Authorization: `Bearer ${accessToken}` } : {}),
			...(init?.body ? { 'Content-Type': 'application/json' } : {}),
			...init?.headers
		}
	});

	let payload: ApiEnvelope<T> | T | null = null;
	try {
		payload = (await response.json()) as ApiEnvelope<T> | T;
	} catch {
		// Preserve the HTTP status below when the API did not return JSON.
	}

	if (!response.ok) {
		const envelope = payload as ApiEnvelope<T> | null;
		const detail = envelope?.errors?.map((item) => item.message).filter(Boolean).join(', ');
		throw new Error(detail || `User administration request failed with ${response.status}.`);
	}

	if (payload && typeof payload === 'object' && 'data' in payload) {
		return (payload as ApiEnvelope<T>).data as T;
	}
	return payload as T;
};

export const listPlatformManagedTenants = (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined
) => request<ManagedTenantUsers[]>(fetcher, '/api/platform/user-administration/tenants', accessToken);

export const createPlatformCustomerAdminInvite = (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined,
	tenantKey: string,
	contact: { email?: string; phone?: string }
) =>
	request<ManagedInviteResult>(
		fetcher,
		`/api/platform/user-administration/tenants/${encodeURIComponent(tenantKey)}/customer-admin-invites`,
		accessToken,
		{ method: 'POST', body: JSON.stringify(contact) }
	);

export const listCurrentTenantUsers = async (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined
): Promise<ManagedTenantUser[]> => {
	const [memberships, invites] = await Promise.all([
		request<Array<{
			id: string;
			userId: string;
			invitedEmail?: string | null;
			invitedPhone?: string | null;
			role: string;
			membershipStatus: string;
			dateInvited?: string | null;
			dateJoined?: string | null;
		}>>(fetcher, '/api/TenantMembership', accessToken),
		request<Array<{ id: string; reservedSeatMembershipId: string }>>(fetcher, '/api/Invite', accessToken)
	]);
	const inviteByMembership = new Map(invites.map((invite) => [invite.reservedSeatMembershipId, invite.id]));
	return memberships.map((membership) => ({
		membershipId: membership.id,
		inviteId: inviteByMembership.get(membership.id),
		userId: membership.userId && !/^0+$/.test(membership.userId.replaceAll('-', '')) ? membership.userId : null,
		email: membership.invitedEmail,
		phone: membership.invitedPhone,
		role: membership.role,
		status: membership.membershipStatus,
		invitedAtUtc: membership.dateInvited,
		joinedAtUtc: membership.dateJoined
	}));
};

export const createCurrentTenantInvite = (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined,
	contact: { email?: string; phone?: string; role: string }
) =>
	request<ManagedInviteResult['invite']>(fetcher, '/api/Invite', accessToken, {
		method: 'POST',
		body: JSON.stringify({
			invitedEmail: contact.email,
			invitedPhone: contact.phone,
			role: contact.role
		})
	});

export const updateCurrentTenantUserRole = (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined,
	membershipId: string,
	role: string
) =>
	request(fetcher, `/api/TenantMembership/${encodeURIComponent(membershipId)}/role`, accessToken, {
		method: 'POST',
		body: JSON.stringify({ role })
	});

export const removeCurrentTenantUser = (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined,
	membershipId: string
) =>
	request(fetcher, `/api/TenantMembership/${encodeURIComponent(membershipId)}`, accessToken, {
		method: 'DELETE'
	});

export const cancelCurrentTenantInvite = (
	fetcher: typeof globalThis.fetch,
	accessToken: string | null | undefined,
	inviteId: string
) =>
	request(fetcher, `/api/Invite/${encodeURIComponent(inviteId)}/cancel`, accessToken, {
		method: 'POST'
	});

export const getInviteAcceptanceContext = (
	fetcher: typeof globalThis.fetch,
	inviteId: string,
	inviteToken: string,
	accessToken?: string | null
) =>
	request<InviteAcceptanceContext>(
		fetcher,
		`/api/Invite/${encodeURIComponent(inviteId)}/acceptance?token=${encodeURIComponent(inviteToken)}`,
		accessToken ?? 'anonymous'
	);

export const redeemInvite = (
	fetcher: typeof globalThis.fetch,
	inviteId: string,
	inviteToken: string,
	accessToken: string | null | undefined
) =>
	request<{ tenantId: string }>(fetcher, `/api/Invite/${encodeURIComponent(inviteId)}/redeem`, accessToken, {
		method: 'POST',
		body: JSON.stringify({ inviteToken })
	});
