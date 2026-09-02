import { env } from '$env/dynamic/private';
import {
	authRefreshTokenCookie,
	authTokenCookie,
	extractAccessToken,
	getAuthApiBaseUrl,
	getTokenSessionId,
	legacyAdminCookieNames,
	refreshAuthSession
} from '$lib/server/auth-session';

export const POST = async ({ cookies, fetch, url }) => {
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
	cookies.delete(authRefreshTokenCookie, options);
	for (const cookieName of legacyAdminCookieNames) {
		cookies.delete(cookieName, options);
		cookies.delete(cookieName, { ...options, path: '/turnkeyops/admin' });
	}

	return new Response(null, { status: 303, headers: { Location: '/auth/login' } });
};
