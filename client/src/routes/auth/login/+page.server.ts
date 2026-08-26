import { fail, redirect } from '@sveltejs/kit';
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
	getSafeAdminReturnTo,
	hasInternalAdminRole,
	inferOtpChannel,
	legacyAdminCookieNames,
	resolveBdrAdminRole,
	startOtp,
	validateAdminAccessToken
} from '$lib/server/auth-session';

const adminSessionMaxAge = 60 * 60 * 8;

const getFormString = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

const getSurfaceMeta = (returnTo: string) => {
	const surface = getAdminSurface(returnTo);
	return {
		surface,
		label:
			surface === 'internal-admin'
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

export const load = async ({ cookies, url, fetch }) => {
	const returnTo = getSafeAdminReturnTo(url.searchParams.get('returnTo'));
	const surfaceMeta = getSurfaceMeta(returnTo);
	const token = cookies.get(authTokenCookie);
	const session = (await validateAdminAccessToken(fetch, token))
		? getAdminSessionFromToken(token, returnTo)
		: null;

	if (session && (surfaceMeta.surface === 'internal-admin' || session.role)) {
		throw redirect(303, returnTo);
	}

	return {
		returnTo,
		...surfaceMeta
	};
};

export const actions = {
	request: async ({ request, fetch }) => {
		const formData = await request.formData();
		const identifier = getFormString(formData, 'identifier');
		const returnTo = getSafeAdminReturnTo(getFormString(formData, 'returnTo'));
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
			const otpState = await startOtp(fetch, identifier);
			return {
				step: 'verify',
				identifier,
				otpState,
				returnTo,
				...surfaceMeta
			};
		} catch (cause) {
			return fail(502, {
				step: 'request',
				message: cause instanceof Error ? cause.message : 'Unable to send verification code.',
				identifier,
				returnTo,
				...surfaceMeta
			});
		}
	},
	verify: async ({ request, cookies, fetch, url }) => {
		const formData = await request.formData();
		const identifier = getFormString(formData, 'identifier');
		const code = getFormString(formData, 'code');
		const challengeId = getFormString(formData, 'challengeId');
		const returnTo = getSafeAdminReturnTo(getFormString(formData, 'returnTo'));
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

		let verifiedReturnTo = returnTo;
		try {
			const authResult = await completeOtp(fetch, identifier, code, challengeId);
			const accessToken = extractAccessToken(authResult);
			if (!accessToken) {
				throw new Error('Authentication completed but no access token was returned.');
			}

			const roles = extractAuthRoles(authResult, accessToken);
			const bdrRole = resolveBdrAdminRole(roles);
			if (surfaceMeta.surface === 'external-admin' && !bdrRole) {
				return fail(403, {
					step: 'verify',
					message: 'Your account does not have External Admin access.',
					identifier,
					otpState: getReturnedOtpState(challengeId, identifier),
					returnTo,
					...surfaceMeta
				});
			}
			if (surfaceMeta.surface === 'internal-admin' && !hasInternalAdminRole(roles)) {
				return fail(403, {
					step: 'verify',
					message: 'Your account does not have Internal Admin access.',
					identifier,
					otpState: getReturnedOtpState(challengeId, identifier),
					returnTo,
					...surfaceMeta
				});
			}

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
