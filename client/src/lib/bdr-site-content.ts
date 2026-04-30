export type ContentLink = {
	label: string;
	href: string;
};

export type ContentItem = {
	title: string;
	copy: string;
};

export type BdrAsset = {
	key: string;
	name: string;
	type: 'logo' | 'icon' | 'hero-image' | 'background-image' | 'texture' | 'project-photo';
	file: string;
	altText: string;
	contractorCategory: string;
	tags: string[];
	sortOrder: number;
};

export type BdrServiceCategory = {
	name: string;
	slug: string;
	description: string;
	iconAssetKey: string;
	imageAssetKey?: string;
	contractorType: string;
	featured: boolean;
	sortOrder: number;
};

export type BdrSiteContent = {
	assetLibrary: BdrAsset[];
	serviceCategories: BdrServiceCategory[];
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
	assetLibrary: [
		{
			key: 'bdr-crest-logo',
			name: 'Company crest logo',
			type: 'logo',
			file: '/clientFiles/BDRLogo.jpeg',
			altText: 'BDR Construction crest logo',
			contractorCategory: 'concrete',
			tags: ['brand', 'logo', 'header', 'footer'],
			sortOrder: 1
		},
		{
			key: 'bdr-favicon-mark',
			name: 'Favicon mark',
			type: 'icon',
			file: '/clientFiles/logo.png',
			altText: 'BDR Construction favicon mark',
			contractorCategory: 'concrete',
			tags: ['brand', 'favicon', 'logo-mark'],
			sortOrder: 2
		},
		{
			key: 'hero-driveway-scene',
			name: 'Modern home driveway hero',
			type: 'hero-image',
			file: '/clientFiles/image0.jpeg',
			altText: 'Modern home with a finished concrete driveway',
			contractorCategory: 'concrete',
			tags: ['hero', 'driveway', 'mockup'],
			sortOrder: 3
		},
		{
			key: 'cta-finishing-scene',
			name: 'Concrete finishing CTA banner',
			type: 'background-image',
			file: '/clientFiles/image1.jpeg',
			altText: 'Concrete finishing equipment at work on site',
			contractorCategory: 'concrete',
			tags: ['cta', 'banner', 'equipment', 'mockup'],
			sortOrder: 4
		},
		{
			key: 'dark-grid-texture',
			name: 'Dark grid texture',
			type: 'texture',
			file: '/clientFiles/assets/dark-grid-texture.svg',
			altText: 'Subtle dark grid texture',
			contractorCategory: 'shared',
			tags: ['texture', 'dark-surface', 'background'],
			sortOrder: 5
		},
		{
			key: 'grain-overlay-texture',
			name: 'Concrete grain overlay',
			type: 'texture',
			file: '/clientFiles/assets/grain-overlay-texture.svg',
			altText: 'Subtle concrete grain overlay texture',
			contractorCategory: 'shared',
			tags: ['texture', 'grain', 'overlay'],
			sortOrder: 6
		},
		{
			key: 'service-driveways-icon',
			name: 'Driveways service icon',
			type: 'icon',
			file: '/clientFiles/assets/driveways-icon.svg',
			altText: 'Line icon for driveway work',
			contractorCategory: 'concrete',
			tags: ['service', 'icon', 'driveways'],
			sortOrder: 7
		},
		{
			key: 'service-patios-icon',
			name: 'Patios service icon',
			type: 'icon',
			file: '/clientFiles/assets/patios-icon.svg',
			altText: 'Line icon for patio work',
			contractorCategory: 'concrete',
			tags: ['service', 'icon', 'patios'],
			sortOrder: 8
		},
		{
			key: 'service-sidewalks-icon',
			name: 'Sidewalks service icon',
			type: 'icon',
			file: '/clientFiles/assets/sidewalks-icon.svg',
			altText: 'Line icon for sidewalk work',
			contractorCategory: 'concrete',
			tags: ['service', 'icon', 'sidewalks'],
			sortOrder: 9
		},
		{
			key: 'service-steps-icon',
			name: 'Steps and stoops service icon',
			type: 'icon',
			file: '/clientFiles/assets/steps-stoops-icon.svg',
			altText: 'Line icon for steps and stoops work',
			contractorCategory: 'concrete',
			tags: ['service', 'icon', 'steps', 'stoops'],
			sortOrder: 10
		},
		{
			key: 'service-slabs-icon',
			name: 'Concrete slabs service icon',
			type: 'icon',
			file: '/clientFiles/assets/slabs-icon.svg',
			altText: 'Line icon for slab work',
			contractorCategory: 'concrete',
			tags: ['service', 'icon', 'slabs'],
			sortOrder: 11
		},
		{
			key: 'service-decorative-icon',
			name: 'Decorative concrete service icon',
			type: 'icon',
			file: '/clientFiles/assets/decorative-concrete-icon.svg',
			altText: 'Line icon for decorative concrete work',
			contractorCategory: 'concrete',
			tags: ['service', 'icon', 'decorative'],
			sortOrder: 12
		},
		{
			key: 'footer-facebook-icon',
			name: 'Facebook social icon',
			type: 'icon',
			file: '/clientFiles/assets/facebook-icon.svg',
			altText: 'Facebook icon',
			contractorCategory: 'shared',
			tags: ['social', 'facebook', 'footer'],
			sortOrder: 13
		},
		{
			key: 'footer-instagram-icon',
			name: 'Instagram social icon',
			type: 'icon',
			file: '/clientFiles/assets/instagram-icon.svg',
			altText: 'Instagram icon',
			contractorCategory: 'shared',
			tags: ['social', 'instagram', 'footer'],
			sortOrder: 14
		}
	],
	serviceCategories: [
		{
			name: 'Driveways',
			slug: 'driveways',
			description: 'New pours, replacements, widening, and driveway apron tie-ins.',
			iconAssetKey: 'service-driveways-icon',
			imageAssetKey: 'hero-driveway-scene',
			contractorType: 'concrete',
			featured: true,
			sortOrder: 1
		},
		{
			name: 'Patios',
			slug: 'patios',
			description: 'Backyard entertainment slabs, extensions, and clean broom-finish installs.',
			iconAssetKey: 'service-patios-icon',
			contractorType: 'concrete',
			featured: true,
			sortOrder: 2
		},
		{
			name: 'Sidewalks',
			slug: 'sidewalks',
			description: 'Walkways, repair sections, and city-style pedestrian flatwork.',
			iconAssetKey: 'service-sidewalks-icon',
			contractorType: 'concrete',
			featured: true,
			sortOrder: 3
		},
		{
			name: 'Steps & Stoops',
			slug: 'steps-stoops',
			description: 'Safe access upgrades with formed steps, stoops, and landing work.',
			iconAssetKey: 'service-steps-icon',
			contractorType: 'concrete',
			featured: true,
			sortOrder: 4
		},
		{
			name: 'Concrete Slabs',
			slug: 'concrete-slabs',
			description: 'Pads, sheds, garages, and work areas that need solid flatwork.',
			iconAssetKey: 'service-slabs-icon',
			contractorType: 'concrete',
			featured: true,
			sortOrder: 5
		},
		{
			name: 'Decorative Concrete',
			slug: 'decorative-concrete',
			description: 'Stamped, colored, and upgraded finishes for higher-visual-impact surfaces.',
			iconAssetKey: 'service-decorative-icon',
			contractorType: 'concrete',
			featured: true,
			sortOrder: 6
		}
	],
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

export const getBdrAsset = (content: BdrSiteContent, key: string) =>
	content.assetLibrary.find((asset) => asset.key === key) ?? null;

export const getBdrServiceCategories = (content: BdrSiteContent) =>
	[...content.serviceCategories].sort((left, right) => left.sortOrder - right.sortOrder);
