import { error, redirect, type Handle } from '@sveltejs/kit';
import {
	authTokenCookie,
	bdrAdminSessionCookie,
	buildLoginRedirect,
	getAdminSessionFromToken,
	isAdminPath,
	validateAdminAccessToken
} from '$lib/server/auth-session';
import { resolveProductionPathname } from '$lib/config/domains';
import { getExternalAdminTenantForPath } from '$lib/config/external-admin';
import { getTenantById } from '$lib/config/tenants';

export const handle: Handle = async ({ event, resolve }) => {
	const authPathname = resolveProductionPathname(event.url.hostname, event.url.pathname);

	if (!isAdminPath(authPathname)) {
		return resolve(event);
	}

	const authToken = event.cookies.get(authTokenCookie);
	const tokenIsValid = await validateAdminAccessToken(event.fetch, authToken);
	const authSession = tokenIsValid
		? getAdminSessionFromToken(
				authToken,
				authPathname,
				event.cookies.get(bdrAdminSessionCookie)
			)
		: null;
	if (authSession) {
		const routeTenant = getExternalAdminTenantForPath(authPathname);
		const claimedTenant = authSession.tenantId ? getTenantById(authSession.tenantId) : null;
		if (routeTenant && claimedTenant && routeTenant.id !== claimedTenant.id) {
			throw error(403, `This account does not have access to ${routeTenant.name}.`);
		}

		event.locals.adminSession = authSession;
		if (authSession.surface === 'external-admin' && authSession.role) {
			event.locals.bdrAdminSession = {
				role: authSession.role,
				source: 'auth-token'
			};
		}

		return resolve(event);
	}

	if (event.request.headers.get('accept')?.includes('text/html')) {
		const loginUrl = new URL(event.url);
		loginUrl.pathname = authPathname;
		throw redirect(303, buildLoginRedirect(loginUrl));
	}

	throw error(403, 'Admin access requires owner or office admin privileges.');
};
