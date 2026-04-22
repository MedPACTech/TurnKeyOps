import type { Customer, Estimate, Invoice, Lead, MvpScaffoldSnapshot } from '$lib/types/mvp';

type CustomerSeed = {
	property: string;
	segment: string;
	lastTouch: string;
	nextStep: string;
	files: string[];
	risk: string;
};

type EstimateSeed = {
	scopeSummary: string;
	customerFacingSections: string[];
	internalCosting: string[];
	contractStatus: string;
	signatureStatus: string;
	depositStatus: string;
	productionReadiness: string;
	nextStep: string;
};

type InvoiceSeed = {
	billingPhase: string;
	paymentMethod: string;
	checkHold: string;
	owner: string;
	nextStep: string;
};

type CalendarSeed = {
	day: string;
	time: string;
	type: string;
	owner: string;
	weather: string;
	status: string;
	nextStep: string;
};

const customerSeeds: CustomerSeed[] = [
	{
		property: '131 Ridgeway Lane, Charlotte, NC',
		segment: 'Residential replacement',
		lastTouch: 'Called after ladder assist and attic photo review',
		nextStep: 'Collect signed contract and confirm dumpster drop',
		files: ['Inspection photos', 'Insurance notes', 'Signed estimate packet'],
		risk: 'Waiting on homeowner signature'
	},
	{
		property: '870 Pine Grove Blvd, Gastonia, NC',
		segment: 'Commercial flat roof',
		lastTouch: 'Met with site manager to align membrane scope and tenant access',
		nextStep: 'Finalize deposit timing and after-hours staging',
		files: ['Scope revision', 'Access plan', 'Vendor quote backup'],
		risk: 'Tenant access window is narrow'
	},
	{
		property: 'Harborside Buildings A-D, Cornelius, NC',
		segment: 'HOA phased install',
		lastTouch: 'Board email sent with weather contingency and crew sequence',
		nextStep: 'Release phase-two materials after treasurer confirmation',
		files: ['Board approvals', 'Material schedule', 'Closeout checklist'],
		risk: 'Weather watch for shared common areas'
	},
	{
		property: '18 Mason Court, Huntersville, NC',
		segment: 'Residential insurance supplement',
		lastTouch: 'Left voicemail after final walkthrough and supplement revision',
		nextStep: 'Resolve supplement delta before final collection push',
		files: ['Supplement worksheet', 'Final photo set', 'Invoice backup'],
		risk: 'Final payment timing is slipping'
	}
];

const estimateSeeds: EstimateSeed[] = [
	{
		scopeSummary: 'Architectural shingle replacement with venting upgrades, chimney flashing, and cleanup.',
		customerFacingSections: ['Project overview', 'Scope and materials', 'Warranty', 'Customer approval'],
		internalCosting: ['Labor budget 34%', 'Material lock through Apr 4', 'Dump fee included', 'Margin target 42%'],
		contractStatus: 'Contract packet ready',
		signatureStatus: 'Sent for e-signature',
		depositStatus: 'Deposit request queued',
		productionReadiness: 'Awaiting signature before production hold releases',
		nextStep: 'Office follow-up by 3 PM'
	},
	{
		scopeSummary: 'TPO repair and overlay package with tenant safety barricades and night staging.',
		customerFacingSections: ['Safety plan', 'Phased work summary', 'Allowances', 'Approval and deposit'],
		internalCosting: ['Labor budget 29%', 'Lift rental pending', 'Vendor membrane quote attached', 'Margin target 38%'],
		contractStatus: 'Approved pending deposit',
		signatureStatus: 'Signed by property manager',
		depositStatus: 'Deposit expected by ACH',
		productionReadiness: 'Ready to assign once payment clears',
		nextStep: 'Hold install window through Friday'
	},
	{
		scopeSummary: 'HOA multi-building shingle and gutter phase with building-by-building sequencing.',
		customerFacingSections: ['Phase map', 'Resident notice plan', 'Allowances', 'Board authorization'],
		internalCosting: ['Labor budget 31%', 'Gutter allowance open', 'Crew split across phases', 'Margin target 36%'],
		contractStatus: 'Executed contract',
		signatureStatus: 'Board approval complete',
		depositStatus: 'Deposit received',
		productionReadiness: 'Materials and sequencing ready',
		nextStep: 'Confirm weather call on Wednesday'
	},
	{
		scopeSummary: 'Storm restoration revision with decking allowance and supplement request to carrier.',
		customerFacingSections: ['Damage findings', 'Upgrade options', 'Allowance notes', 'Revision approval'],
		internalCosting: ['Labor budget 33%', 'Supplement gap unresolved', 'Material buffer 6%', 'Margin target 35%'],
		contractStatus: 'Revision in review',
		signatureStatus: 'Pending revised acceptance',
		depositStatus: 'Deposit on hold until supplement closes',
		productionReadiness: 'Not ready for schedule lock',
		nextStep: 'Estimator to revise line items'
	}
];

const invoiceSeeds: InvoiceSeed[] = [
	{
		billingPhase: 'Deposit invoice',
		paymentMethod: 'ACH or card',
		checkHold: 'No hold',
		owner: 'Office admin',
		nextStep: 'Collect payment before crew release'
	},
	{
		billingPhase: 'Progress draw',
		paymentMethod: 'Check received',
		checkHold: 'Funds held until bank clears',
		owner: 'Owner review',
		nextStep: 'Do not release supplier payment until hold clears'
	},
	{
		billingPhase: 'Final invoice',
		paymentMethod: 'Customer mailing check',
		checkHold: 'Watchlist if check arrives after due date',
		owner: 'Collections',
		nextStep: 'Close out after punch-list photos land'
	},
	{
		billingPhase: 'Post-approval billing',
		paymentMethod: 'Invoice not yet released',
		checkHold: 'Blocked until contract path resolves',
		owner: 'Sales ops',
		nextStep: 'Convert approved estimate into deposit request'
	}
];

const calendarSeeds: CalendarSeed[] = [
	{
		day: 'Mon',
		time: '8:00 AM',
		type: 'Install',
		owner: 'Crew A',
		weather: 'Clear install window through 3 PM',
		status: 'Locked',
		nextStep: 'Dumpster drop before arrival'
	},
	{
		day: 'Tue',
		time: '10:30 AM',
		type: 'Inspection',
		owner: 'Estimator',
		weather: 'Dry, light wind',
		status: 'Ready',
		nextStep: 'Capture membrane photo set'
	},
	{
		day: 'Wed',
		time: '7:30 AM',
		type: 'Material drop',
		owner: 'Ops',
		weather: 'Rain risk after noon',
		status: 'Contingency watch',
		nextStep: 'Call by 11 AM if radar worsens'
	},
	{
		day: 'Thu',
		time: '9:00 AM',
		type: 'Punch list',
		owner: 'Crew B',
		weather: 'Mild with low wind',
		status: 'Pending closeout',
		nextStep: 'Upload final photos for billing'
	}
];

const leadActions = [
	'Confirm inspection route and pull satellite measurements.',
	'Prepare internal costing draft before next call.',
	'Schedule follow-up call with decision maker.',
	'Coordinate insurance supplement notes for review.'
];

const byIndex = <T>(items: T[], index: number) => items[index % items.length];

export const getScaffoldBanner = (source: 'api' | 'fallback') =>
	source === 'api'
		? 'Connected to the scaffold API endpoint for this session.'
		: 'Using local scaffold fallback so the MVP stays demoable while the API repo is in motion.';

export const decorateLead = (lead: Lead, index: number) => ({
	...lead,
	nextStep: byIndex(leadActions, index),
	temperature: index % 2 === 0 ? 'Warm' : 'Hot'
});

export const buildCustomerViews = (
	customers: Customer[],
	estimates: Estimate[],
	invoices: Invoice[]
) =>
	customers.map((customer, index) => {
		const seed = byIndex(customerSeeds, index);
		const relatedEstimates = estimates.filter((estimate) => estimate.customerId === customer.id);
		const relatedInvoices = invoices.filter((invoice) => invoice.customerId === customer.id);

		return {
			...customer,
			...seed,
			openEstimateCount: relatedEstimates.length,
			openInvoiceCount: relatedInvoices.length
		};
	});

export const buildEstimateViews = (estimates: Estimate[], customers: Customer[]) =>
	estimates.map((estimate, index) => ({
		...estimate,
		...byIndex(estimateSeeds, index),
		customer: customers.find((customer) => customer.id === estimate.customerId)
	}));

export const buildInvoiceViews = (invoices: Invoice[], customers: Customer[]) =>
	invoices.map((invoice, index) => ({
		...invoice,
		...byIndex(invoiceSeeds, index),
		customer: customers.find((customer) => customer.id === invoice.customerId)
	}));

export const buildCalendarViews = (snapshot: MvpScaffoldSnapshot) =>
	snapshot.customers.slice(0, 4).map((customer, index) => {
		const seed = byIndex(calendarSeeds, index);
		const estimate = snapshot.estimates.find((item) => item.customerId === customer.id);
		const invoice = snapshot.invoices.find((item) => item.customerId === customer.id);

		return {
			id: `${customer.id}-${seed.day}`,
			job: customer.displayName,
			day: seed.day,
			time: seed.time,
			type: seed.type,
			owner: seed.owner,
			weather: seed.weather,
			status: seed.status,
			nextStep: seed.nextStep,
			estimateStatus: estimate?.status ?? 'No estimate linked',
			billingStatus: invoice?.status ?? 'No invoice yet'
		};
	});

export const buildDashboardAlerts = (snapshot: MvpScaffoldSnapshot) => {
	const estimates = buildEstimateViews(snapshot.estimates, snapshot.customers);
	const invoices = buildInvoiceViews(snapshot.invoices, snapshot.customers);
	const leads = snapshot.leads.map(decorateLead);

	return [
		`${leads[0]?.companyName ?? 'Lead queue'} is the highest-value lead and needs ${leads[0]?.nextStep.toLowerCase() ?? 'follow-up'}`,
		`${estimates.find((estimate) => estimate.signatureStatus.includes('Pending'))?.estimateNumber ?? estimates[0]?.estimateNumber ?? 'Estimate queue'} is blocked on signature or revision`,
		`${invoices.find((invoice) => invoice.checkHold !== 'No hold')?.invoiceNumber ?? invoices[0]?.invoiceNumber ?? 'Billing queue'} requires check-hold or collections attention`
	];
};

export const buildPublicProof = (snapshot: MvpScaffoldSnapshot) => [
	`${snapshot.summary.customerCount} active customer records already move through the BDR MVP surface.`,
	`${snapshot.summary.estimateCount} live estimates support approval, deposit, and schedule handoff.`,
	`${snapshot.summary.invoiceCount} invoices demonstrate deposit, final billing, and payment-hold handling.`
];
