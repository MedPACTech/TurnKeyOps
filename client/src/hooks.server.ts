import type { Handle } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import {
	authRefreshTokenCookie,
	authTokenCookie,
	buildLoginRedirect,
	extractAccessToken,
	extractRefreshToken,
	getAdminSessionFromToken,
	isAdminPath,
	isCrossSiteFormMutation,
	legacyAdminCookieNames,
	refreshAuthSession,
	validateAdminAccessToken
} from '$lib/server/auth-session';
import { resolveProductionPathname } from '$lib/config/domains';
import { getExternalAdminTenantForPath } from '$lib/config/external-admin';
import { getTenantById } from '$lib/config/tenants';

const withSecurityHeaders = (response: Response, isHttps: boolean) => {
	response.headers.set('X-Content-Type-Options', 'nosniff');
	response.headers.set('X-Frame-Options', 'DENY');
	response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
	response.headers.set('Permissions-Policy', 'camera=(), geolocation=(), payment=()');
	response.headers.set('Cross-Origin-Opener-Policy', 'same-origin');
	if (isHttps) response.headers.set('Strict-Transport-Security', 'max-age=31536000; includeSubDomains');
	return response;
};

const setSessionCookies = (
	event: Parameters<Handle>[0]['event'],
	accessToken: string,
	refreshToken: string | null
) => {
	const secure = env.NODE_ENV === 'production' || event.url.protocol === 'https:';
	event.cookies.set(authTokenCookie, accessToken, {
		path: '/',
		httpOnly: true,
		sameSite: 'strict',
		secure,
		maxAge: 60 * 60 * 8
	});
	if (refreshToken) {
		event.cookies.set(authRefreshTokenCookie, refreshToken, {
			path: '/',
			httpOnly: true,
			sameSite: 'strict',
			secure,
			maxAge: 60 * 60 * 24 * 30
		});
	}
};

const clearSessionCookies = (
	event: Parameters<Handle>[0]['event'],
	response: Response
) => {
	const secure = env.NODE_ENV === 'production' || event.url.protocol === 'https:';
	const options = {
		path: '/',
		httpOnly: true,
		sameSite: 'strict' as const,
		secure,
		maxAge: 0,
		expires: new Date(0)
	};
	for (const cookieName of [authTokenCookie, authRefreshTokenCookie, ...legacyAdminCookieNames]) {
		response.headers.append('Set-Cookie', event.cookies.serialize(cookieName, '', options));
	}
	return response;
};

const accessFailure = (
	event: Parameters<Handle>[0]['event'],
	status: 401 | 403,
	message: string,
	loginLocation?: string
) => {
	const wantsHtml = event.request.headers.get('accept')?.includes('text/html');
	const response = loginLocation && wantsHtml
		? new Response(null, { status: 303, headers: { Location: loginLocation } })
		: new Response(wantsHtml ? message : JSON.stringify({ message }), {
			status,
			headers: { 'Content-Type': wantsHtml ? 'text/plain; charset=utf-8' : 'application/json' }
		});
	return clearSessionCookies(
		event,
		withSecurityHeaders(response, event.url.protocol === 'https:')
	);
};

export const handle: Handle = async ({ event, resolve }) => {
	if (
		isCrossSiteFormMutation(
			event.request.method,
			event.request.headers.get('content-type'),
			event.request.headers.get('origin'),
			event.url.origin
		)
	) {
		return accessFailure(event, 403, 'Cross-site form submissions are forbidden.');
	}

	const authPathname = resolveProductionPathname(event.url.hostname, event.url.pathname);

	if (!isAdminPath(authPathname)) {
		return withSecurityHeaders(await resolve(event), event.url.protocol === 'https:');
	}

	let authToken = event.cookies.get(authTokenCookie);
	let tokenIsValid = await validateAdminAccessToken(event.fetch, authToken);
	if (!tokenIsValid) {
		const refreshToken = event.cookies.get(authRefreshTokenCookie);
		if (refreshToken) {
			try {
				const refreshed = await refreshAuthSession(event.fetch, refreshToken);
				const refreshedAccessToken = extractAccessToken(refreshed);
				if (
					refreshedAccessToken &&
					(await validateAdminAccessToken(event.fetch, refreshedAccessToken))
				) {
					authToken = refreshedAccessToken;
					tokenIsValid = true;
					setSessionCookies(event, refreshedAccessToken, extractRefreshToken(refreshed));
				}
			} catch {
				// Invalid, expired, or revoked refresh tokens fall through to session cleanup.
			}
		}
	}
	const authSession = tokenIsValid
		? getAdminSessionFromToken(authToken, authPathname)
		: null;
	if (authSession) {
		const routeTenant = getExternalAdminTenantForPath(authPathname);
		const claimedTenant = authSession.tenantId ? getTenantById(authSession.tenantId) : null;
		if (routeTenant && (!claimedTenant || routeTenant.id !== claimedTenant.id)) {
			return accessFailure(event, 403, `This account does not have access to ${routeTenant.name}.`);
		}

		event.locals.adminSession = authSession;
		if (authSession.surface === 'external-admin' && authSession.role) {
			event.locals.bdrAdminSession = {
				role: authSession.role,
				source: 'auth-token'
			};
		}

		return withSecurityHeaders(await resolve(event), event.url.protocol === 'https:');
	}

	if (event.request.headers.get('accept')?.includes('text/html')) {
		const loginUrl = new URL(event.url);
		loginUrl.pathname = authPathname;
		return accessFailure(
			event,
			401,
			'A valid authorized admin session is required.',
			buildLoginRedirect(loginUrl)
		);
	}

	return accessFailure(event, 401, 'A valid authorized admin session is required.');
};
