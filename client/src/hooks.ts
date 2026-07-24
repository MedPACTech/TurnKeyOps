import type { Reroute } from '@sveltejs/kit';
import { resolveProductionPathname } from '$lib/config/domains';

export const reroute: Reroute = ({ url }) => {
	const pathname = resolveProductionPathname(url.hostname, url.pathname);
	return pathname === url.pathname ? undefined : pathname;
};
