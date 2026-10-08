import { tenantLoginUrl } from '$lib/server/tenant-auth';
import { pendingTokenCookie } from '$lib/server/workspaces';
import { env } from '$env/dynamic/private';
import {
	authRefreshTokenCookie,
	authTokenCookie,
	extractAccessToken,
	getAuthApiBaseUrl,
	getSafeAdminReturnTo,
	getTokenSessionId,
	legacyAdminCookieNames,
	refreshAuthSession
} from '$lib/server/auth-session';

export const POST = async ({ cookies, fetch, url, request }) => {
	const formData = await request.formData();
	const requestedReturnTo = formData.get('returnTo');
	const returnTo = typeof requestedReturnTo === 'string' && requestedReturnTo
        ? getSafeAdminReturnTo(requestedReturnTo) : '/auth/workspaces';
	const accessToken = cookies.get(authTokenCookie);
	const refreshToken = cookies.get(authRefreshTokenCookie);

	const revoke = async (token: string | null | undefined) => {
		const sessionId = getTokenSessionId(token);
		if (!token || !sessionId) return false;
		try {
			const response = await fetch(`${getAuthApiBaseUrl()}/api/auth/sessions/revoke`, {
				method: 'POST',
				headers: {
					Authorization: `Bearer ${token}`,
					'Content-Type': 'application/json',
					Accept: 'application/json'
				},
				body: JSON.stringify({ sessionId })
			});
			return response.ok;
		} catch {
			return false;
		}
	};

	const revoked = await revoke(accessToken);
	if (!revoked && refreshToken) {
		try {
			const refreshed = await refreshAuthSession(fetch, refreshToken);
			await revoke(extractAccessToken(refreshed));
		} catch {
			// Local cookie invalidation still completes; protected routes validate the token on every request.
		}
	}

	const secure = env.NODE_ENV === 'production' || url.protocol === 'https:';
	const options = { path: '/', httpOnly: true, sameSite: 'strict' as const, secure };
	cookies.delete(authTokenCookie, options);
	cookies.delete(pendingTokenCookie, options);
	cookies.delete(authRefreshTokenCookie, options);
	for (const cookieName of legacyAdminCookieNames) {
		cookies.delete(cookieName, options);
		cookies.delete(cookieName, { ...options, path: '/turnkeyops/admin' });
	}

	return new Response(null, {
		status: 303,
		headers: { Location: tenantLoginUrl(returnTo) }
	});
};
