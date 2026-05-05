import { error, redirect, type Handle } from '@sveltejs/kit';
import { isBdrAdminViewRole } from '$lib/config/platform';
import { getPersistedBdrAdminRole } from '$lib/server/bdr-contact-access';

const isBdrAdminPath = (pathname: string) => pathname === '/bdr/admin' || pathname.startsWith('/bdr/admin/');
const bdrAdminSessionCookie = 'tko_bdr_admin_role';
const bdrAdminContactCookie = 'tko_bdr_admin_contact_id';

const buildLoginRedirect = (url: URL) => {
	const returnTo = `${url.pathname}${url.search}`;
	return `/auth/login?returnTo=${encodeURIComponent(returnTo)}`;
};

export const handle: Handle = async ({ event, resolve }) => {
	if (!isBdrAdminPath(event.url.pathname)) {
		return resolve(event);
	}

	const devBootstrapRole = event.url.searchParams.get('role');
	const sessionRole = event.cookies.get(bdrAdminSessionCookie);
	const persistedRole = await getPersistedBdrAdminRole(event.cookies.get(bdrAdminContactCookie));

	if (persistedRole) {
		event.locals.bdrAdminSession = {
			role: persistedRole,
			source: 'session'
		};
		return resolve(event);
	}

	if (isBdrAdminViewRole(sessionRole)) {
		event.locals.bdrAdminSession = {
			role: sessionRole,
			source: 'session'
		};
		return resolve(event);
	}

	if (isBdrAdminViewRole(devBootstrapRole)) {
		event.cookies.set(bdrAdminSessionCookie, devBootstrapRole, {
			path: '/bdr/admin',
			httpOnly: true,
			sameSite: 'lax',
			secure: event.url.protocol === 'https:',
			maxAge: 60 * 60 * 8
		});

		event.locals.bdrAdminSession = {
			role: devBootstrapRole,
			source: 'dev-bootstrap'
		};
		return resolve(event);
	}

	if (event.request.headers.get('accept')?.includes('text/html')) {
		throw redirect(303, buildLoginRedirect(event.url));
	}

	throw error(403, 'Admin access requires owner or office admin privileges.');
};
