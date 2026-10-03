import { error } from '@sveltejs/kit';
import { apiRequest } from '$lib/api/client';
import { authTokenCookie } from '$lib/server/auth-session';
import { carlZipfTenant } from '$lib/config/tenants';
import type { RequestEvent } from '@sveltejs/kit';
import type { QuoteRequest } from '$lib/quote-requests';
import type { CustomerRecord } from './records';
export type IntakeRecord = QuoteRequest & { updatedAtUtc: string };
export const sessionToken = (event: Pick<RequestEvent, 'locals' | 'cookies'>) => {
 if (!event.locals.bdrAdminSession || event.locals.adminSession?.tenantId !== carlZipfTenant.id) throw error(403, 'Carl Zipf admin access is required.');
 const token = event.cookies.get(authTokenCookie);
 if (!token) throw error(401, 'Sign in to access customer records.');
 return token;
};
export const fetchIntake = async (fetcher: typeof fetch, token: string) => {
 const records = await apiRequest<IntakeRecord[]>('/api/quote-requests', {}, fetcher, token);
 return records.filter(record => record.tenantId === carlZipfTenant.id).sort((a, b) => b.submittedAtUtc.localeCompare(a.submittedAtUtc));
};
export const fetchCustomers = (fetcher: typeof fetch, token: string, query = '') => apiRequest<CustomerRecord[]>(`/api/Customers/search?query=${encodeURIComponent(query)}`, {}, fetcher, token);
