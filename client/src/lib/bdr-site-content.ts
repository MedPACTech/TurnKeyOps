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

export type BdrPresetService = {
	name: string;
	slug: string;
	description: string;
	iconAssetKey: string;
	imageAssetKey?: string;
};

export type BdrContractorPreset = {
	id: string;
	label: string;
	contractorType: string;
	defaultHeroHeadline: string;
	defaultServices: BdrPresetService[];
	defaultIconAssetKeys: string[];
};

export type BdrThemeSettings = {
	mode: 'Light' | 'Dark' | 'System';
	preset: 'Clean' | 'Industrial' | 'Premium' | 'Minimal' | 'Bold';
	colors: {
		primary: string;
		secondary: string;
		accent: string;
		background: string;
		surface: string;
		text: string;
		border: string;
	};
	typography: {
		headingFont: string;
		bodyFont: string;
	};
	sizing: {
		buttonRadius: string;
		cardRadius: string;
		logoSize: string;
	};
	iconStyle: string;
	brandAssets: {
		logoAssetKey: string;
		faviconAssetKey: string;
	};
};

export type BdrSiteContent = {
	assetLibrary: BdrAsset[];
	serviceCategories: BdrServiceCategory[];
	contractorPresets: BdrContractorPreset[];
	activeContractorPresetId: string;
	themeSettings: BdrThemeSettings;
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
		},
		{
			key: 'preset-roofing-icon',
			name: 'Roofing preset icon',
			type: 'icon',
			file: '/clientFiles/assets/roofing-icon.svg',
			altText: 'Line icon for roofing work',
			contractorCategory: 'roofing',
			tags: ['preset', 'roofing', 'icon'],
			sortOrder: 15
		},
		{
			key: 'preset-masonry-icon',
			name: 'Masonry preset icon',
			type: 'icon',
			file: '/clientFiles/assets/masonry-icon.svg',
			altText: 'Line icon for masonry work',
			contractorCategory: 'masonry',
			tags: ['preset', 'masonry', 'icon'],
			sortOrder: 16
		},
		{
			key: 'preset-landscaping-icon',
			name: 'Landscaping preset icon',
			type: 'icon',
			file: '/clientFiles/assets/landscaping-icon.svg',
			altText: 'Line icon for landscaping work',
			contractorCategory: 'landscaping',
			tags: ['preset', 'landscaping', 'icon'],
			sortOrder: 17
		},
		{
			key: 'preset-hvac-icon',
			name: 'HVAC preset icon',
			type: 'icon',
			file: '/clientFiles/assets/hvac-icon.svg',
			altText: 'Line icon for HVAC work',
			contractorCategory: 'hvac',
			tags: ['preset', 'hvac', 'icon'],
			sortOrder: 18
		},
		{
			key: 'preset-remodeling-icon',
			name: 'Remodeling preset icon',
			type: 'icon',
			file: '/clientFiles/assets/remodeling-icon.svg',
			altText: 'Line icon for remodeling work',
			contractorCategory: 'remodeling',
			tags: ['preset', 'remodeling', 'icon'],
			sortOrder: 19
		},
		{
			key: 'preset-exterior-icon',
			name: 'Exterior services preset icon',
			type: 'icon',
			file: '/clientFiles/assets/exterior-services-icon.svg',
			altText: 'Line icon for exterior services work',
			contractorCategory: 'exterior-services',
			tags: ['preset', 'exterior-services', 'icon'],
			sortOrder: 20
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
	contractorPresets: [
		{
			id: 'concrete-flatwork',
			label: 'Concrete / Flatwork',
			contractorType: 'concrete',
			defaultHeroHeadline: 'Concrete driveways, patios, and flatwork handled with a clear path from request to finished pour.',
			defaultServices: [
				{
					name: 'Driveways',
					slug: 'driveways',
					description: 'New pours, replacements, widening, and apron tie-ins.',
					iconAssetKey: 'service-driveways-icon',
					imageAssetKey: 'hero-driveway-scene'
				},
				{
					name: 'Patios',
					slug: 'patios',
					description: 'Backyard entertainment slabs, extensions, and broom-finish installs.',
					iconAssetKey: 'service-patios-icon'
				},
				{
					name: 'Sidewalks',
					slug: 'sidewalks',
					description: 'Walkways, repair sections, and pedestrian flatwork.',
					iconAssetKey: 'service-sidewalks-icon'
				},
				{
					name: 'Decorative Concrete',
					slug: 'decorative-concrete',
					description: 'Stamped, colored, and upgraded finishes for high-visual-impact surfaces.',
					iconAssetKey: 'service-decorative-icon'
				}
			],
			defaultIconAssetKeys: ['service-driveways-icon', 'service-patios-icon', 'service-sidewalks-icon', 'service-decorative-icon']
		},
		{
			id: 'roofing-restoration',
			label: 'Roofing / Restoration',
			contractorType: 'roofing',
			defaultHeroHeadline: 'Roofing and storm-restoration work with fast intake, clear scopes, and dependable schedule follow-through.',
			defaultServices: [
				{
					name: 'Roof replacement',
					slug: 'roof-replacement',
					description: 'Full tear-off and replacement scopes with ventilation and cleanup coordination.',
					iconAssetKey: 'preset-roofing-icon'
				},
				{
					name: 'Leak repair',
					slug: 'leak-repair',
					description: 'Urgent repairs, flashing fixes, and storm-damage response.',
					iconAssetKey: 'preset-roofing-icon'
				},
				{
					name: 'Storm restoration',
					slug: 'storm-restoration',
					description: 'Insurance support, photo sets, and supplement-ready documentation.',
					iconAssetKey: 'preset-roofing-icon'
				}
			],
			defaultIconAssetKeys: ['preset-roofing-icon']
		},
		{
			id: 'masonry-hardscape',
			label: 'Masonry / Hardscape',
			contractorType: 'masonry',
			defaultHeroHeadline: 'Masonry and hardscape crews with a cleaner handoff from design questions to field-ready scope.',
			defaultServices: [
				{
					name: 'Brick repair',
					slug: 'brick-repair',
					description: 'Repointing, repairs, and selective rebuilds for worn masonry areas.',
					iconAssetKey: 'preset-masonry-icon'
				},
				{
					name: 'Retaining walls',
					slug: 'retaining-walls',
					description: 'Structural and decorative wall scopes with drainage planning.',
					iconAssetKey: 'preset-masonry-icon'
				},
				{
					name: 'Fire pits and veneer',
					slug: 'fire-pits-veneer',
					description: 'Outdoor masonry focal points and facade upgrades.',
					iconAssetKey: 'preset-masonry-icon'
				}
			],
			defaultIconAssetKeys: ['preset-masonry-icon']
		},
		{
			id: 'landscaping-outdoor',
			label: 'Landscaping / Outdoor',
			contractorType: 'landscaping',
			defaultHeroHeadline: 'Landscaping sites launched with service defaults that keep seasonal crews and outdoor work organized.',
			defaultServices: [
				{
					name: 'Landscape installs',
					slug: 'landscape-installs',
					description: 'Planting, bed refreshes, grading, and outdoor cleanup packages.',
					iconAssetKey: 'preset-landscaping-icon'
				},
				{
					name: 'Drainage work',
					slug: 'drainage-work',
					description: 'Yard drainage corrections, swales, and runoff control.',
					iconAssetKey: 'preset-landscaping-icon'
				},
				{
					name: 'Outdoor maintenance',
					slug: 'outdoor-maintenance',
					description: 'Seasonal care plans and repeat maintenance visits.',
					iconAssetKey: 'preset-landscaping-icon'
				}
			],
			defaultIconAssetKeys: ['preset-landscaping-icon']
		},
		{
			id: 'hvac-comfort',
			label: 'HVAC / Comfort',
			contractorType: 'hvac',
			defaultHeroHeadline: 'HVAC replacements, repairs, and maintenance work launched from a consistent contractor-site baseline.',
			defaultServices: [
				{
					name: 'System replacement',
					slug: 'system-replacement',
					description: 'Full equipment change-outs with options and installation scheduling.',
					iconAssetKey: 'preset-hvac-icon'
				},
				{
					name: 'Repair service',
					slug: 'repair-service',
					description: 'Diagnostic visits, urgent repair calls, and component replacements.',
					iconAssetKey: 'preset-hvac-icon'
				},
				{
					name: 'Maintenance plans',
					slug: 'maintenance-plans',
					description: 'Seasonal tune-ups and recurring service agreements.',
					iconAssetKey: 'preset-hvac-icon'
				}
			],
			defaultIconAssetKeys: ['preset-hvac-icon']
		},
		{
			id: 'remodeling-interiors',
			label: 'Remodeling / Interiors',
			contractorType: 'remodeling',
			defaultHeroHeadline: 'Remodeling sites with clearer default messaging for consultation, scope, and build planning.',
			defaultServices: [
				{
					name: 'Kitchen remodels',
					slug: 'kitchen-remodels',
					description: 'Cabinet, surface, and fixture renovation projects.',
					iconAssetKey: 'preset-remodeling-icon'
				},
				{
					name: 'Bathroom remodels',
					slug: 'bathroom-remodels',
					description: 'Bath upgrades, tile, plumbing fixtures, and layout improvements.',
					iconAssetKey: 'preset-remodeling-icon'
				},
				{
					name: 'Interior finish work',
					slug: 'interior-finish-work',
					description: 'Trim, paint, flooring, and room refresh packages.',
					iconAssetKey: 'preset-remodeling-icon'
				}
			],
			defaultIconAssetKeys: ['preset-remodeling-icon']
		},
		{
			id: 'exterior-services',
			label: 'Exterior Services',
			contractorType: 'exterior-services',
			defaultHeroHeadline: 'Exterior services that keep siding, gutters, trim, and facade projects moving through a shared workflow.',
			defaultServices: [
				{
					name: 'Siding repair',
					slug: 'siding-repair',
					description: 'Selective repairs and full siding refresh scopes.',
					iconAssetKey: 'preset-exterior-icon'
				},
				{
					name: 'Gutter systems',
					slug: 'gutter-systems',
					description: 'Replacement gutters, guards, and drainage improvements.',
					iconAssetKey: 'preset-exterior-icon'
				},
				{
					name: 'Trim and fascia',
					slug: 'trim-fascia',
					description: 'Exterior trim, fascia, and detail work that protects the envelope.',
					iconAssetKey: 'preset-exterior-icon'
				}
			],
			defaultIconAssetKeys: ['preset-exterior-icon']
		}
	],
	activeContractorPresetId: 'concrete-flatwork',
	themeSettings: {
		mode: 'Dark',
		preset: 'Industrial',
		colors: {
			primary: '#F97316',
			secondary: '#171717',
			accent: '#F5F5F5',
			background: '#0F1111',
			surface: '#1F1F1F',
			text: '#FFFFFF',
			border: '#2A2A2A'
		},
		typography: {
			headingFont: 'Oswald',
			bodyFont: 'Inter'
		},
		sizing: {
			buttonRadius: '999px',
			cardRadius: '1.5rem',
			logoSize: 'Medium'
		},
		iconStyle: 'Line',
		brandAssets: {
			logoAssetKey: 'bdr-crest-logo',
			faviconAssetKey: 'bdr-favicon-mark'
		}
	},
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

export const getBdrContractorPresets = (content: BdrSiteContent) => [...content.contractorPresets];

export const getBdrActiveContractorPreset = (content: BdrSiteContent) =>
	content.contractorPresets.find((preset) => preset.id === content.activeContractorPresetId) ??
	content.contractorPresets[0] ??
	null;

export const applyBdrContractorPresetToContent = (
	content: BdrSiteContent,
	presetId: string
) => {
	const preset = content.contractorPresets.find((candidate) => candidate.id === presetId);

	if (!preset) {
		return null;
	}

	content.activeContractorPresetId = preset.id;
	content.hero.headline = preset.defaultHeroHeadline;
	content.services.items = preset.defaultServices.map((service) => service.name);
	content.serviceCategories = preset.defaultServices.map((service, index) => ({
		name: service.name,
		slug: service.slug,
		description: service.description,
		iconAssetKey: service.iconAssetKey,
		imageAssetKey: service.imageAssetKey,
		contractorType: preset.contractorType,
		featured: true,
		sortOrder: index + 1
	}));

	return preset;
};
