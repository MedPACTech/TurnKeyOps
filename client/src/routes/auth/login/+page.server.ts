import { fail, redirect } from '@sveltejs/kit';
import {
	authTokenCookie,
	bdrAdminSessionCookie,
	completeOtp,
	extractAccessToken,
	extractAuthRoles,
	extractRefreshToken,
	getAdminSessionFromToken,
	getAdminSurface,
	getDefaultAdminReturnTo,
	getSafeAdminReturnTo,
	internalAdminSessionCookie,
	resolveBdrAdminRole,
	startOtp,
	type OtpChannel
} from '$lib/server/auth-session';

const authRefreshTokenCookie = 'tko_refresh_token';
const adminSessionMaxAge = 60 * 60 * 8;

const isOtpChannel = (value: string): value is OtpChannel => value === 'email' || value === 'sms';

const getFormString = (formData: FormData, key: string) => String(formData.get(key) ?? '').trim();

const getSurfaceMeta = (returnTo: string) => {
	const surface = getAdminSurface(returnTo);
	return {
		surface,
		label: surface === 'internal-admin' ? 'Internal Admin' : 'External Admin',
		defaultReturnTo: getDefaultAdminReturnTo(surface)
	};
};

const getReturnedOtpState = (challengeId: string, preferredChannel: OtpChannel) => ({
	challengeId,
	channel: preferredChannel,
	destinationMasked: '',
	devCode: null
});

export const load = async ({ cookies, url }) => {
	const returnTo = getSafeAdminReturnTo(url.searchParams.get('returnTo'));
	const surfaceMeta = getSurfaceMeta(returnTo);
	const session = getAdminSessionFromToken(cookies.get(authTokenCookie), returnTo, cookies.get(bdrAdminSessionCookie));

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
		const preferredChannelValue = getFormString(formData, 'preferredChannel');
		const preferredChannel = isOtpChannel(preferredChannelValue) ? preferredChannelValue : 'email';
		const returnTo = getSafeAdminReturnTo(getFormString(formData, 'returnTo'));
		const surfaceMeta = getSurfaceMeta(returnTo);

		if (!identifier) {
			return fail(400, {
				step: 'request',
				message: 'Enter your email or mobile number.',
				identifier,
				preferredChannel,
				returnTo,
				...surfaceMeta
			});
		}

		try {
			const otpState = await startOtp(fetch, identifier, preferredChannel);
			return {
				step: 'verify',
				identifier,
				preferredChannel,
				otpState,
				returnTo,
				...surfaceMeta
			};
		} catch (cause) {
			return fail(502, {
				step: 'request',
				message: cause instanceof Error ? cause.message : 'Unable to send verification code.',
				identifier,
				preferredChannel,
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
		const preferredChannelValue = getFormString(formData, 'preferredChannel');
		const preferredChannel = isOtpChannel(preferredChannelValue) ? preferredChannelValue : 'email';
		const returnTo = getSafeAdminReturnTo(getFormString(formData, 'returnTo'));
		const surfaceMeta = getSurfaceMeta(returnTo);

		if (!identifier || !code) {
			return fail(400, {
				step: 'verify',
				message: 'Enter the verification code.',
				identifier,
				preferredChannel,
				otpState: getReturnedOtpState(challengeId, preferredChannel),
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
					preferredChannel,
					otpState: getReturnedOtpState(challengeId, preferredChannel),
					returnTo,
					...surfaceMeta
				});
			}

			cookies.set(authTokenCookie, accessToken, {
				path: '/',
				httpOnly: true,
				sameSite: 'lax',
				secure: url.protocol === 'https:',
				maxAge: adminSessionMaxAge
			});

			const refreshToken = extractRefreshToken(authResult);
			if (refreshToken) {
				cookies.set(authRefreshTokenCookie, refreshToken, {
					path: '/',
					httpOnly: true,
					sameSite: 'lax',
					secure: url.protocol === 'https:',
					maxAge: 60 * 60 * 24 * 30
				});
			}

			if (bdrRole) {
				cookies.set(bdrAdminSessionCookie, bdrRole, {
					path: '/bdr/admin',
					httpOnly: true,
					sameSite: 'lax',
					secure: url.protocol === 'https:',
					maxAge: adminSessionMaxAge
				});
			}

			if (surfaceMeta.surface === 'internal-admin') {
				cookies.set(internalAdminSessionCookie, '1', {
					path: '/turnkeyops/admin',
					httpOnly: true,
					sameSite: 'lax',
					secure: url.protocol === 'https:',
					maxAge: adminSessionMaxAge
				});
			}
		} catch (cause) {
			return fail(401, {
				step: 'verify',
				message: cause instanceof Error ? cause.message : 'Verification failed.',
				identifier,
				preferredChannel,
				otpState: getReturnedOtpState(challengeId, preferredChannel),
				returnTo,
				...surfaceMeta
			});
		}

		throw redirect(303, verifiedReturnTo);
	}
};
