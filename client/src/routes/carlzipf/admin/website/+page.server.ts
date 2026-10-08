import { fail } from '@sveltejs/kit';
import { loadCarlZipfDocument, normalizeCarlZipfContent } from '$lib/server/carlzipf-site-content';
import { updateTenantSettings } from '$lib/api/tenant-settings';
import { authTokenCookie } from '$lib/server/auth-session';
export const load = async ({ fetch }) => {
 const document = await loadCarlZipfDocument(fetch);
 return { content: normalizeCarlZipfContent(document.values.carlZipfSite), version: document.version ?? '' };
};
export const actions = {
 default: async ({ request, fetch, cookies }) => {
  const fields = await request.formData();
  let input: unknown;
  try { input = JSON.parse(String(fields.get('content'))); } catch { return fail(400, { error: 'Enter valid content JSON.' }); }
  if (!input || typeof input !== 'object' || Array.isArray(input)) return fail(400, { error: 'Content must be an object.' });
  try {
   const document = await loadCarlZipfDocument(fetch);
   if ((document.version ?? '') !== String(fields.get('version'))) return fail(409, { error: 'Content changed. Reload before saving.' });
   const saved = await updateTenantSettings('public-content', { navigation: {}, hero: {}, services: {}, quoteForm: {}, footer: {}, ...document.values, carlZipfSite: normalizeCarlZipfContent(input) }, document.version, fetch, cookies.get(authTokenCookie));
   return { saved: true, version: saved.version };
  } catch { return fail(502, { error: 'Could not save content. Reload and try again.' }); }
 }
};
