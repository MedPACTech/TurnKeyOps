import { env } from '$env/dynamic/private';
import { isBdrAdminViewRole, type BdrAdminViewRole } from '$lib/config/platform';

export type AdminSurface = 'external-admin' | 'internal-admin';

export type AdminSession = {
	surface: AdminSurface;
	role: BdrAdminViewRole | null;
	email: string;
	tenantId: string;
	source: 'auth-token' | 'contact-access';
};

export type OtpChannel = 'email' | 'sms';

export type StartOtpResponse = {
	channel?: string;
	destinationMasked?: string;
	expiresInSeconds?: number;
	requiresTermsAcceptance?: boolean;
	availableChannels?: string[];
	devCode?: string | null;
	challengeId?: string | null;
};

export type AuthResult = {
	accessToken?: string;
	token?:
		| string
		| {
				accessToken?: string;
				refreshToken?: string;
				[key: string]: unknown;
		  };
	refreshToken?: string;
	roles?: string[];
	auth?: {
		accessToken?: string;
		token?: string;
		refreshToken?: string;
	};
	user?: {
		email?: string;
		roles?: string[];
		tenantId?: string;
	};
	[key: string]: unknown;
};

type ApiEnvelope<T> = {
	success: boolean;
	data?: T;
	errors?: Array<{ message?: string }>;
};

type JwtClaims = Record<string, unknown>;

export const authTokenCookie = 'tko_auth_token';
export const bdrAdminSessionCookie = 'tko_bdr_admin_role';
export const bdrAdminContactCookie = 'tko_bdr_admin_contact_id';
export const internalAdminSessionCookie = 'tko_internal_admin_session';

const defaultApiBaseUrl = 'http://localhost:5178';
const roleClaimKeys = [
	'role',
	'roles',
	'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
];

export const getAuthApiBaseUrl = () =>
	(env.PUBLIC_TKO_API_BASE_URL || env.TKO_API_BASE_URL || env.VITE_API_URL || defaultApiBaseUrl).replace(/\/$/, '');

export const isExternalAdminPath = (pathname: string) => pathname === '/bdr/admin' || pathname.startsWith('/bdr/admin/');
export const isInternalAdminPath = (pathname: string) =>
	pathname === '/turnkeyops/admin' || pathname.startsWith('/turnkeyops/admin/');
export const isAdminPath = (pathname: string) => isExternalAdminPath(pathname) || isInternalAdminPath(pathname);

export const getAdminSurface = (pathname: string): AdminSurface =>
	isInternalAdminPath(pathname) ? 'internal-admin' : 'external-admin';

export const getDefaultAdminReturnTo = (surface: AdminSurface) =>
	surface === 'internal-admin' ? '/turnkeyops/admin/dashboard' : '/bdr/admin/dashboard';

export const getSafeAdminReturnTo = (value: string | null | undefined) => {
	if (value?.startsWith('/turnkeyops/admin')) return value;
	if (value?.startsWith('/bdr/admin')) return value;
	return '/bdr/admin/dashboard';
};

export const buildLoginRedirect = (url: URL) => {
	const returnTo = `${url.pathname}${url.search}`;
	return `/auth/login?returnTo=${encodeURIComponent(returnTo)}`;
};

const isEnvelope = <T>(value: unknown): value is ApiEnvelope<T> =>
	typeof value === 'object' && value !== null && 'success' in value;

const unwrapApiPayload = <T>(payload: unknown): T => {
	if (!isEnvelope<T>(payload)) {
		return payload as T;
	}

	if (!payload.success) {
		const message = payload.errors?.map((item) => item.message).filter(Boolean).join(', ') || 'Request failed';
		throw new Error(message);
	}

	return payload.data as T;
};

export const postAuthApi = async <T>(
	fetch: typeof globalThis.fetch,
	path: string,
	body: Record<string, unknown>
): Promise<T> => {
	const response = await fetch(`${getAuthApiBaseUrl()}/api${path}`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(body)
	});

	let payload: unknown = {};
	try {
		payload = await response.json();
	} catch {
		payload = {};
	}

	if (!response.ok) {
		const message = isEnvelope<unknown>(payload)
			? payload.errors?.map((item) => item.message).filter(Boolean).join(', ')
			: '';
		throw new Error(message || `Authentication request failed with ${response.status}`);
	}

	return unwrapApiPayload<T>(payload);
};

export const startOtp = (fetch: typeof globalThis.fetch, identifier: string, preferredChannel: OtpChannel) =>
	postAuthApi<StartOtpResponse>(fetch, '/auth/startotp', {
		destination: identifier.trim(),
		preferredChannel
	});

export const completeOtp = (
	fetch: typeof globalThis.fetch,
	identifier: string,
	code: string,
	challengeId?: string | null
) => {
	if (!challengeId) {
		throw new Error('Missing OTP challenge id. Request a fresh verification code.');
	}

	return postAuthApi<AuthResult>(fetch, '/auth/completeotp', {
		destination: identifier.trim(),
		code: code.trim(),
		challengeId
	});
};

export const extractAccessToken = (result: AuthResult) => {
	if (typeof result.token === 'object' && result.token !== null) {
		return result.token.accessToken ?? null;
	}

	return result.accessToken ?? result.token ?? result.auth?.accessToken ?? result.auth?.token ?? null;
};

export const extractRefreshToken = (result: AuthResult) => {
	if (typeof result.token === 'object' && result.token !== null) {
		return result.token.refreshToken ?? null;
	}

	return result.refreshToken ?? result.auth?.refreshToken ?? null;
};

const decodeJwtClaims = (token: string | null | undefined): JwtClaims => {
	if (!token) return {};
	const [, payload] = token.split('.');
	if (!payload) return {};

	try {
		const padded = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '=');
		return JSON.parse(globalThis.atob(padded)) as JwtClaims;
	} catch {
		return {};
	}
};

const asStringArray = (value: unknown): string[] => {
	if (Array.isArray(value)) {
		return value.filter((item): item is string => typeof item === 'string');
	}

	return typeof value === 'string' ? [value] : [];
};

export const extractAuthRoles = (result: AuthResult | null | undefined, token: string | null | undefined) => {
	const claims = decodeJwtClaims(token);
	const claimRoles = roleClaimKeys.flatMap((key) => asStringArray(claims[key]));
	const resultRoles = result ? [...asStringArray(result.roles), ...asStringArray(result.user?.roles)] : [];

	return [...resultRoles, ...claimRoles];
};

export const resolveBdrAdminRole = (
	roles: string[],
	fallbackRole?: string | null
): BdrAdminViewRole | null => {
	if (isBdrAdminViewRole(fallbackRole)) return fallbackRole;

	const normalized = roles.map((role) => role.trim().toLowerCase().replace(/[_\s]+/g, '-'));
	if (normalized.includes('owner') || normalized.includes('company-owner') || normalized.includes('tenant-owner')) {
		return 'owner';
	}

	if (
		normalized.some((role) =>
			['office-admin', 'company-admin', 'tenant-admin', 'admin', 'administrator'].includes(role)
		)
	) {
		return 'office-admin';
	}

	return null;
};

const getClaimString = (claims: JwtClaims, keys: string[]) => {
	for (const key of keys) {
		const value = claims[key];
		if (typeof value === 'string') return value;
	}

	return '';
};

const isExpired = (claims: JwtClaims) => {
	const rawExp = claims.exp;
	const exp = typeof rawExp === 'number' ? rawExp : Number(rawExp);
	return Number.isFinite(exp) && exp > 0 && Date.now() / 1000 > exp;
};

export const getAdminSessionFromToken = (
	token: string | null | undefined,
	pathname: string,
	fallbackRole?: string | null
): AdminSession | null => {
	const claims = decodeJwtClaims(token);
	if (!Object.keys(claims).length || isExpired(claims)) return null;

	const roles = extractAuthRoles(null, token);
	const role = resolveBdrAdminRole(roles, fallbackRole);
	const surface = getAdminSurface(pathname);

	if (surface === 'external-admin' && !role) {
		return null;
	}

	return {
		surface,
		role,
		email: getClaimString(claims, ['email', 'unique_name', 'preferred_username']),
		tenantId: getClaimString(claims, ['tenant_id', 'tenant', 'tid']),
		source: 'auth-token'
	};
};
