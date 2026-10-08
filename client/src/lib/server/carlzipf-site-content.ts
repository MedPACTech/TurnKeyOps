import { defaultCarlZipfContent, type CarlZipfSiteContent } from '$lib/carlzipf-site-content';
import { getPublicTenantContent } from '$lib/api/tenant-settings';
import { carlZipfTenant } from '$lib/config/tenants';

export function normalizeCarlZipfContent(value: unknown): CarlZipfSiteContent {
 const merge = (fallback: any, input: any): any => {
  if (typeof fallback === 'string') {
   if (typeof input !== 'string' || !input.trim() || input.length > 5000) return fallback;
   if (fallback.startsWith('#') && !/^(#[a-zA-Z][\w-]*|\/(?!\/)[^<>\\]*|https:\/\/[^<>\\]+|tel:\+?[\d-]+)$/.test(input)) return fallback;
   if (fallback.startsWith('/carlzipf/') && !/^\/[^/][^"<>\\]*$/.test(input)) return fallback;
   return input;
  }
  if (Array.isArray(fallback)) return Array.isArray(input) && input.length <= 20 ? input.map((item) => merge(fallback[0], item)) : fallback;
  return Object.fromEntries(Object.entries(fallback).map(([key, item]) => [key, merge(item, input?.[key])]));
 };
 return merge(defaultCarlZipfContent, value);
}
export const loadCarlZipfDocument = (fetcher: typeof fetch) => getPublicTenantContent<{ carlZipfSite?: CarlZipfSiteContent }>(carlZipfTenant.id, fetcher);
export async function loadCarlZipfContent(fetcher: typeof fetch) {
 try { return normalizeCarlZipfContent((await loadCarlZipfDocument(fetcher)).values.carlZipfSite); }
 catch { return structuredClone(defaultCarlZipfContent); }
}
