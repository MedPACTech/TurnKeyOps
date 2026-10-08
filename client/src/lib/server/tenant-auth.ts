import { error } from '@sveltejs/kit';
import { tenants } from '$lib/config/tenants';
import { getSafeAdminReturnTo } from './session-policy';

export const tenantForPath = (path: string) => tenants.find(t => path.startsWith(`/${t.slug}/`));
export const tenantLoginUrl = (returnTo: string) => `${tenantForPath(returnTo) ? `/${tenantForPath(returnTo)!.slug}` : ''}/auth/login?returnTo=${encodeURIComponent(returnTo)}`;
export const loginDestination = (slug: string | undefined, requested: string | null) => {
    if (!slug && requested === '/auth/workspaces') return requested;
    if (!slug) return requested ? getSafeAdminReturnTo(requested) : '/auth/workspaces';
    const tenant = tenants.find(t => t.slug === slug);
    if (!tenant) error(404, 'Unknown workspace');
    const safe = requested ? getSafeAdminReturnTo(requested) : tenant.adminPath;
    return tenantForPath(safe)?.id === tenant.id ? safe : tenant.adminPath;
};
