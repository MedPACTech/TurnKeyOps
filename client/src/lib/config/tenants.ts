export type TradeProfileId = 'concrete-construction' | 'land-clearing';
export type TenantSlug = 'bdr' | 'thinkpink';

export type TenantDefinition = {
	id: string;
	slug: TenantSlug;
	name: string;
	shortName: string;
	tradeProfile: TradeProfileId;
	tradeLabel: string;
	status: 'live' | 'configuration';
	publicPath: string;
	adminPath: string;
	accent: string;
	services: string[];
	estimateInputs: string[];
	jobStages: string[];
	bobContext: string;
};

export const tenants: TenantDefinition[] = [
	{
		id: '7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b',
		slug: 'bdr',
		name: 'BDR Construction',
		shortName: 'BDR',
		tradeProfile: 'concrete-construction',
		tradeLabel: 'Concrete and construction',
		status: 'live',
		publicPath: '/bdr/public',
		adminPath: '/bdr/admin/bob',
		accent: '#f97316',
		services: ['Concrete', 'Site preparation', 'Exterior construction'],
		estimateInputs: ['Length', 'Width', 'Depth', 'Square footage', 'Concrete yards', 'Finish'],
		jobStages: ['Intake', 'Site visit', 'Estimate', 'Prep', 'Production', 'Closeout'],
		bobContext: 'Concrete estimating, site preparation, production scheduling, weather windows, and job closeout.'
	},
	{
		id: '88888888-8888-8888-8888-888888888882',
		slug: 'thinkpink',
		name: 'Think Pink Land Clearing',
		shortName: 'Think Pink',
		tradeProfile: 'land-clearing',
		tradeLabel: 'Land clearing and tree removal',
		status: 'configuration',
		publicPath: '/thinkpink/public',
		adminPath: '/thinkpink/admin',
		accent: '#d40f80',
		services: ['Land clearing', 'Forestry mulching', 'Stump grinding', 'Right-of-way and trails', 'Storm cleanup'],
		estimateInputs: ['Acreage', 'Vegetation density', 'Tree count and diameter', 'Terrain', 'Access', 'Disposal method', 'Restoration'],
		jobStages: ['Intake', 'Property assessment', 'Estimate', 'Mobilization', 'Clearing', 'Disposal', 'Restoration', 'Closeout'],
		bobContext: 'Land clearing, tree and stump removal, forestry mulching, equipment access, hauling, disposal, grading, and restoration.'
	}
];

export const getTenant = (slug: string) => tenants.find((tenant) => tenant.slug === slug);
export const getTenantById = (id: string) => tenants.find((tenant) => tenant.id === id);
export const bdrTenant = tenants[0];
export const thinkPinkTenant = tenants[1];
