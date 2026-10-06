import { env } from '$env/dynamic/private';
import type { Cookies } from '@sveltejs/kit';
import { authTokenCookie, authRefreshTokenCookie, extractAccessToken, extractRefreshToken, getAuthApiBaseUrl, validateAdminAccessToken, type AuthResult } from './auth-session';
import { decodeJwtClaims } from './session-policy';
import { tenants } from '$lib/config/tenants';
export const tokenTenantId = (token: string | undefined) => { const c = decodeJwtClaims(token); return String(c.tenant_id ?? c.tenant ?? c.tid ?? '').toLowerCase(); };
export const pendingTokenCookie = 'tko_workspace_token';
export async function workspaceRequest(fetch: typeof globalThis.fetch, token: string, tenantId?: string) {
    const response = await fetch(`${getAuthApiBaseUrl()}/api/auth/workspaces`, {
        method: tenantId ? 'POST' : 'GET',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        ...(tenantId ? { body: JSON.stringify({ tenantId }) } : {})
    });
    if (!response.ok) throw new Error('Unable to access this workspace. Check your membership or sign in again.');
    const payload = await response.json();
    if (payload.success === false) throw new Error('Workspace access denied.');
    return payload.data ?? payload;
}
export async function selectWorkspace(fetch: typeof globalThis.fetch, token: string, tenantId: string): Promise<AuthResult> {
    const result = await workspaceRequest(fetch, token, tenantId);
    const access = extractAccessToken(result);
    const tenant = tenants.find(t => t.id === tenantId);
    if (!tenant || !access || !await validateAdminAccessToken(fetch, access) || tokenTenantId(access) !== tenantId)
        throw new Error('The selected workspace did not return a valid admin session.');
    return result;
}
export function saveWorkspaceSession(cookies: Cookies, url: URL, result: AuthResult) {
    const options = { path: '/', httpOnly: true, sameSite: 'strict' as const, secure: env.NODE_ENV === 'production' || url.protocol === 'https:' };
    cookies.set(authTokenCookie, extractAccessToken(result)!, { ...options, maxAge: 28800 });
    cookies.delete(authRefreshTokenCookie, options);
    const refresh = extractRefreshToken(result);
    if (refresh) cookies.set(authRefreshTokenCookie, refresh, { ...options, maxAge: 2592000 });
    cookies.delete(pendingTokenCookie, options);
}
