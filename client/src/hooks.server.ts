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

export const handle: Handle = async ({ event, resolve }) => {
	if (!isAdminPath(event.url.pathname)) {
		return resolve(event);
	}

	const authSession = getAdminSessionFromToken(
		event.cookies.get(authTokenCookie),
		event.url.pathname,
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

	const persistedRole = isExternalAdminPath(event.url.pathname)
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
		throw redirect(303, buildLoginRedirect(event.url));
	}

	throw error(403, 'Admin access requires owner or office admin privileges.');
};
