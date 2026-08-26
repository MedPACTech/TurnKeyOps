export type BdrAdminViewRole = 'owner' | 'office-admin';
export type AdminSurface = 'external-admin' | 'internal-admin';
export type AdminSession = {
	surface: AdminSurface;
	role: BdrAdminViewRole | null;
	email: string;
	tenantId: string;
	source: 'auth-token';
};

type JwtClaims = Record<string, unknown>;
const roleClaimKeys = [
	'role',
	'roles',
	'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
];

export const isExternalAdminPath = (pathname: string) =>
	pathname === '/bdr/admin' ||
	pathname.startsWith('/bdr/admin/') ||
	pathname === '/thinkpink/admin' ||
	pathname.startsWith('/thinkpink/admin/');
export const isInternalAdminPath = (pathname: string) =>
	pathname === '/turnkeyops/admin' || pathname.startsWith('/turnkeyops/admin/');
export const isAdminPath = (pathname: string) => isExternalAdminPath(pathname) || isInternalAdminPath(pathname);
export const getAdminSurface = (pathname: string): AdminSurface =>
	isInternalAdminPath(pathname) ? 'internal-admin' : 'external-admin';
export const getDefaultAdminReturnTo = (surface: AdminSurface) =>
	surface === 'internal-admin' ? '/turnkeyops/admin/dashboard' : '/bdr/admin/bob';

export const getSafeAdminReturnTo = (value: string | null | undefined) => {
	if (!value || value.includes('\\') || /[\u0000-\u001f]/.test(value)) return '/bdr/admin/bob';
	try {
		const parsed = new URL(value, 'https://turnkeyops.invalid');
		if (parsed.origin !== 'https://turnkeyops.invalid') return '/bdr/admin/bob';
		const allowed = ['/turnkeyops/admin', '/bdr/admin', '/thinkpink/admin'].some(
			(prefix) => parsed.pathname === prefix || parsed.pathname.startsWith(`${prefix}/`)
		);
		if (allowed) return `${parsed.pathname}${parsed.search}`;
	} catch {
		return '/bdr/admin/bob';
	}
	return '/bdr/admin/bob';
};

export const isCrossSiteFormMutation = (
	method: string,
	contentType: string | null,
	requestOrigin: string | null,
	expectedOrigin: string
) => {
	if (!['POST', 'PUT', 'PATCH', 'DELETE'].includes(method.toUpperCase())) return false;
	const mediaType = contentType?.split(';', 1)[0].trim().toLowerCase();
	if (!['application/x-www-form-urlencoded', 'multipart/form-data', 'text/plain'].includes(mediaType ?? '')) {
		return false;
	}
	return requestOrigin !== expectedOrigin;
};

export const decodeJwtClaims = (token: string | null | undefined): JwtClaims => {
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

const asStringArray = (value: unknown): string[] =>
	Array.isArray(value)
		? value.filter((item): item is string => typeof item === 'string')
		: typeof value === 'string'
			? [value]
			: [];

export const extractTokenRoles = (token: string | null | undefined) => {
	const claims = decodeJwtClaims(token);
	return roleClaimKeys.flatMap((key) => asStringArray(claims[key]));
};

export const resolveBdrAdminRole = (roles: string[]): BdrAdminViewRole | null => {
	const normalized = roles.map((role) => role.trim().toLowerCase().replace(/[_\s]+/g, '-'));
	if (normalized.includes('owner') || normalized.includes('company-owner') || normalized.includes('tenant-owner'))
		return 'owner';
	if (
		normalized.some((role) =>
			['office-admin', 'company-admin', 'tenant-admin', 'admin', 'administrator'].includes(role)
		)
	)
		return 'office-admin';
	return null;
};

export const hasInternalAdminRole = (roles: string[]) =>
	roles.some((role) => role.trim().toLowerCase().replace(/[\s-]+/g, '_') === 'internal_admin');

const getClaimString = (claims: JwtClaims, keys: string[]) => {
	for (const key of keys) {
		const value = claims[key];
		if (typeof value === 'string') return value;
	}
	return '';
};

export const getTokenSessionId = (token: string | null | undefined) =>
	getClaimString(decodeJwtClaims(token), ['sid', 'session_id', 'sessionId']);

const isExpired = (claims: JwtClaims) => {
	const rawExp = claims.exp;
	const exp = typeof rawExp === 'number' ? rawExp : Number(rawExp);
	return !Number.isFinite(exp) || exp <= 0 || Date.now() / 1000 >= exp;
};

export const getAdminSessionFromToken = (
	token: string | null | undefined,
	pathname: string
): AdminSession | null => {
	const claims = decodeJwtClaims(token);
	if (!Object.keys(claims).length || isExpired(claims)) return null;
	const roles = extractTokenRoles(token);
	const role = resolveBdrAdminRole(roles);
	const surface = getAdminSurface(pathname);
	const tenantId = getClaimString(claims, ['tenant_id', 'tenant', 'tid']);
	if (surface === 'external-admin' && (!role || !tenantId)) return null;
	if (surface === 'internal-admin' && !hasInternalAdminRole(roles)) return null;
	return {
		surface,
		role,
		email: getClaimString(claims, ['email', 'unique_name', 'preferred_username']),
		tenantId,
		source: 'auth-token'
	};
};
