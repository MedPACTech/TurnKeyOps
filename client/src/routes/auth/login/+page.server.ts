import { loginDestination, tenantForPath, tenantLoginUrl } from '$lib/server/tenant-auth';
import { selectWorkspace, pendingTokenCookie, tokenTenantId } from '$lib/server/workspaces';
import { fail, redirect, isRedirect } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import {
	authTokenCookie,
	authRefreshTokenCookie,
	completeOtp,
	extractAccessToken,
	extractAuthRoles,
	extractRefreshToken,
	getAdminSessionFromToken,
	getAdminSurface,
	getDefaultAdminReturnTo,
	hasInternalAdminRole,
	inferOtpChannel,
	isTechnicianPath,
	legacyAdminCookieNames,
	resolveBdrAdminRole,
	startOtp,
	validateAdminAccessToken
} from '$lib/server/auth-session';

const adminSessionMaxAge = 60 * 60 * 8;

const getFormString = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

const getSurfaceMeta = (returnTo: string) => {
	const isInviteAcceptance = returnTo.startsWith('/auth/invite/');
	const isTechnician = isTechnicianPath(returnTo.split('?')[0]);
	const surface = getAdminSurface(returnTo);
	return {
		surface,
		isInviteAcceptance,
		isTechnician,
		label:
			returnTo === '/auth/workspaces' ? 'TurnKeyOps' : isInviteAcceptance
				? 'Invite activation'
				: isTechnician
					? 'Carl Zipf Field'
				: returnTo.startsWith('/carlzipf/admin')
					? 'Carl Zipf Admin'
				: surface === 'internal-admin'
				? 'Internal Admin'
				: returnTo.startsWith('/thinkpink/admin')
					? 'Think Pink Admin'
					: 'BDR Admin',
		defaultReturnTo: getDefaultAdminReturnTo(surface)
	};
};

const getReturnedOtpState = (challengeId: string, identifier: string) => ({
	challengeId,
	channel: inferOtpChannel(identifier),
	destinationMasked: '',
	devCode: null
});

export const load = async ({ cookies, url, fetch, params }) => {
	const returnTo = loginDestination((params as { tenant?: string }).tenant, url.searchParams.get('returnTo'));
	if (!(params as { tenant?: string }).tenant && tenantForPath(returnTo)) redirect(303, tenantLoginUrl(returnTo));
	const surfaceMeta = getSurfaceMeta(returnTo);
	const token = cookies.get(authTokenCookie);
	const tokenIsValid = await validateAdminAccessToken(fetch, token);
	const session = tokenIsValid
		? getAdminSessionFromToken(token, returnTo)
		: null;

	if (tokenIsValid && returnTo === '/auth/workspaces') redirect(303, returnTo);
	if (tokenIsValid && tenantForPath(returnTo)?.id !== tokenTenantId(token) && tenantForPath(returnTo)) redirect(303, `/auth/workspaces?returnTo=${encodeURIComponent(returnTo)}`);
	if (((surfaceMeta.isInviteAcceptance || surfaceMeta.isTechnician) && tokenIsValid) || (session && (surfaceMeta.surface === 'internal-admin' || session.role))) {
		throw redirect(303, returnTo);
	}

	return {
		returnTo,
		...surfaceMeta
	};
};

export const actions = {
	request: async ({ request, fetch, params }) => {
		const formData = await request.formData();
		const identifier = getFormString(formData, 'identifier');
		const returnTo = loginDestination((params as { tenant?: string }).tenant, getFormString(formData, 'returnTo') || null);
		const surfaceMeta = getSurfaceMeta(returnTo);
		const channel = inferOtpChannel(identifier);

		if (!channel) {
			return fail(400, {
				step: 'request',
				message: 'Enter a valid email address or mobile number.',
				identifier,
				returnTo,
				...surfaceMeta
			});
		}

		try {
			const otpState = await startOtp(fetch, identifier, tenantForPath(returnTo)?.id);
			return {
				step: 'verify',
				identifier,
				otpState,
				returnTo,
				...surfaceMeta
			};
		} catch (cause) {
			if (isRedirect(cause)) throw cause;
			return fail(502, {
				step: 'request',
				message: cause instanceof Error ? cause.message : 'Unable to send verification code.',
				identifier,
				returnTo,
				...surfaceMeta
			});
		}
	},
	verify: async ({ request, cookies, fetch, url, params }) => {
		const formData = await request.formData();
		const identifier = getFormString(formData, 'identifier');
		const code = getFormString(formData, 'code');
		const challengeId = getFormString(formData, 'challengeId');
		const returnTo = loginDestination((params as { tenant?: string }).tenant, getFormString(formData, 'returnTo') || null);
		const surfaceMeta = getSurfaceMeta(returnTo);

		if (!identifier || !code) {
			return fail(400, {
				step: 'verify',
				message: 'Enter the verification code.',
				identifier,
				otpState: getReturnedOtpState(challengeId, identifier),
				returnTo,
				...surfaceMeta
			});
		}

		const verifiedReturnTo = returnTo;
		try {
			let authResult = await completeOtp(fetch, identifier, code, challengeId);
			let accessToken = extractAccessToken(authResult);
            const preToken = typeof authResult.preTenantToken === 'string' ? authResult.preTenantToken : null;
            if (tenantForPath(returnTo) && (accessToken || preToken)) {
                authResult = await selectWorkspace(fetch, (accessToken || preToken)!, tenantForPath(returnTo)!.id);
                accessToken = extractAccessToken(authResult);
            } else if (preToken || (returnTo === '/auth/workspaces' && accessToken)) {
                cookies.set(pendingTokenCookie, (preToken || accessToken)!, { path: '/', httpOnly: true, sameSite: 'strict', secure: env.NODE_ENV === 'production' || url.protocol === 'https:', maxAge: 600 });
                redirect(303, '/auth/workspaces');
            }
			if (!accessToken) {
				throw new Error('Authentication completed but no access token was returned.');
			}

			const roles = extractAuthRoles(authResult, accessToken);
			const bdrRole = resolveBdrAdminRole(roles);
			if (!surfaceMeta.isInviteAcceptance && !surfaceMeta.isTechnician && surfaceMeta.surface === 'external-admin' && !bdrRole) {
				return fail(403, {
					step: 'verify',
					message: 'Your account does not have External Admin access.',
					identifier,
					otpState: getReturnedOtpState(challengeId, identifier),
					returnTo,
					...surfaceMeta
				});
			}
			if (!surfaceMeta.isInviteAcceptance && surfaceMeta.surface === 'internal-admin' && !hasInternalAdminRole(roles)) {
				return fail(403, {
					step: 'verify',
					message: 'Your account does not have Internal Admin access.',
					identifier,
					otpState: getReturnedOtpState(challengeId, identifier),
					returnTo,
					...surfaceMeta
				});
			}

			cookies.delete(pendingTokenCookie, { path: '/' });
			cookies.delete(authRefreshTokenCookie, { path: '/' });
			const secureCookie = env.NODE_ENV === 'production' || url.protocol === 'https:';

			cookies.set(authTokenCookie, accessToken, {
				path: '/',
				httpOnly: true,
				sameSite: 'strict',
				secure: secureCookie,
				maxAge: adminSessionMaxAge
			});

			const refreshToken = extractRefreshToken(authResult);
			if (refreshToken) {
				cookies.set(authRefreshTokenCookie, refreshToken, {
					path: '/',
					httpOnly: true,
					sameSite: 'strict',
					secure: secureCookie,
					maxAge: 60 * 60 * 24 * 30
				});
			}

			for (const cookieName of legacyAdminCookieNames) {
				cookies.delete(cookieName, {
					path: '/',
					httpOnly: true,
					sameSite: 'strict',
					secure: secureCookie
				});
			}
		} catch (cause) {
			if (isRedirect(cause)) throw cause;
			return fail(401, {
				step: 'verify',
				message: cause instanceof Error ? cause.message : 'Verification failed.',
				identifier,
				otpState: getReturnedOtpState(challengeId, identifier),
				returnTo,
				...surfaceMeta
			});
		}

		throw redirect(303, verifiedReturnTo);
	}
};
