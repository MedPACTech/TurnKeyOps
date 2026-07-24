import type { BdrAdminNavItem } from '$lib/config/platform';
import { getTenant, type TenantDefinition, type TenantSlug } from '$lib/config/tenants';

export type ExternalAdminModule =
	| 'bob'
	| 'dashboard'
	| 'calendar'
	| 'jobs'
	| 'requests'
	| 'estimates'
	| 'invoices'
	| 'customers'
	| 'settings';

export type ExternalAdminTheme = {
	accent: string;
	accentHover: string;
	accentSoft: string;
	accentBorder: string;
	accentText: string;
};

export type ExternalAdminConfig = {
	tenant: TenantDefinition;
	homeHref: string;
	publicHref: string;
	workspaceLabel: string;
	workspaceSummary: string;
	navigation: BdrAdminNavItem[];
	theme: ExternalAdminTheme;
};

type ModuleDefinition = Omit<BdrAdminNavItem, 'href'>;

const modules: Record<ExternalAdminModule, ModuleDefinition> = {
	bob: { slug: 'bob', label: 'Ask Bob', summary: 'AI operating partner for priorities, decisions, and approved actions', contextLabel: 'AI Ops', focusLabel: 'Daily briefing', canvasLabel: 'Decision canvas', section: 'overview' },
	dashboard: { slug: 'dashboard', label: 'Dashboard', summary: 'Pipeline, schedule, workload, and cash visibility', contextLabel: 'Admin', focusLabel: 'Control room', canvasLabel: 'Operating canvas', section: 'overview' },
	calendar: { slug: 'calendar', label: 'Calendar', summary: 'Appointments, site visits, production dates, and crew availability', contextLabel: 'Operations', focusLabel: 'Scheduling desk', canvasLabel: 'Dispatch canvas', section: 'operations' },
	jobs: { slug: 'jobs', label: 'Jobs', summary: 'Active work, field status, holds, and completion controls', contextLabel: 'Production Ops', focusLabel: 'Run desk', canvasLabel: 'Job canvas', section: 'operations' },
	requests: { slug: 'requests', label: 'Requests', summary: 'Public-site intake, triage, follow-up, and conversion', contextLabel: 'Intake Ops', focusLabel: 'Request inbox', canvasLabel: 'Message canvas', section: 'customers' },
	estimates: { slug: 'estimates', label: 'Estimates', summary: 'Estimate preparation, approval, deposits, and contract status', contextLabel: 'Sales Ops', focusLabel: 'Pipeline lane', canvasLabel: 'Estimate canvas', section: 'revenue' },
	invoices: { slug: 'invoices', label: 'Invoices', summary: 'Billing status, payment holds, and collections', contextLabel: 'Finance Ops', focusLabel: 'Collections lane', canvasLabel: 'Billing canvas', section: 'revenue' },
	customers: { slug: 'customers', label: 'Contacts', summary: 'Customers, properties, files, and communication history', contextLabel: 'Relationship Ops', focusLabel: 'Relationship desk', canvasLabel: 'Record canvas', section: 'customers' },
	settings: { slug: 'settings', label: 'Admin', summary: 'Trade defaults, website controls, and workspace configuration', contextLabel: 'Admin Ops', focusLabel: 'Configuration', canvasLabel: 'Settings canvas', section: 'admin' }
};

const tenantModules: Record<TenantSlug, ExternalAdminModule[]> = {
	bdr: ['bob', 'dashboard', 'calendar', 'jobs', 'requests', 'estimates', 'invoices', 'customers', 'settings'],
	thinkpink: ['bob', 'dashboard', 'requests', 'estimates', 'jobs', 'settings']
};

const themes: Record<TenantSlug, ExternalAdminTheme> = {
	bdr: { accent: '#f97316', accentHover: '#ea580c', accentSoft: '#fff7ed', accentBorder: '#fed7aa', accentText: '#c2410c' },
	thinkpink: { accent: '#d40f80', accentHover: '#a50c64', accentSoft: '#fff0f7', accentBorder: '#f5b6d5', accentText: '#a50c64' }
};

const adminBase = (tenant: TenantDefinition) => tenant.adminPath.replace(/\/bob$/, '');

const moduleHref = (tenant: TenantDefinition, module: ExternalAdminModule) => {
	if (tenant.slug === 'bdr' && module === 'customers') return `${adminBase(tenant)}/contact`;
	return `${adminBase(tenant)}/${module}`;
};

export const getExternalAdminConfig = (slug: TenantSlug): ExternalAdminConfig => {
	const tenant = getTenant(slug);
	if (!tenant) throw new Error(`Unknown External Admin tenant: ${slug}`);

	const navigation = tenantModules[slug].map((module) => ({
		...modules[module],
		href: moduleHref(tenant, module)
	}));

	return {
		tenant,
		homeHref: navigation[0]?.href ?? tenant.adminPath,
		publicHref: tenant.publicPath,
		workspaceLabel: `${tenant.shortName} Admin`,
		workspaceSummary: `${tenant.tradeLabel} workspace for estimating, scheduling, field operations, and customer follow-through.`,
		navigation,
		theme: themes[slug]
	};
};

export const normalizeExternalAdminPath = (config: ExternalAdminConfig, pathname: string) =>
	pathname === adminBase(config.tenant) || pathname === `${adminBase(config.tenant)}/`
		? config.homeHref
		: pathname;

export const getExternalAdminActiveNav = (config: ExternalAdminConfig, pathname: string) =>
	config.navigation.find((item) => pathname === item.href || pathname.startsWith(`${item.href}/`)) ??
	config.navigation[0];

export const getExternalAdminTenantForPath = (pathname: string) =>
	(['bdr', 'thinkpink'] as TenantSlug[])
		.map((slug) => getExternalAdminConfig(slug).tenant)
		.find((tenant) => {
			const base = adminBase(tenant);
			return pathname === base || pathname.startsWith(`${base}/`);
		}) ?? null;
