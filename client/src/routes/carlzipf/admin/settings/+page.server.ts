import { fail } from '@sveltejs/kit';
import { authTokenCookie } from '$lib/server/auth-session';
import { loadLocksmithSettings, saveLocksmithSettings } from '$lib/server/locksmith-settings';
import type { LocksmithSettings } from '$lib/server/locksmith-settings';
import { locksmithLaborDefaults } from '$lib/locksmith';
export const load = async ({ fetch, cookies }) => {
 const { document, settings } = await loadLocksmithSettings(fetch, cookies.get(authTokenCookie));
 return { settings, version: document.version };
};
export const actions = {
 save: async ({ request, fetch, cookies }) => {
  const form = await request.formData();
  try {
   const numeric = (key: string, nullable = false) => {
    const value = String(form.get(key) ?? '').trim();
    if (!value && nullable) return null;
    const result = Number(value);
    if (!value || !Number.isFinite(result) || result < 0) throw new Error('Enter valid non-negative pricing values.');
    return result;
   };
   const maxDiscountPercent = numeric('maxDiscountPercent')!;
   if (maxDiscountPercent > 100) throw new Error('Maximum discount must be between 0 and 100%.');
   const taxPercent = numeric('taxPercent', true);
   if (taxPercent !== null && taxPercent > 100) throw new Error('Tax rate must be between 0 and 100%.');
   const laborHoursByJobType = structuredClone(locksmithLaborDefaults);
   for (const jobType of ['residential', 'commercial'] as const) for (const service of Object.keys(locksmithLaborDefaults[jobType])) {
    const hours = numeric(`laborHours:${jobType}:${service}`)!;
    if (hours <= 0 || hours > 100) throw new Error('Standard labor hours must be greater than 0 and no more than 100.');
    laborHoursByJobType[jobType][service] = hours;
   }
   const { settings } = await loadLocksmithSettings(fetch, cookies.get(authTokenCookie));
   await saveLocksmithSettings({ ...settings, pricing: { ...settings.pricing,
    laborRatePerHour: numeric('laborRatePerHour', true), maxDiscountPercent,
    taxPercent, laborHoursByJobType,
    approvalAboveTotal: numeric('approvalAboveTotal', true), requireOfficeApproval: form.has('requireOfficeApproval')
   } }, String(form.get('version') ?? ''), fetch, cookies.get(authTokenCookie));
   return { message: 'Quote policy saved.' };
  } catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save policy.' }); }
 },
 catalog: async ({ request, fetch, cookies }) => {
  const form = await request.formData();
  try {
   const input = JSON.parse(String(form.get('catalogJson') ?? '')) as unknown;
   if (!Array.isArray(input) || input.length > 1000) throw new Error('Provide up to 1,000 catalog items.');
   const ids = new Set<string>();
   const catalog: LocksmithSettings['catalog'] = input.map((raw: unknown) => {
    if (!raw || typeof raw !== 'object') throw new Error('Each catalog item needs an ID, name, price and job type.');
    const item = raw as Record<string, unknown>;
    const id = String(item.id ?? '').trim();
    const name = String(item.name ?? '').trim();
    const price = item.unitPrice;
    const jobTypes = item.jobTypes;
    if (!id || id.length > 100 || ids.has(id) || !name || name.length > 200 ||
     typeof price !== 'number' || !Number.isFinite(price) || price < 0 || price > 1_000_000 ||
     !Array.isArray(jobTypes) || jobTypes.length < 1 || jobTypes.length > 2 ||
     jobTypes.some(type => type !== 'residential' && type !== 'commercial') ||
     new Set(jobTypes).size !== jobTypes.length || typeof item.sample !== 'boolean')
     throw new Error('Each catalog item needs a unique ID, name, valid price, job types and sample status.');
    ids.add(id);
    const externalId = String(item.externalId ?? '').trim();
    if (externalId.length > 100) throw new Error('External product IDs must be 100 characters or fewer.');
    return { id, name, unitPrice: price, jobTypes, sample: item.sample, ...(externalId ? { externalId } : {}) };
   });
   const { settings } = await loadLocksmithSettings(fetch, cookies.get(authTokenCookie));
   await saveLocksmithSettings({ ...settings, catalog }, String(form.get('version') ?? ''), fetch, cookies.get(authTokenCookie));
   return { message: 'Hardware catalog saved.' };
  } catch (cause) { return fail(400, { error: cause instanceof Error ? cause.message : 'Could not save catalog.' }); }
 }
};
