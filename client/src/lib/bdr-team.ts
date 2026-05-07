import type { QuoteRequest, QuoteRequestWorkflowTaskKey } from '$lib/quote-requests';

export type BdrEmployeeAccessRole = 'none' | 'field' | 'office-admin' | 'owner';

export type BdrEmployeeSkillKey =
	| 'intake-review'
	| 'customer-follow-up'
	| 'site-visit-scheduling'
	| 'field-inspection'
	| 'estimate-drafting'
	| 'estimate-send'
	| 'closeout';

export type BdrEmployeePermissionKey =
	| 'manage-quotes'
	| 'update-customer-details'
	| 'schedule-site-visits'
	| 'complete-site-visits'
	| 'draft-estimates'
	| 'send-estimates'
	| 'close-quotes'
	| 'manage-admin-access';

export type BdrEmployeeContact = {
	id: string;
	displayName: string;
	title: string;
	team: string;
	employmentType: 'Full time' | 'Part time' | 'Contractor' | 'Seasonal';
	email: string;
	phone: string;
	accessRole: BdrEmployeeAccessRole;
	skills: BdrEmployeeSkillKey[];
	permissions: BdrEmployeePermissionKey[];
	availability: string;
	workload: number;
};

export const bdrEmployeeSkillMeta: Record<BdrEmployeeSkillKey, { label: string; detail: string }> = {
	'intake-review': {
		label: 'Intake review',
		detail: 'Can validate new public-site requests and decide the next office step.'
	},
	'customer-follow-up': {
		label: 'Customer follow-up',
		detail: 'Can request missing scope, contact, readiness, and scheduling details.'
	},
	'site-visit-scheduling': {
		label: 'Site visit scheduling',
		detail: 'Can book or move site visits and coordinate calendar ownership.'
	},
	'field-inspection': {
		label: 'Field inspection',
		detail: 'Can complete property visits and return site notes for estimating.'
	},
	'estimate-drafting': {
		label: 'Estimate drafting',
		detail: 'Can prepare scope, line items, and quote packets.'
	},
	'estimate-send': {
		label: 'Estimate send',
		detail: 'Can issue estimates and manage customer follow-up.'
	},
	closeout: {
		label: 'Closeout',
		detail: 'Can mark quote outcomes and move won work into handoff.'
	}
};

export const bdrEmployeePermissionMeta: Record<BdrEmployeePermissionKey, { label: string; detail: string }> = {
	'manage-quotes': {
		label: 'Manage quotes',
		detail: 'Can update quote workspace ownership, next actions, and workflow activity.'
	},
	'update-customer-details': {
		label: 'Update customer details',
		detail: 'Can edit contact, site, and requested timing fields.'
	},
	'schedule-site-visits': {
		label: 'Schedule visits',
		detail: 'Can book site visits and assign field resources.'
	},
	'complete-site-visits': {
		label: 'Complete visits',
		detail: 'Can mark a site visit complete and move the quote into estimate prep.'
	},
	'draft-estimates': {
		label: 'Draft estimates',
		detail: 'Can prepare quote scope and pricing work.'
	},
	'send-estimates': {
		label: 'Send estimates',
		detail: 'Can send estimate packets and drive customer follow-up.'
	},
	'close-quotes': {
		label: 'Close quotes',
		detail: 'Can mark quotes won, lost, declined, or archived.'
	},
	'manage-admin-access': {
		label: 'Manage access',
		detail: 'Can update employee app access for admin users.'
	}
};

export const quoteWorkflowTaskRequirements: Record<
	QuoteRequestWorkflowTaskKey,
	{
		label: string;
		skill: BdrEmployeeSkillKey;
		permission: BdrEmployeePermissionKey;
	}
> = {
	'intake-review': {
		label: 'Review intake',
		skill: 'intake-review',
		permission: 'manage-quotes'
	},
	'customer-follow-up': {
		label: 'Chase missing info',
		skill: 'customer-follow-up',
		permission: 'update-customer-details'
	},
	'book-site-visit': {
		label: 'Book site visit',
		skill: 'site-visit-scheduling',
		permission: 'schedule-site-visits'
	},
	'complete-site-visit': {
		label: 'Complete site visit',
		skill: 'field-inspection',
		permission: 'complete-site-visits'
	},
	'draft-estimate': {
		label: 'Draft estimate',
		skill: 'estimate-drafting',
		permission: 'draft-estimates'
	},
	'send-estimate': {
		label: 'Send estimate',
		skill: 'estimate-send',
		permission: 'send-estimates'
	},
	closeout: {
		label: 'Close outcome',
		skill: 'closeout',
		permission: 'close-quotes'
	}
};

export const bdrEmployeeContacts: BdrEmployeeContact[] = [
	{
		id: 'employee-jordan-ellis',
		displayName: 'Jordan Ellis',
		title: 'Office admin',
		team: 'Office ops',
		employmentType: 'Full time',
		email: 'jordan@bdr-demo.local',
		phone: '704-555-0188',
		accessRole: 'office-admin',
		skills: ['intake-review', 'customer-follow-up', 'site-visit-scheduling', 'estimate-send'],
		permissions: ['manage-quotes', 'update-customer-details', 'schedule-site-visits', 'send-estimates'],
		availability: 'Office queue',
		workload: 2
	},
	{
		id: 'employee-casey-morgan',
		displayName: 'Casey Morgan',
		title: 'Estimator / field lead',
		team: 'Field estimating',
		employmentType: 'Full time',
		email: 'casey@bdr-demo.local',
		phone: '704-555-0142',
		accessRole: 'field',
		skills: ['field-inspection', 'estimate-drafting'],
		permissions: ['complete-site-visits', 'draft-estimates'],
		availability: 'Field today',
		workload: 1
	},
	{
		id: 'employee-riley-stone',
		displayName: 'Riley Stone',
		title: 'Estimator',
		team: 'Estimating desk',
		employmentType: 'Contractor',
		email: 'riley@bdr-demo.local',
		phone: '704-555-0164',
		accessRole: 'field',
		skills: ['site-visit-scheduling', 'field-inspection', 'estimate-drafting'],
		permissions: ['schedule-site-visits', 'complete-site-visits', 'draft-estimates'],
		availability: 'Available',
		workload: 3
	},
	{
		id: 'employee-ella-owner',
		displayName: 'Ella Robinson',
		title: 'Owner',
		team: 'Leadership',
		employmentType: 'Full time',
		email: 'ella@bdr-demo.local',
		phone: '704-555-0199',
		accessRole: 'owner',
		skills: ['closeout', 'estimate-send', 'customer-follow-up'],
		permissions: ['close-quotes', 'send-estimates', 'manage-quotes', 'manage-admin-access'],
		availability: 'Owner review',
		workload: 4
	}
];

export const getBdrEmployeeByName = (name: string | null | undefined) =>
	bdrEmployeeContacts.find((employee) => employee.displayName === name?.trim()) ?? null;

export const getEligibleBdrEmployeesForTask = (taskKey: QuoteRequestWorkflowTaskKey | null | undefined) => {
	if (!taskKey) return [];
	const requirement = quoteWorkflowTaskRequirements[taskKey];
	return bdrEmployeeContacts
		.filter(
			(employee) =>
				employee.skills.includes(requirement.skill) &&
				employee.permissions.includes(requirement.permission)
		)
		.sort((a, b) => a.workload - b.workload || a.displayName.localeCompare(b.displayName));
};

export const getRecommendedBdrEmployeeForTask = (
	taskKey: QuoteRequestWorkflowTaskKey | null | undefined,
	_request?: QuoteRequest | null
) => getEligibleBdrEmployeesForTask(taskKey)[0] ?? null;
