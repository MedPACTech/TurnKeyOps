import { env } from '$env/dynamic/private';
import { extractTokenRoles } from './session-policy';
export {
	getAdminSessionFromToken,
	getAdminSurface,
	getDefaultAdminReturnTo,
	getSafeAdminReturnTo,
	getTokenSessionId,
	hasInternalAdminRole,
	isAdminPath,
	isCrossSiteFormMutation,
	isExternalAdminPath,
	isInternalAdminPath,
	resolveBdrAdminRole
} from './session-policy';
export type { AdminSession, AdminSurface } from './session-policy';

export type OtpChannel = 'email' | 'sms';

export type StartOtpResponse = {
	channel?: string;
	destinationMasked?: string;
	expiresInSeconds?: number;
	requiresTermsAcceptance?: boolean;
	availableChannels?: string[];
	devCode?: string | null;
	challengeId?: string | null;
	expiresAt?: string | null;
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

export const authTokenCookie = 'tko_auth_token';
export const authRefreshTokenCookie = 'tko_refresh_token';
export const legacyAdminCookieNames = [
	'tko_bdr_admin_role',
	'tko_bdr_admin_contact_id',
	'tko_internal_admin_session'
] as const;

const defaultApiBaseUrl = 'http://localhost:5178';

export const getAuthApiBaseUrl = () => {
	const configured = env.PUBLIC_TKO_API_BASE_URL || env.TKO_API_BASE_URL || env.VITE_API_URL;
	if (!configured && env.NODE_ENV === 'production') {
		throw new Error('Production authentication requires TKO_API_BASE_URL or PUBLIC_TKO_API_BASE_URL.');
	}
	return (configured || defaultApiBaseUrl).replace(/\/$/, '');
};

export const validateAdminAccessToken = async (
	fetch: typeof globalThis.fetch,
	token: string | null | undefined
) => {
	if (!token) return false;

	try {
		const response = await fetch(`${getAuthApiBaseUrl()}/api/auth/session`, {
			headers: {
				Authorization: `Bearer ${token}`,
				Accept: 'application/json'
			}
		});
		return response.ok;
	} catch {
		return false;
	}
};

const isLocalAuthApi = (baseUrl: string) => {
	try {
		const { hostname } = new URL(baseUrl);
		return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
	} catch {
		return false;
	}
};

export const isDevelopmentAuthEnabled = () =>
	env.NODE_ENV !== 'production' &&
	env.TKO_DEVELOPMENT_AUTH_MODE === 'true' &&
	isLocalAuthApi(getAuthApiBaseUrl());

const normalizeOtpDestination = (identifier: string, channel?: OtpChannel) => {
	const value = identifier.trim();
	if (channel === 'email' || value.includes('@')) return value;

	const digits = value.replace(/\D/g, '');
	if (digits.length === 10) return `+1${digits}`;
	if (digits.length === 11 && digits.startsWith('1')) return `+${digits}`;

	return value;
};

export const inferOtpChannel = (identifier: string): OtpChannel | null => {
	const value = identifier.trim();
	if (!value) return null;
	if (value.includes('@')) return 'email';

	const digits = value.replace(/\D/g, '');
	return digits.length >= 7 && digits.length <= 15 ? 'sms' : null;
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

export const startOtp = async (fetch: typeof globalThis.fetch, identifier: string) => {
	const channel = inferOtpChannel(identifier);
	if (!channel) {
		throw new Error('Enter a valid email address or mobile number.');
	}

	const result = await postAuthApi<StartOtpResponse>(fetch, '/auth/startotp', {
		destination: normalizeOtpDestination(identifier, channel),
		preferredChannel: channel
	});

	return {
		...result,
		channel: result.channel ?? channel,
		devCode: isDevelopmentAuthEnabled() ? (result.devCode ?? '123456') : null
	};
};

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
		destination: normalizeOtpDestination(identifier),
		code: code.trim(),
		challengeId
	});
};

export const refreshAuthSession = (fetch: typeof globalThis.fetch, refreshToken: string) =>
	postAuthApi<AuthResult>(fetch, '/auth/refresh', { refreshToken });

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

const asStringArray = (value: unknown): string[] => {
	if (Array.isArray(value)) {
		return value.filter((item): item is string => typeof item === 'string');
	}

	return typeof value === 'string' ? [value] : [];
};

export const extractAuthRoles = (result: AuthResult | null | undefined, token: string | null | undefined) => {
	const resultRoles = result ? [...asStringArray(result.roles), ...asStringArray(result.user?.roles)] : [];

	return [...resultRoles, ...extractTokenRoles(token)];
};
