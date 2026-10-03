import { error, redirect } from '@sveltejs/kit';
import { carlZipfTenant } from '$lib/config/tenants';
import { parseTechnicianContext } from '$lib/locksmith';
import { authTokenCookie, buildLoginRedirect, getAuthApiBaseUrl } from '$lib/server/auth-session';
import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ cookies, fetch, url, setHeaders }) => {
	setHeaders({ 'Cache-Control': 'private, no-store' });
	const token = cookies.get(authTokenCookie);
	if (!token) throw redirect(303, buildLoginRedirect(url));
	let response: Response;
	try {
		response = await fetch(`${getAuthApiBaseUrl()}/api/locksmith/me`, {
			headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' }
		});
	} catch {
		throw error(503, 'Technician access could not be verified. Reconnect and try again.');
	}
	if (response.status === 401) throw redirect(303, buildLoginRedirect(url));
	if (response.status === 403) throw error(403, 'Your account does not have access to this field workspace.');
	if (!response.ok) throw error(503, 'Technician access could not be verified. Please try again.');
	const payload: unknown = await response.json().catch(() => null);
	const envelope = payload && typeof payload === 'object' && 'success' in payload ? payload as { success: boolean; data?: unknown } : null;
	const techContext = parseTechnicianContext(envelope ? (envelope.success ? envelope.data : null) : payload, carlZipfTenant.id);
	if (!techContext) throw error(403, 'This account does not have access to Carl Zipf field work.');
	return { techContext };
};
