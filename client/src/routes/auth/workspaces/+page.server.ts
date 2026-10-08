import { fail, redirect } from '@sveltejs/kit';
import { tenants } from '$lib/config/tenants';
import { authTokenCookie } from '$lib/server/auth-session';
import { loginDestination } from '$lib/server/tenant-auth';
import { pendingTokenCookie, workspaceRequest, selectWorkspace, saveWorkspaceSession } from '$lib/server/workspaces';
export const load = async ({ cookies, fetch, url }) => {
    const token = cookies.get(pendingTokenCookie) || cookies.get(authTokenCookie);
    if (!token) redirect(303, '/auth/login');
    let ids: Array<{ tenantId: string }>;
    try { ids = await workspaceRequest(fetch, token); }
    catch { return { workspaces: [], returnTo: '', message: 'Unable to load workspaces. Sign out and sign in again.' }; }
    return { workspaces: tenants.filter(t => ids.some(i => i.tenantId === t.id)).map(t => ({ slug: t.slug, name: t.name })), returnTo: url.searchParams.get('returnTo') ?? '' };
};
export const actions = { default: async ({ request, cookies, fetch, url }) => {
    const token = cookies.get(pendingTokenCookie) || cookies.get(authTokenCookie);
    if (!token) redirect(303, '/auth/login');
    const form = await request.formData();
    const tenant = tenants.find(t => t.slug === form.get('tenant'));
    if (!tenant) return fail(400, { message: 'Choose a valid workspace.' });
    try { saveWorkspaceSession(cookies, url, await selectWorkspace(fetch, token, tenant.id)); }
    catch (cause) { return fail(403, { message: cause instanceof Error ? cause.message : 'Workspace access denied.' }); }
    redirect(303, loginDestination(tenant.slug, String(form.get('returnTo') ?? '')));
} };
