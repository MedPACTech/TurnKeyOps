import { error } from '@sveltejs/kit';
import { carlZipfTenant } from '$lib/config/tenants';
import { parseTechnicianContext } from '$lib/locksmith';
import { authTokenCookie, getAuthApiBaseUrl } from '$lib/server/auth-session';
import type { RequestEvent } from '@sveltejs/kit';

export async function fieldApi(event: RequestEvent) {
	const token = event.cookies.get(authTokenCookie);
	if (!token) throw error(401, 'Sign in again before handing off this draft.');
	const call = (path: string, init: RequestInit = {}) => event.fetch(`${getAuthApiBaseUrl()}${path}`, { ...init, headers: { Accept: 'application/json', Authorization: `Bearer ${token}`, ...(init.body && !(init.body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}), ...init.headers }, signal: AbortSignal.timeout(30_000) });
	const response = await call('/api/locksmith/me');
	if (!response.ok) throw error(response.status === 401 || response.status === 403 ? response.status : 503, 'Your field access could not be verified. Reconnect or sign in again.');
	const payload = await response.json();
	const context = parseTechnicianContext(payload && 'success' in payload ? payload.success ? payload.data : null : payload, carlZipfTenant.id);
	if (!context) throw error(403, 'This account cannot access Carl Zipf field work.');
	return { call, context };
}

export async function fieldResponse<T>(response: Response): Promise<T> {
	const payload = await response.json().catch(() => null);
	if (!response.ok || payload?.success === false) {
		const message = payload?.errors?.map?.((item: { message?: string }) => item.message).filter(Boolean).join(', ') || payload?.message || payload?.title || payload?.error;
		throw error(response.ok ? 502 : response.status, typeof message === 'string' ? message : `Shared API request failed (${response.status}).`);
	}
	return payload && 'success' in payload ? payload.data as T : payload as T;
}

export function requireFieldOrigin(event: RequestEvent) {
	if (event.request.headers.get('origin') !== event.url.origin) throw error(403, 'Submit field work from this workspace.');
}
