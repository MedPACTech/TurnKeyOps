export type ContentLink = {
	label: string;
	href: string;
};

export type ContentItem = {
	title: string;
	copy: string;
};

export type BdrSiteContent = {
	navigation: {
		announcement: string;
		brandName: string;
		links: ContentLink[];
	};
	hero: {
		eyebrow: string;
		headline: string;
		body: string;
		primaryCtaLabel: string;
		primaryCtaHref: string;
		secondaryCtaLabel: string;
		secondaryCtaHref: string;
		proofEyebrow: string;
	};
	services: {
		eyebrow: string;
		title: string;
		copy: string;
		items: string[];
	};
	trust: {
		eyebrow: string;
		title: string;
		copy: string;
		points: string[];
	};
	process: {
		eyebrow: string;
		steps: Array<{ step: string; title: string; copy: string }>;
	};
	supportingSections: Array<{
		eyebrow: string;
		title: string;
		copy: string;
		items: ContentItem[];
	}>;
	contact: {
		eyebrow: string;
		title: string;
		body: string;
		primaryCtaLabel: string;
		primaryCtaHref: string;
		secondaryCtaLabel: string;
		secondaryCtaHref: string;
	};
	footer: {
		eyebrow: string;
		brandName: string;
		body: string;
		linksEyebrow: string;
		links: ContentLink[];
	};
	postFooter: {
		utilityLinks: ContentLink[];
		copyright: string;
	};
};

export const bdrSiteContent: BdrSiteContent = {
	navigation: {
		announcement: 'Navigation',
		brandName: 'BDR Construction',
		links: [
			{ href: '#hero', label: 'Home' },
			{ href: '#services', label: 'Services' },
			{ href: '#trust', label: 'Why BDR' },
			{ href: '#process', label: 'Process' },
			{ href: '#quote-request', label: 'Request a Quote' },
			{ href: '#contact', label: 'Contact' }
		]
	},
	hero: {
		eyebrow: 'Hero',
		headline:
			'Roofing and exterior work with a clear path from inspection to quote, schedule, and completion.',
		body:
			'BDR Construction helps homeowners, property managers, and commercial clients move from first contact to scoped work with responsive communication and a dependable project process.',
		primaryCtaLabel: 'Request a quote',
		primaryCtaHref: '#quote-request',
		secondaryCtaLabel: 'Explore services',
		secondaryCtaHref: '#services',
		proofEyebrow: 'Why customers call BDR'
	},
	services: {
		eyebrow: 'Services',
		title: 'What BDR handles',
		copy:
			'BDR handles the exterior work customers need most, from urgent leak response to full replacements and restoration projects.',
		items: [
			'Residential roof replacement',
			'Commercial roofing and repair',
			'Siding, gutters, and exterior restoration',
			'Insurance and storm-damage coordination'
		]
	},
	trust: {
		eyebrow: 'Trust signals',
		title: 'Why customers trust the process',
		copy:
			'Customers need a contractor they can trust with communication, timing, and follow-through from the first inspection through final billing.',
		points: [
			'Licensed and insured residential and commercial crews',
			'Storm-damage and insurance coordination support',
			'Simple estimate packet backed by a structured office workflow',
			'Clear handoff from inspection to install to billing'
		]
	},
	process: {
		eyebrow: 'Process',
		steps: [
			{
				step: '1',
				title: 'Request an estimate',
				copy: 'Tell BDR about the property, timing, and damage. Urgent storm and leak calls move to the front of the office queue.'
			},
			{
				step: '2',
				title: 'Inspection and scope',
				copy: 'An estimator confirms measurements, photographs conditions, and builds a customer-friendly scope backed by internal costing.'
			},
			{
				step: '3',
				title: 'Approve and schedule',
				copy: 'Customers approve the contract, sign electronically, handle the deposit if required, and lock the production window.'
			}
		]
	},
	supportingSections: [
		{
			eyebrow: 'Project lanes',
			title: 'Featured work types',
			copy: 'BDR supports the job types that typically drive the highest urgency, coordination, and homeowner questions.',
			items: [
				{
					title: 'Residential roof replacement',
					copy: 'Architectural shingles, flashing, venting, cleanup, and homeowner communication in one packet.'
				},
				{
					title: 'Commercial repair and overlay',
					copy: 'Flat-roof scopes with staging, tenant-safety notes, and night-work coordination.'
				},
				{
					title: 'Storm restoration',
					copy: 'Insurance photo sets, supplement support, and material coordination without spreadsheet drift.'
				}
			]
		},
		{
			eyebrow: 'Behind the scenes',
			title: 'Connected office workflow',
			copy: 'Behind every project is an office workflow built to keep communication, approvals, and billing moving cleanly.',
			items: [
				{
					title: 'Estimate packets',
					copy: 'Estimate packets can stay customer-friendly while internal costing remains in admin.'
				},
				{
					title: 'Contract state',
					copy: 'Contract and signature state now show up as part of the same estimate lane.'
				},
				{
					title: 'Invoices',
					copy: 'Invoices support both deposit billing and final billing with visible check-hold posture.'
				}
			]
		}
	],
	contact: {
		eyebrow: 'Contact',
		title: 'Ready to talk about your project?',
		body:
			'If you need roofing, repairs, storm restoration, or exterior work, BDR Construction can help you move from estimate to signed agreement with a cleaner process.',
		primaryCtaLabel: 'Request a quote',
		primaryCtaHref: '#quote-request',
		secondaryCtaLabel: 'Call BDR',
		secondaryCtaHref: 'tel:2202177026'
	},
	footer: {
		eyebrow: 'Footer',
		brandName: 'BDR Construction',
		body:
			'Residential and commercial roofing, exterior work, and storm restoration backed by a dependable process from intake to closeout.',
		linksEyebrow: 'Footer links',
		links: [
			{ href: '#hero', label: 'Home' },
			{ href: '#services', label: 'Services' },
			{ href: '#process', label: 'Process' },
			{ href: '#quote-request', label: 'Request a Quote' },
			{ href: '#contact', label: 'Contact' }
		]
	},
	postFooter: {
		utilityLinks: [
			{ href: '#top', label: 'Top' },
			{ href: '#hero', label: 'Home' },
			{ href: '#services', label: 'Services' },
			{ href: '#process', label: 'Process' },
			{ href: '#quote-request', label: 'Request a Quote' },
			{ href: '#contact', label: 'Contact' }
		],
		copyright: '© {{year}} BDR Construction. All rights reserved.'
	}
};

export const resolveBdrCopyright = (year: number) =>
	bdrSiteContent.postFooter.copyright.replace('{{year}}', String(year));
