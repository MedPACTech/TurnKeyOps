import { fail } from '@sveltejs/kit';
import {
	authRefreshTokenCookie,
	authTokenCookie,
	validateAdminAccessToken
} from '$lib/server/auth-session';
import { getTenantById } from '$lib/config/tenants';
import { getInviteAcceptanceContext, redeemInvite } from '$lib/server/user-administration';

export const load = async ({ params, url, fetch, cookies }) => {
	const inviteToken = url.searchParams.get('token')?.trim() ?? '';
	if (!inviteToken) return { invalid: true, message: 'This activation link is missing its one-time token.' };

	const accessToken = cookies.get(authTokenCookie);
	const tokenIsValid = await validateAdminAccessToken(fetch, accessToken);
	try {
		const context = await getInviteAcceptanceContext(
			fetch,
			params.inviteId,
			inviteToken,
			tokenIsValid ? accessToken : null
		);
		return { invalid: false, context, inviteToken };
	} catch (cause) {
		return {
			invalid: true,
			message: cause instanceof Error ? cause.message : 'This activation link is invalid or expired.'
		};
	}
};

export const actions = {
	redeem: async ({ params, request, fetch, cookies, url }) => {
		const data = await request.formData();
		const inviteToken = String(data.get('inviteToken') ?? '').trim();
		const accessToken = cookies.get(authTokenCookie);
		if (!inviteToken || !(await validateAdminAccessToken(fetch, accessToken))) {
			return fail(401, { error: 'Sign in with the invited email or mobile number before accepting.' });
		}

		try {
			const membership = await redeemInvite(fetch, params.inviteId, inviteToken, accessToken);
			const tenant = getTenantById(membership.tenantId);
			const adminPath = tenant?.adminPath ?? '/auth/login';
			const secure = url.protocol === 'https:';
			cookies.delete(authTokenCookie, { path: '/', secure, sameSite: 'strict' });
			cookies.delete(authRefreshTokenCookie, { path: '/', secure, sameSite: 'strict' });
			return {
				accepted: true,
				message: 'Invite accepted. Sign in again to open your new tenant workspace.',
				signInUrl: `/auth/login?returnTo=${encodeURIComponent(adminPath)}`
			};
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Could not accept this invite.' });
		}
	}
};
