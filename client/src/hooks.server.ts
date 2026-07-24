import { error, redirect, type Handle } from '@sveltejs/kit';
import {
	authTokenCookie,
	bdrAdminContactCookie,
	bdrAdminSessionCookie,
	buildLoginRedirect,
	getAdminSessionFromToken,
	isAdminPath,
	isExternalAdminPath
} from '$lib/server/auth-session';
import { getPersistedBdrAdminRole } from '$lib/server/bdr-contact-access';
import { resolveProductionPathname } from '$lib/config/domains';

export const handle: Handle = async ({ event, resolve }) => {
	const authPathname = resolveProductionPathname(event.url.hostname, event.url.pathname);

	if (!isAdminPath(authPathname)) {
		return resolve(event);
	}

	const authSession = getAdminSessionFromToken(
		event.cookies.get(authTokenCookie),
		authPathname,
		event.cookies.get(bdrAdminSessionCookie)
	);
	if (authSession) {
		event.locals.adminSession = authSession;
		if (authSession.surface === 'external-admin' && authSession.role) {
			event.locals.bdrAdminSession = {
				role: authSession.role,
				source: 'auth-token'
			};
		}

		return resolve(event);
	}

	const persistedRole = isExternalAdminPath(authPathname)
		? await getPersistedBdrAdminRole(event.cookies.get(bdrAdminContactCookie))
		: null;
	if (persistedRole) {
		event.locals.adminSession = {
			surface: 'external-admin',
			role: persistedRole,
			email: '',
			tenantId: '',
			source: 'contact-access'
		};
		event.locals.bdrAdminSession = {
			role: persistedRole,
			source: 'contact-access'
		};
		return resolve(event);
	}

	if (event.request.headers.get('accept')?.includes('text/html')) {
		const loginUrl = new URL(event.url);
		loginUrl.pathname = authPathname;
		throw redirect(303, buildLoginRedirect(loginUrl));
	}

	throw error(403, 'Admin access requires owner or office admin privileges.');
};
