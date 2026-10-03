import { fieldApi, fieldResponse } from './field-api.server';
import type { FieldPricingContext } from '$lib/locksmith-handoff';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async (event) => {
	await event.parent();
	try {
		const { call, context } = await fieldApi(event);
		const pricing = await fieldResponse<FieldPricingContext>(await call('/api/quote-estimates/locksmith-context'));
		return { pricingContext: { ...pricing, catalog: pricing.catalog.filter((item) => item.sample === false && item.jobTypes.some((jobType) => context.capabilities.includes(jobType))) }, pricingError: '' };
	} catch { return { pricingContext: null, pricingError: 'Configured pricing could not be loaded. You can still capture a device draft; reconnect and reload before preparing a quote.' }; }
};
