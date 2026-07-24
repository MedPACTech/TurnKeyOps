export const bdrAdminRoles = ['owner', 'office-admin', 'estimator-crew-lite'] as const;

export type BdrAdminRole = (typeof bdrAdminRoles)[number];

export type SurfaceDefinition = {
	slug: string;
	title: string;
	path: string;
	audience: string;
	status: 'active' | 'planned';
	description: string;
	highlight: string;
	theme: 'platform' | 'tenant' | 'operations';
};

export type BdrAdminNavItem = {
	slug: string;
	label: string;
	href: string;
	summary: string;
	contextLabel: string;
	focusLabel: string;
	canvasLabel: string;
	section: 'overview' | 'operations' | 'revenue' | 'customers' | 'content' | 'admin';
};

export type TurnkeyOpsAdminNavItem = {
	slug: string;
	label: string;
	href: string;
	summary: string;
	contextLabel: string;
	focusLabel: string;
	canvasLabel: string;
	allNav?: TurnkeyOpsAdminNavItem[];
};

export type StatCard = {
	label: string;
	value: string;
	detail: string;
};

export type BdrAdminShellMetric = {
	label: string;
	value: string;
	detail: string;
};

export type BdrAdminShellNote = {
	title: string;
	detail: string;
};

export type BdrAdminShellAction = {
	label: string;
	href: string;
	variant?: 'primary' | 'secondary';
};

export type BdrAdminShellState = {
	title: string;
	description: string;
	context: {
		label: string;
		title: string;
		summary: string;
		metrics: BdrAdminShellMetric[];
	};
	focus: {
		label: string;
		title: string;
		summary: string;
		notes: BdrAdminShellNote[];
	};
	canvas: {
		label: string;
		title: string;
		summary: string;
		actions: BdrAdminShellAction[];
	};
};

export const surfaceDefinitions: SurfaceDefinition[] = [
	{
		slug: 'turnkeyops-public',
		title: 'TurnKeyOps Platform',
		path: '/turnkeyops/public',
		audience: 'Prospects, partners, and future tenants',
		status: 'active',
		description:
			'Multi-tenant operations platform narrative for service businesses that need quoting, scheduling, billing, and customer workflows in one place.',
		highlight: 'Platform story is now distinct from BDR’s contractor-facing brand.',
		theme: 'platform'
	},
	{
		slug: 'bdr-public',
		title: 'BDR Construction',
		path: '/bdr/public',
		audience: 'Homeowners, property managers, and referral traffic',
		status: 'active',
		description:
			'Customer-facing first pass for BDR with services, trust signals, process, and fast paths to estimate requests.',
		highlight: 'BDR keeps a separate contractor identity as the first tenant on the platform, with black-and-orange primary branding.',
		theme: 'tenant'
	},
	{
		slug: 'bdr-admin',
		title: 'BDR Admin',
		path: '/bdr/admin',
		audience: 'Owner, office admin, estimator crew',
		status: 'active',
		description:
			'Operational workspace for scheduling, quoting, customer records, invoicing, weather checks, and payment tracking.',
		highlight: 'Built around the BDR MVP blueprint instead of placeholder admin cards.',
		theme: 'operations'
	},
	{
		slug: 'turnkeyops-admin',
		title: 'TurnKeyOps Operator Console',
		path: '/turnkeyops/admin',
		audience: 'Platform operators and implementation leads',
		status: 'active',
		description:
			'Internal console for tenant rollout, configuration priorities, implementation readiness, and platform health across the portfolio.',
		highlight: 'Now modeled as a real platform admin shell with route-based navigation and clear separation from BDR tenant workflows.',
		theme: 'platform'
	}
];

export const bdrAdminNavigation: BdrAdminNavItem[] = [
	{
		slug: 'bob',
		label: 'Ask Bob',
		href: '/bdr/admin/bob',
		summary: 'AI operating partner for priorities, decisions, and approved actions',
		contextLabel: 'AI Ops',
		focusLabel: 'Daily briefing',
		canvasLabel: 'Decision canvas',
		section: 'overview'
	},
	{
		slug: 'dashboard',
		label: 'Dashboard',
		href: '/bdr/admin/dashboard',
		summary: 'Pipeline, crew load, weather, and cash visibility',
		contextLabel: 'Admin',
		focusLabel: 'Control room',
		canvasLabel: 'Operating canvas',
		section: 'overview'
	},
	{
		slug: 'calendar',
		label: 'Calendar',
		href: '/bdr/admin/calendar',
		summary: 'Appointments, inspections, installs, and weather windows',
		contextLabel: 'Operations',
		focusLabel: 'Scheduling desk',
		canvasLabel: 'Dispatch canvas',
		section: 'operations'
	},
	{
		slug: 'jobs',
		label: 'Jobs',
		href: '/bdr/admin/jobs',
		summary: 'Production jobs, crew status, holds, and completion controls',
		contextLabel: 'Production Ops',
		focusLabel: 'Run desk',
		canvasLabel: 'Job canvas',
		section: 'operations'
	},
	{
		slug: 'requests',
		label: 'Quotes',
		href: '/bdr/admin/requests',
		summary: 'Public-site intake, triage, follow-up, and conversion handling',
		contextLabel: 'Intake Ops',
		focusLabel: 'Request inbox',
		canvasLabel: 'Message canvas',
		section: 'customers'
	},
	{
		slug: 'estimates',
		label: 'Estimates',
		href: '/bdr/admin/estimates',
		summary: 'Quote prep, approval, deposits, and contract status',
		contextLabel: 'Sales Ops',
		focusLabel: 'Pipeline lane',
		canvasLabel: 'Quote canvas',
		section: 'revenue'
	},
	{
		slug: 'invoices',
		label: 'Invoices',
		href: '/bdr/admin/invoices',
		summary: 'Billing status, check hold logic, and collections',
		contextLabel: 'Finance Ops',
		focusLabel: 'Collections lane',
		canvasLabel: 'Billing canvas',
		section: 'revenue'
	},
	{
		slug: 'customers',
		label: 'Contacts',
		href: '/bdr/admin/contact',
		summary: 'Accounts, properties, files, and communication history',
		contextLabel: 'Relationship Ops',
		focusLabel: 'Relationship desk',
		canvasLabel: 'Record canvas',
		section: 'customers'
	},
	{
		slug: 'settings',
		label: 'Admin',
		href: '/bdr/admin/settings',
		summary: 'Defaults, website controls, and admin configuration',
		contextLabel: 'Admin Ops',
		focusLabel: 'Config domains',
		canvasLabel: 'Configuration canvas',
		section: 'admin'
	}
];

export const turnkeyOpsAdminNavigation: TurnkeyOpsAdminNavItem[] = [
	{
		slug: 'dashboard',
		label: 'Dashboard',
		href: '/turnkeyops/admin/dashboard',
		summary: 'Portfolio health, launch pressure, and platform-operating signals',
		contextLabel: 'Platform',
		focusLabel: 'Control room',
		canvasLabel: 'Oversight canvas'
	},
	{
		slug: 'tenants',
		label: 'Tenants',
		href: '/turnkeyops/admin/tenants',
		summary: 'Rollout stages, launch blockers, and tenant-specific readiness',
		contextLabel: 'Rollout',
		focusLabel: 'Portfolio board',
		canvasLabel: 'Tenant canvas'
	},
	{
		slug: 'playbooks',
		label: 'Playbooks',
		href: '/turnkeyops/admin/playbooks',
		summary: 'Reusable implementation standards and vertical operating templates',
		contextLabel: 'Implementation',
		focusLabel: 'Standardization',
		canvasLabel: 'Playbook canvas'
	},
	{
		slug: 'health',
		label: 'Platform Health',
		href: '/turnkeyops/admin/health',
		summary: 'Reliability, integrations, data quality, and release confidence',
		contextLabel: 'Reliability',
		focusLabel: 'Health watch',
		canvasLabel: 'Telemetry canvas'
	},
	{
		slug: 'access',
		label: 'Access & Controls',
		href: '/turnkeyops/admin/access',
		summary: 'Roles, environment controls, audit expectations, and launch gates',
		contextLabel: 'Governance',
		focusLabel: 'Control plane',
		canvasLabel: 'Policy canvas'
	}
];

export const bdrAdminRoleMeta: Record<
	BdrAdminRole,
	{
		label: string;
		eyebrow: string;
		description: string;
		focus: string;
		permissions: string[];
	}
> = {
	owner: {
		label: 'Owner',
		eyebrow: 'Full visibility',
		description: 'Sees revenue health, team throughput, decision points, and risk across the full operation.',
		focus: 'Protect margin, approve exceptions, and keep installs moving.',
		permissions: ['Financial oversight', 'Crew allocation', 'Approval authority']
	},
	'office-admin': {
		label: 'Office Admin',
		eyebrow: 'Operations control',
		description: 'Coordinates front-office execution, paperwork, scheduling, and customer follow-through.',
		focus: 'Keep documents, appointments, invoices, and phone callbacks on track.',
		permissions: ['Schedule management', 'Customer updates', 'Invoice processing']
	},
	'estimator-crew-lite': {
		label: 'Estimator Crew Lite',
		eyebrow: 'Field-ready access',
		description: 'Gets a simpler work surface focused on appointments, estimates, and property context.',
		focus: 'Prepare quotes, confirm scope, and hand clean jobs into production.',
		permissions: ['Estimate queue', 'Calendar visibility', 'Customer notes']
	}
};

export const bdrDashboardStats: StatCard[] = [
	{ label: 'Open estimates', value: '18', detail: '6 awaiting signature, 4 need follow-up today' },
	{ label: 'Jobs on calendar', value: '11', detail: '3 installs, 5 inspections, 3 punch-list visits' },
	{ label: 'Receivables due', value: '$48.2k', detail: '2 checks on hold, 5 invoices due this week' },
	{ label: 'Weather watch', value: '2 risks', detail: 'Wednesday rain impacts one install and one inspection window' }
];

export const bdrServiceHighlights = [
	'Residential roof replacement',
	'Commercial roofing and repair',
	'Siding, gutters, and exterior restoration',
	'Insurance and storm-damage coordination'
];

export const turnkeyOpsPillars = [
	{
		title: 'Lead to estimate',
		copy: 'Capture inquiries, qualify demand, and move deals into structured estimates without spreadsheet drift.'
	},
	{
		title: 'Schedule to production',
		copy: 'Run one operating calendar with weather awareness, crew context, and install handoffs.'
	},
	{
		title: 'Contract to cash',
		copy: 'Generate contracts, track signatures, invoice against approved scope, and manage payment holds cleanly.'
	}
];

export const bdrAdminNavSections: Array<{
	key: BdrAdminNavItem['section'];
	label: string;
	description: string;
}> = [
	{ key: 'overview', label: 'Overview', description: 'Leadership and daily office pulse' },
	{ key: 'operations', label: 'Operations', description: 'Scheduling, dispatch, and production timing' },
	{ key: 'revenue', label: 'Revenue', description: 'Estimate approvals, billing, and collections' },
	{ key: 'customers', label: 'Contacts', description: 'People, properties, vendors, and service history' },
	{ key: 'content', label: 'Website', description: 'Public-site copy, structure, preview, and editable sections' },
	{ key: 'admin', label: 'Admin', description: 'System rules, estimate defaults, and operator settings' }
];

const bdrAdminShellStates: Record<string, BdrAdminShellState> = {
	'/bdr/admin/dashboard': {
		title: 'Run the BDR office from one operating surface',
		description:
			'Leadership, front-office ops, and field-facing work now live in the same shell so pipeline, production, paperwork, weather, and cash all stay legible.',
		context: {
			label: 'Office pulse',
			title: 'Today’s operating picture',
			summary: 'This left rail mirrors the kind of contextual desk a real backoffice team needs: what is moving, what is blocked, and what can slip if nobody owns it.',
			metrics: [
				{ label: 'Front office', value: '18 open', detail: 'Estimate follow-ups and proposal revisions still in motion.' },
				{ label: 'Production', value: '11 jobs', detail: 'Install, inspection, and punch-list activity on the shared calendar.' },
				{ label: 'Collections', value: '$48.2k', detail: 'Receivables visible without leaving the owner dashboard.' }
			]
		},
		focus: {
			label: 'Executive watch',
			title: 'Where leadership attention pays off',
			summary: 'The dashboard is opinionated: it should push the owner or office lead toward the handful of decisions that unblock the whole week.',
			notes: [
				{ title: 'Weather risk midweek', detail: 'Wednesday rain threatens one install and one inspection if material drops are not shifted early.' },
				{ title: 'Signature bottleneck', detail: 'A small set of estimate packets are close to schedule lock but still waiting on approvals or deposits.' },
				{ title: 'Cash visibility', detail: 'Invoices, deposit holds, and collection notes stay visible from the same shell instead of a disconnected accounting screen.' }
			]
		},
		canvas: {
			label: 'Control room',
			title: 'Cross-functional operating board',
			summary: 'The main canvas is meant to feel like the office control room, not a marketing dashboard. Every module below ties back to actual backoffice workflow.',
			actions: [
				{ label: 'Review estimate queue', href: '/bdr/admin/estimates' },
				{ label: 'Open calendar', href: '/bdr/admin/calendar', variant: 'secondary' }
			]
		}
	},
	'/bdr/admin/calendar': {
		title: 'Dispatch, appointments, and weather in one schedule surface',
		description:
			'The calendar is the operating spine for BDR: sales handoffs, inspection windows, install commitments, and weather-aware decisions all land here first.',
		context: {
			label: 'Scheduling context',
			title: 'What dispatch has to protect',
			summary: 'A useful contractor calendar is not just dates on a grid. It has to show readiness, weather, ownership, and whether billing or paperwork blocks the next move.',
			metrics: [
				{ label: 'This week', value: '11 events', detail: 'Shared across inspections, installs, drops, and punch-list work.' },
				{ label: 'Weather holds', value: '2 risks', detail: 'Jobs likely to need a go/no-go call before trucks roll.' },
				{ label: 'Schedule owners', value: '3 roles', detail: 'Owner, office admin, and estimator all read from the same operating calendar.' }
			]
		},
		focus: {
			label: 'Dispatch rules',
			title: 'What must be true before a slot locks',
			summary: 'Wellderly’s admin has strong contextual framing around work surfaces. This section does that for the BDR schedule desk.',
			notes: [
				{ title: 'Ready before route', detail: 'Estimate approval, contract posture, and deposit readiness should be visible before committing crews.' },
				{ title: 'Weather first', detail: 'Weather context belongs in the calendar itself, not buried in a separate planning tool.' },
				{ title: 'One source of truth', detail: 'Office and field-lite views use the same route and same schedule state, with role differences handled in context.' }
			]
		},
		canvas: {
			label: 'Dispatch canvas',
			title: 'Office calendar and production handoff',
			summary: 'This view is structured like a scheduling desk: upcoming jobs on the left, risk and rules on the right, and a clear bias toward actionability.',
			actions: [
				{ label: 'Open dashboard', href: '/bdr/admin/dashboard', variant: 'secondary' },
				{ label: 'Check estimate readiness', href: '/bdr/admin/estimates' }
			]
		}
	},
	'/bdr/admin/jobs': {
		title: 'Run production jobs from schedule lock through completion',
		description:
			'Jobs are the live production layer after approval, deposit, and scheduling. This surface keeps crew state, customer context, billing posture, holds, notes, and completion actions in one place.',
		context: {
			label: 'Production context',
			title: 'What the job desk owns',
			summary: 'The office needs a place to run actual work after the invoice clears the release gate. Jobs should not be buried inside invoices or calendar events once crews start moving.',
			metrics: [
				{ label: 'Lifecycle', value: '5 states', detail: 'Scheduled, running, hold, complete, and cancelled states stay explicit.' },
				{ label: 'Billing link', value: 'Invoice-tied', detail: 'Deposit and invoice context remains attached after scheduling.' },
				{ label: 'Crew control', value: 'Live desk', detail: 'Crew assignment, schedule window, notes, and blockers stay editable.' }
			]
		},
		focus: {
			label: 'Production habits',
			title: 'What has to stay visible while work is active',
			summary: 'A job is where promises become field work. The surface should help the office start work, pause it for a real reason, recover the schedule, and close it out cleanly.',
			notes: [
				{ title: 'Run state matters', detail: 'Scheduled work needs a clear transition into in-progress before the office can track what is actually happening.' },
				{ title: 'Holds need reasons', detail: 'Weather, access, materials, paperwork, and customer delays should be captured where the job is managed.' },
				{ title: 'Closeout starts here', detail: 'Completion is the handoff point for final billing, customer follow-up, and internal wrap-up.' }
			]
		},
		canvas: {
			label: 'Job canvas',
			title: 'Production queue, run controls, and closeout posture',
			summary: 'This canvas turns schedule-ready invoices into manageable production records with status actions and a concise activity trail.',
			actions: [
				{ label: 'Open calendar', href: '/bdr/admin/calendar', variant: 'secondary' },
				{ label: 'Open invoice queue', href: '/bdr/admin/invoices' }
			]
		}
	},
	'/bdr/admin/estimates': {
		title: 'Run the estimate-to-contract lane like a real revenue desk',
		description:
			'Estimate work is no longer a placeholder page. It now sits inside a shell that treats quoting, approvals, deposits, and production readiness as one connected lane.',
		context: {
			label: 'Revenue context',
			title: 'How the quote lane behaves',
			summary: 'The office needs to see customer-ready packets and internal costing posture at once, because both determine whether work can actually move forward.',
			metrics: [
				{ label: 'Quote queue', value: '18 active', detail: 'Every quote stays tied to customer record and next action.' },
				{ label: 'Approvals', value: '6 pending', detail: 'Near-term work most likely to get blocked on signature or deposit.' },
				{ label: 'Production ready', value: '4 close', detail: 'Estimate packets almost ready for the schedule board.' }
			]
		},
		focus: {
			label: 'Commercial logic',
			title: 'What the office needs beyond the customer PDF',
			summary: 'The customer packet can stay clean, but the admin layer still needs costing, readiness, and handoff detail to keep jobs profitable.',
			notes: [
				{ title: 'Scope clarity', detail: 'Estimate records should show what is promised publicly and what is needed internally to produce the job.' },
				{ title: 'Approval posture', detail: 'Signature, contract, and deposit are separated so the team sees the actual blocker.' },
				{ title: 'Production handoff', detail: 'The estimate lane should feed schedule lock without a second re-entry step.' }
			]
		},
		canvas: {
			label: 'Quote canvas',
			title: 'Estimate packets, approvals, and readiness',
			summary: 'The main canvas is where front-office selling and production preparation finally meet in one backoffice workflow.',
			actions: [
				{ label: 'View contact records', href: '/bdr/admin/contact', variant: 'secondary' },
				{ label: 'Open invoice queue', href: '/bdr/admin/invoices' }
			]
		}
	},
	'/bdr/admin/contact': {
		title: 'Contact records now behave like an operating system, not a contact list',
		description:
			'Accounts, properties, paperwork, linked estimates, and billing touchpoints stay connected so the office can work from one customer record instead of stitching tools together.',
		context: {
			label: 'Relationship context',
			title: 'What every account record should carry',
			summary: 'A contractor admin needs customer records to anchor properties, documents, status, communication history, and what happens next.',
			metrics: [
				{ label: 'Account base', value: '6 active', detail: 'Live scaffold customers visible in the BDR workspace.' },
				{ label: 'Open quote links', value: '4 linked', detail: 'Customers already tied to active estimate work.' },
				{ label: 'Billing activity', value: '5 accounts', detail: 'Collections or payment posture visible from the same record lane.' }
			]
		},
		focus: {
			label: 'Record design',
			title: 'What the office needs at a glance',
			summary: 'This area intentionally mirrors admin-product thinking: context on the side, record work in the center, next action always visible.',
			notes: [
				{ title: 'Property anchored', detail: 'Every customer entry should resolve to a service address and job context, not just a contact card.' },
				{ title: 'Paperwork visible', detail: 'Files and readiness matter because office friction usually shows up in missing documents.' },
				{ title: 'Action-first records', detail: 'The current next step stays visible so follow-up is operational, not anecdotal.' }
			]
		},
		canvas: {
			label: 'Record canvas',
			title: 'Contacts, properties, files, and linked work',
			summary: 'The customer canvas is the relationship desk for the office team, connecting sales, production, and billing context in one record view.',
			actions: [
				{ label: 'Open estimates', href: '/bdr/admin/estimates', variant: 'secondary' },
				{ label: 'Open calendar', href: '/bdr/admin/calendar' }
			]
		}
	},
	'/bdr/admin/requests': {
		title: 'Treat quote requests like a real intake inbox instead of a contact dump',
		description:
			'Public-site submissions now land in a dedicated admin lane that feels like an operating inbox: triage, ownership, inspection scheduling, estimate preparation, and conversion all stay visible from one place.',
		context: {
			label: 'Inbox context',
			title: 'Why this route exists',
			summary: 'Wellderly-style admin behavior is mostly about context and workflow discipline. This route adapts that into a quote-request inbox for BDR, where every submission keeps an owner, a status, and a next action.',
			metrics: [
				{ label: 'Intake source', value: 'Public + office', detail: 'Website forms and manual office entries live in one inbox.' },
				{ label: 'Processing lane', value: '7 stages', detail: 'Requests can move from new intake through win/close in UI.' },
				{ label: 'Connected flow', value: 'Quote-first', detail: 'This lane bridges marketing response into the estimate desk and schedule board.' }
			]
		},
		focus: {
			label: 'Office habits',
			title: 'What a usable request inbox should protect',
			summary: 'The office needs to know who owns first response, what the customer asked for, how urgent it is, and what must happen next. That information belongs in the queue, not buried after a click.',
			notes: [
				{ title: 'New leads need a clock', detail: 'Fresh requests should read like unread messages with visible urgency.' },
				{ title: 'Status should mean next action', detail: 'Every processing state should imply what the office does next.' },
				{ title: 'Conversion path stays visible', detail: 'Won requests should point naturally into estimates and schedule handoff.' }
			]
		},
		canvas: {
			label: 'Request inbox',
			title: 'Quote messages, triage, and processing controls',
			summary: 'This canvas is intentionally message-like: summary list on the left, selected detail in the center, and practical office actions close at hand.',
			actions: [
				{ label: 'Open estimate queue', href: '/bdr/admin/estimates' },
				{ label: 'View public form', href: '/bdr/public#quote-request', variant: 'secondary' }
			]
		}
	},
	'/bdr/admin/website': {
		title: 'Manage the public-site narrative without leaving the BDR admin shell',
		description:
			'Content management now lives beside operations, revenue, and customer work so the office can update the public site in the same system where jobs actually run.',
		context: {
			label: 'Content context',
			title: 'What this new route owns',
			summary: 'The content desk is structured around the actual sections on the BDR public site: navigation, hero, services, trust, process, support blocks, contact CTA, footer, and the utility strip.',
			metrics: [
				{ label: 'Site sections', value: '8 managed', detail: 'Public homepage sections and utility areas now grouped into editable admin modules.' },
				{ label: 'Content posture', value: 'Scaffolded', detail: 'Ready for backend persistence without redesigning the admin flow.' },
				{ label: 'Owner outcome', value: 'Self-serve', detail: 'Client-facing site changes can now live in the portal instead of code only.' }
			]
		},
		focus: {
			label: 'Content operations',
			title: 'What matters before publishing exists',
			summary: 'This first pass is intentionally practical: strong section boundaries, clear editing affordances, and enough information design to become real CMS tooling later.',
			notes: [
				{ title: 'Section-based ownership', detail: 'Treat navigation, hero, services, trust, and footer as separate admin concerns so changes stay legible.' },
				{ title: 'Real site mapping', detail: 'The public BDR page now reads from the same content scaffold, which keeps the admin route grounded.' },
				{ title: 'Future-friendly UX', detail: 'Draft, publish, approvals, and media management can layer onto this route without throwing away the shell model.' }
			]
		},
		canvas: {
			label: 'Content canvas',
			title: 'Public-site sections, links, and call-to-action management',
			summary: 'This canvas is built as an editable content desk inside the broader BDR operating system, not as a detached marketing settings page.',
			actions: [
				{ label: 'View public site', href: '/bdr/public', variant: 'secondary' },
				{ label: 'Open dashboard', href: '/bdr/admin/dashboard' }
			]
		}
	},
	'/bdr/admin/invoices': {
		title: 'Keep billing, collections, and check holds visible from the same queue',
		description:
			'Collections now feel like part of the actual BDR office workflow, with billing phase, payment method, next step, and hold logic all living inside the admin shell.',
		context: {
			label: 'Finance context',
			title: 'What the billing lane protects',
			summary: 'Receivables are not just accounting output. They determine whether work proceeds, whether closeout happens cleanly, and where the owner needs to intervene.',
			metrics: [
				{ label: 'Receivables queue', value: '$48.2k', detail: 'Current outstanding value visible to owner and office ops.' },
				{ label: 'Check holds', value: '2 at risk', detail: 'Invoices that should stay visibly constrained until resolved.' },
				{ label: 'Billing stages', value: '3 phases', detail: 'Deposit, progress, and final billing all present in the same queue.' }
			]
		},
		focus: {
			label: 'Collections posture',
			title: 'Why this is part of operations, not a side system',
			summary: 'The BDR admin needed a real billing lane. This section frames invoices as a workflow surface tied directly to production and closeout.',
			notes: [
				{ title: 'Payment method matters', detail: 'Card, check, and financing transitions change what the team can safely release or schedule.' },
				{ title: 'Hold logic is first-class', detail: 'Unresolved holds should stay obvious in the primary queue, not hidden in record detail.' },
				{ title: 'Office handoff', detail: 'Billing posture should be understandable to both the owner and office admin without translation.' }
			]
		},
		canvas: {
			label: 'Billing canvas',
			title: 'Receivables, release decisions, and collection actions',
			summary: 'This canvas makes the finance side feel like a real backoffice lane with ownership, constraints, and next steps.',
			actions: [
				{ label: 'Review contacts', href: '/bdr/admin/contact', variant: 'secondary' },
				{ label: 'Go to dashboard', href: '/bdr/admin/dashboard' }
			]
		}
	},
	'/bdr/admin/bob': {
		title: 'Ask Bob questions about the business',
		description:
			'Bob should be the contractor backoffice copilot: a calm place to ask what needs attention, what is blocked, and what the office should do next.',
		context: {
			label: 'AI context',
			title: 'What Bob can help with',
			summary: 'Bob should read across quotes, estimates, schedule, invoices, contacts, and settings so owners can ask plain-language business questions.',
			metrics: [
				{ label: 'Connected lanes', value: '6', detail: 'Quotes, estimates, calendar, invoices, contacts, and public-site context.' },
				{ label: 'Posture', value: 'Assistant', detail: 'Bob suggests next moves without hiding the source workflow.' },
				{ label: 'Access', value: 'Admin only', detail: 'Business-wide AI answers stay behind owner and office-admin access.' }
			]
		},
		focus: {
			label: 'Conversation posture',
			title: 'Business questions should start here',
			summary: 'The dashboard gives quick signal. Ask Bob is where an operator can ask the follow-up question and get a useful explanation.',
			notes: [
				{ title: 'Explain the why', detail: 'Bob should cite the queue or record that drove a recommendation.' },
				{ title: 'Act after answering', detail: 'Answers should point to the correct surface when the next step belongs in quotes, estimates, schedule, or billing.' },
				{ title: 'Keep it operational', detail: 'This is not a general chat toy. It is for the contractor business and its backoffice work.' }
			]
		},
		canvas: {
			label: 'Bob canvas',
			title: 'AI answers, queue summaries, and next-action guidance',
			summary: 'The first version can answer from scaffolded context. The production version should call the Bob service with tenant-scoped data and audit-friendly source references.',
			actions: [
				{ label: 'Open quote queue', href: '/bdr/admin/requests', variant: 'secondary' },
				{ label: 'Open invoices', href: '/bdr/admin/invoices' }
			]
		}
	},
	'/bdr/admin/settings': {
		title: 'Operate the admin system through explicit rules instead of hidden constants',
		description:
			'Settings is a real configuration surface for estimate defaults, calculation handling, and business-rule variables so operators can understand how the office system behaves before backend persistence exists.',
		context: {
			label: 'Configuration context',
			title: 'What belongs here',
			summary: 'These settings govern quoting, approval, billing, and release behavior. The surface is meant to feel like an admin console, not a placeholder feature inventory.',
			metrics: [
				{ label: 'Domains', value: '4 active', detail: 'Estimate logic, pricing controls, payment holds, and workflow guardrails.' },
				{ label: 'Editable rules', value: '14 visible', detail: 'Scaffolded defaults exposed in operator language.' },
				{ label: 'Persistence', value: 'Scaffolded', detail: 'Ready to wire into backend settings storage later.' }
			]
		},
		focus: {
			label: 'Operator outcomes',
			title: 'Why this route matters',
			summary: 'The office should not have to guess how the system calculates deposits, handles check holds, or treats estimate readiness. The rules should be inspectable and editable.',
			notes: [
				{ title: 'Visible commercial rules', detail: 'Deposit rates, markup posture, and approval thresholds should be easy to audit.' },
				{ title: 'Operational controls', detail: 'Business timing rules like check holds and schedule release windows belong in admin settings.' },
				{ title: 'Future-safe structure', detail: 'The grouping here can become persisted settings without changing the operator UX shape.' }
			]
		},
		canvas: {
			label: 'Configuration canvas',
			title: 'Estimate logic, business rules, and office defaults',
			summary: 'This canvas should read like an actual admin rule book with editable values, explanatory context, and visible operational impact.',
			actions: [
				{ label: 'Open invoice queue', href: '/bdr/admin/invoices' },
				{ label: 'Open estimate lane', href: '/bdr/admin/estimates', variant: 'secondary' }
			]
		}
	}
};

const bdrAdminPathAliases: Record<string, string> = {
	'/bdr/admin': '/bdr/admin/bob',
	'/bdr/admin/customers': '/bdr/admin/contact',
	'/bdr/admin/content': '/bdr/admin/website',
	'/bdr/admin/website': '/bdr/admin/settings'
};

export const normalizeBdrAdminPath = (pathname: string) => bdrAdminPathAliases[pathname] ?? pathname;

export type BdrAdminViewRole = Extract<BdrAdminRole, 'owner' | 'office-admin'>;

export const normalizeBdrAdminRole = (value: string | null | undefined): BdrAdminRole => {
	if (!value) return 'owner';
	return bdrAdminRoles.includes(value as BdrAdminRole) ? (value as BdrAdminRole) : 'owner';
};

export const isBdrAdminViewRole = (value: string | null | undefined): value is BdrAdminViewRole =>
	value === 'owner' || value === 'office-admin';

export const normalizeBdrAdminViewRole = (value: string | null | undefined): BdrAdminViewRole =>
	isBdrAdminViewRole(value) ? value : 'owner';

export const hasBdrAdminViewAccess = (value: string | null | undefined) => isBdrAdminViewRole(value);

export const getBdrAdminNav = (pathname: string): BdrAdminNavItem => {
	const normalizedPath = normalizeBdrAdminPath(pathname);
	return (
		bdrAdminNavigation.find(
			(item) => normalizedPath === item.href || normalizedPath.startsWith(`${item.href}/`)
		) ?? bdrAdminNavigation[0]
	);
};

export const getBdrAdminShellState = (pathname: string): BdrAdminShellState => {
	const normalizedPath = normalizeBdrAdminPath(pathname);
	return bdrAdminShellStates[normalizedPath] ?? bdrAdminShellStates['/bdr/admin/bob'];
};

export const getTurnkeyOpsAdminNav = (pathname: string) => {
	const active =
		turnkeyOpsAdminNavigation.find((item) => pathname === item.href || pathname.startsWith(`${item.href}/`)) ??
		turnkeyOpsAdminNavigation[0];

	return {
		...active,
		allNav: turnkeyOpsAdminNavigation
	} satisfies TurnkeyOpsAdminNavItem;
};
