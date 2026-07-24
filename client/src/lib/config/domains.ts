export type ProductionSurface =
	| 'turnkeyops-public'
	| 'turnkeyops-admin'
	| 'thinkpink-public'
	| 'thinkpink-admin'
	| 'bdr-public'
	| 'bdr-admin';

export type ProductionDomain = {
	hostname: string;
	surface: ProductionSurface;
	routePrefix: string;
};

export const productionDomains: ProductionDomain[] = [
	{
		hostname: 'turnkeyops.ai',
		surface: 'turnkeyops-public',
		routePrefix: '/turnkeyops/public'
	},
	{
		hostname: 'www.turnkeyops.ai',
		surface: 'turnkeyops-public',
		routePrefix: '/turnkeyops/public'
	},
	{
		hostname: 'admin.turnkeyops.ai',
		surface: 'turnkeyops-admin',
		routePrefix: '/turnkeyops/admin'
	},
	{
		hostname: 'thinkpinklc.com',
		surface: 'thinkpink-public',
		routePrefix: '/thinkpink/public'
	},
	{
		hostname: 'www.thinkpinklc.com',
		surface: 'thinkpink-public',
		routePrefix: '/thinkpink/public'
	},
	{
		hostname: 'admin.thinkpinklc.com',
		surface: 'thinkpink-admin',
		routePrefix: '/thinkpink/admin'
	},
	{
		hostname: 'bdrconcrete.com',
		surface: 'bdr-public',
		routePrefix: '/bdr/public'
	},
	{
		hostname: 'www.bdrconcrete.com',
		surface: 'bdr-public',
		routePrefix: '/bdr/public'
	},
	{
		hostname: 'admin.bdrconcrete.com',
		surface: 'bdr-admin',
		routePrefix: '/bdr/admin'
	}
];

const productionDomainByHostname = new Map(
	productionDomains.map((domain) => [domain.hostname, domain])
);

export const getProductionDomain = (hostname: string) =>
	productionDomainByHostname.get(hostname.toLowerCase()) ?? null;

const applicationRoutePrefixes = ['/turnkeyops/', '/thinkpink/', '/bdr/', '/auth/'];
const assetRoutePrefixes = ['/_app/', '/clientFiles/'];

export const shouldBypassDomainReroute = (pathname: string) =>
	applicationRoutePrefixes.some((prefix) => pathname.startsWith(prefix)) ||
	assetRoutePrefixes.some((prefix) => pathname.startsWith(prefix)) ||
	pathname === '/robots.txt' ||
	pathname.includes('.');

export const resolveProductionPathname = (hostname: string, pathname: string) => {
	const domain = getProductionDomain(hostname);
	if (!domain || shouldBypassDomainReroute(pathname)) {
		return pathname;
	}

	if (pathname === '/') {
		return domain.routePrefix;
	}

	return `${domain.routePrefix}${pathname}`;
};
