import type { MvpScaffoldSnapshot } from '$lib/types/mvp';

export const fallbackMvpSnapshot: MvpScaffoldSnapshot = {
	generatedAtUtc: '2026-03-29T12:00:00.000Z',
	summary: {
		customerCount: 4,
		estimateCount: 4,
		invoiceCount: 4,
		leadCount: 4,
		estimateValue: 121600,
		receivablesValue: 29450,
		pipelineValue: 208000
	},
	customers: [
		{
			id: 'cust-ridgeway',
			displayName: 'Ridgeway Residence',
			primaryContactName: 'Natalie Ridgeway',
			primaryContactEmail: 'natalie@ridgewayhome.com',
			primaryContactPhone: '(704) 555-0148',
			status: 'Active',
			lifecycleStage: 'Customer'
		},
		{
			id: 'cust-pine-grove',
			displayName: 'Pine Grove Retail Center',
			primaryContactName: 'Andre Lewis',
			primaryContactEmail: 'andre@pinegroveretail.com',
			primaryContactPhone: '(704) 555-0193',
			status: 'Pending approval',
			lifecycleStage: 'Opportunity'
		},
		{
			id: 'cust-harborside',
			displayName: 'Harborside HOA',
			primaryContactName: 'Lauren Bishop',
			primaryContactEmail: 'lauren@harborsidehoa.org',
			primaryContactPhone: '(704) 555-0109',
			status: 'In production',
			lifecycleStage: 'Customer'
		},
		{
			id: 'cust-mason',
			displayName: 'Mason Residence',
			primaryContactName: 'Jeff Mason',
			primaryContactEmail: 'jeff@themasonhouse.com',
			primaryContactPhone: '(704) 555-0126',
			status: 'Collections',
			lifecycleStage: 'Customer'
		}
	],
	estimates: [
		{
			id: 'est-24031',
			customerId: 'cust-ridgeway',
			jobSiteId: 'site-ridgeway',
			estimateNumber: 'EST-24031',
			status: 'Ready for signature',
			totalAmount: 28400,
			validUntilUtc: '2026-04-10T00:00:00.000Z'
		},
		{
			id: 'est-24032',
			customerId: 'cust-pine-grove',
			jobSiteId: 'site-pine-grove',
			estimateNumber: 'EST-24032',
			status: 'Awaiting deposit',
			totalAmount: 46200,
			validUntilUtc: '2026-04-15T00:00:00.000Z'
		},
		{
			id: 'est-24033',
			customerId: 'cust-harborside',
			jobSiteId: 'site-harborside',
			estimateNumber: 'EST-24033',
			status: 'Approved',
			totalAmount: 31900,
			validUntilUtc: '2026-04-04T00:00:00.000Z'
		},
		{
			id: 'est-24034',
			customerId: 'cust-mason',
			jobSiteId: 'site-mason',
			estimateNumber: 'EST-24034',
			status: 'Revision requested',
			totalAmount: 15100,
			validUntilUtc: '2026-04-18T00:00:00.000Z'
		}
	],
	invoices: [
		{
			id: 'inv-1084',
			customerId: 'cust-ridgeway',
			jobSiteId: 'site-ridgeway',
			invoiceNumber: 'INV-1084',
			status: 'Deposit due',
			balanceDue: 8520,
			dueDateUtc: '2026-04-02T00:00:00.000Z'
		},
		{
			id: 'inv-1085',
			customerId: 'cust-harborside',
			jobSiteId: 'site-harborside',
			invoiceNumber: 'INV-1085',
			status: 'Check hold',
			balanceDue: 12480,
			dueDateUtc: '2026-03-31T00:00:00.000Z'
		},
		{
			id: 'inv-1086',
			customerId: 'cust-mason',
			jobSiteId: 'site-mason',
			invoiceNumber: 'INV-1086',
			status: 'Final due',
			balanceDue: 5930,
			dueDateUtc: '2026-04-05T00:00:00.000Z'
		},
		{
			id: 'inv-1087',
			customerId: 'cust-pine-grove',
			jobSiteId: 'site-pine-grove',
			invoiceNumber: 'INV-1087',
			status: 'Ready after approval',
			balanceDue: 2520,
			dueDateUtc: '2026-04-12T00:00:00.000Z'
		}
	],
	leads: [
		{
			id: 'lead-brookfield',
			companyName: 'Brookfield Church',
			contactName: 'Pastor Mike Nelson',
			contactEmail: 'office@brookfieldchurch.org',
			contactPhone: '(704) 555-0177',
			pipelineStage: 'Inspection scheduled',
			estimatedValue: 54000,
			source: 'Referral'
		},
		{
			id: 'lead-crestline',
			companyName: 'Crestline Storage',
			contactName: 'Renee Hall',
			contactEmail: 'renee@crestlinestorage.com',
			contactPhone: '(704) 555-0154',
			pipelineStage: 'Scope review',
			estimatedValue: 68000,
			source: 'Google Local'
		},
		{
			id: 'lead-woodland',
			companyName: 'Woodland Estates',
			contactName: 'Terry Bryant',
			contactEmail: 'terry@woodlandestates.com',
			contactPhone: '(704) 555-0162',
			pipelineStage: 'Needs follow-up',
			estimatedValue: 37000,
			source: 'Yard sign'
		},
		{
			id: 'lead-wellington',
			companyName: 'Wellington Family Home',
			contactName: 'Sofia Wellington',
			contactEmail: 'sofia@wellingtonfamily.net',
			contactPhone: '(704) 555-0133',
			pipelineStage: 'Insurance review',
			estimatedValue: 49000,
			source: 'Storm campaign'
		}
	]
};
