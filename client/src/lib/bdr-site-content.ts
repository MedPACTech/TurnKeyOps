export type ContentLink = {
	label: string;
	href: string;
	openInNewTab?: boolean;
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
	detailPageUrl?: string;
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

export type BdrCtaType = 'anchor' | 'link' | 'phone';

export type BdrHeroTrustBadge = {
	iconAssetKey: string;
	title: string;
	description: string;
};

export type BdrHeroMediaOverride = {
	contractorType: string;
	heroImageAssetKey: string;
	backgroundImageAssetKey?: string;
	backgroundTextureAssetKey?: string;
	heroImageAltText?: string;
};

export type BdrSocialLink = {
	platform: string;
	url: string;
	iconAssetKey: string;
};

export type BdrQuoteFormFieldType = 'text' | 'email' | 'tel' | 'textarea' | 'select' | 'file';

export type BdrQuoteFormField = {
	key: string;
	label: string;
	type: BdrQuoteFormFieldType;
	placeholder?: string;
	required: boolean;
	options?: string[];
};

export type BdrQuoteFormBenefit = {
	iconAssetKey: string;
	text: string;
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
		logoAssetKey: string;
		faviconAssetKey: string;
		links: ContentLink[];
		primaryCtaLabel: string;
		primaryCtaHref: string;
		phoneNumber: string;
		showPhoneButton: boolean;
		showThemeControl: boolean;
		stickyHeader: boolean;
		layout: 'logo-left' | 'centered' | 'right-aligned';
	};
	hero: {
		eyebrow: string;
		headline: string;
		subheadline: string;
		primaryCtaLabel: string;
		primaryCtaHref: string;
		primaryCtaType: BdrCtaType;
		secondaryCtaLabel: string;
		secondaryCtaHref: string;
		secondaryCtaType: BdrCtaType;
		heroImageAssetKey: string;
		heroImageAltText: string;
		backgroundImageAssetKey: string;
		backgroundTextureAssetKey: string;
		trustBadgeEyebrow: string;
		trustBadges: BdrHeroTrustBadge[];
		mediaByContractorType: BdrHeroMediaOverride[];
	};
	services: {
		eyebrow: string;
		title: string;
		copy: string;
		items: string[];
		ctaLabel: string;
		ctaHref: string;
	};
	trust: {
		eyebrow: string;
		title: string;
		copy: string;
		points: string[];
	};
	process: {
		eyebrow: string;
		title: string;
		description: string;
		steps: Array<{
			step: string;
			title: string;
			copy: string;
			iconAssetKey: string;
			timeframe?: string;
		}>;
	};
	ctaBanner: {
		eyebrow: string;
		title: string;
		description: string;
		backgroundImageAssetKey: string;
		backgroundImageAltText: string;
		overlayOpacity: number;
		primaryCtaLabel: string;
		primaryCtaHref: string;
		secondaryCtaLabel: string;
		secondaryCtaType: BdrCtaType;
		secondaryCtaHref: string;
	};
	quoteForm: {
		eyebrow: string;
		title: string;
		description: string;
		privacyReassurance: string;
		benefits: BdrQuoteFormBenefit[];
		fields: BdrQuoteFormField[];
		submitButtonLabel: string;
		successMessage: string;
		notificationRecipients: string[];
		queueDestination: string;
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
		logoAssetKey: string;
		brandName: string;
		body: string;
		serviceAreaText: string;
		navigationEyebrow: string;
		navigationLinks: ContentLink[];
		servicesEyebrow: string;
		servicesLinks: ContentLink[];
		contactEyebrow: string;
		phone: string;
		email: string;
		address: string;
		socialLinks: BdrSocialLink[];
	};
	postFooter: {
		legalLinksEyebrow: string;
		legalLinks: ContentLink[];
		copyright: string;
	};
};

export const bdrSiteContent: BdrSiteContent = {
	assetLibrary: [
		{
			key: 'bdr-crest-logo',
			name: 'Company crest logo',
			type: 'logo',
			file: '/clientFiles/BDRLogo-transparent.png',
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
			key: 'social-facebook-icon',
			name: 'Facebook social icon',
			type: 'icon',
			file: '/clientFiles/assets/social-facebook-icon.svg',
			altText: 'Facebook icon',
			contractorCategory: 'shared',
			tags: ['social', 'footer', 'icon'],
			sortOrder: 3
		},
		{
			key: 'social-instagram-icon',
			name: 'Instagram social icon',
			type: 'icon',
			file: '/clientFiles/assets/social-instagram-icon.svg',
			altText: 'Instagram icon',
			contractorCategory: 'shared',
			tags: ['social', 'footer', 'icon'],
			sortOrder: 4
		},
		{
			key: 'social-linkedin-icon',
			name: 'LinkedIn social icon',
			type: 'icon',
			file: '/clientFiles/assets/social-linkedin-icon.svg',
			altText: 'LinkedIn icon',
			contractorCategory: 'shared',
			tags: ['social', 'footer', 'icon'],
			sortOrder: 5
		},
		{
			key: 'hero-driveway-scene',
			name: 'Modern home driveway hero',
			type: 'hero-image',
			file: '/clientFiles/image17.jpeg',
			altText: 'Modern home with a finished concrete driveway',
			contractorCategory: 'concrete',
			tags: ['hero', 'driveway', 'mockup'],
			sortOrder: 6
		},
		{
			key: 'cta-finishing-scene',
			name: 'Concrete finishing CTA banner',
			type: 'background-image',
			file: '/clientFiles/image29.jpeg',
			altText: 'Concrete finishing equipment at work on site',
			contractorCategory: 'concrete',
			tags: ['cta', 'banner', 'equipment', 'mockup'],
			sortOrder: 7
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
				key: 'service-driveways-icon-white',
				name: 'Driveways service icon white',
				type: 'icon',
				file: '/clientFiles/assets/icon_driveways_white.png',
				altText: 'White line icon for driveway work',
				contractorCategory: 'concrete',
				tags: ['service', 'icon', 'driveways', 'white'],
				sortOrder: 101
			},
			{
				key: 'service-patios-icon-white',
				name: 'Patios service icon white',
				type: 'icon',
				file: '/clientFiles/assets/icon_patios_white.png',
				altText: 'White line icon for patio work',
				contractorCategory: 'concrete',
				tags: ['service', 'icon', 'patios', 'white'],
				sortOrder: 102
			},
			{
				key: 'service-sidewalks-icon-white',
				name: 'Sidewalks service icon white',
				type: 'icon',
				file: '/clientFiles/assets/icon_sidewalks_white.png',
				altText: 'White line icon for sidewalk work',
				contractorCategory: 'concrete',
				tags: ['service', 'icon', 'sidewalks', 'white'],
				sortOrder: 103
			},
			{
				key: 'service-steps-icon-white',
				name: 'Steps and stoops service icon white',
				type: 'icon',
				file: '/clientFiles/assets/icon_steps-stoops_white.png',
				altText: 'White line icon for steps and stoops work',
				contractorCategory: 'concrete',
				tags: ['service', 'icon', 'steps', 'stoops', 'white'],
				sortOrder: 104
			},
			{
				key: 'service-slabs-icon-white',
				name: 'Concrete slabs service icon white',
				type: 'icon',
				file: '/clientFiles/assets/icon_concrete-slabs_white.png',
				altText: 'White line icon for slab work',
				contractorCategory: 'concrete',
				tags: ['service', 'icon', 'slabs', 'white'],
				sortOrder: 105
			},
			{
				key: 'service-decorative-icon-white',
				name: 'Decorative concrete service icon white',
				type: 'icon',
				file: '/clientFiles/assets/icon_decorative-concrete_white.png',
				altText: 'White line icon for decorative concrete work',
				contractorCategory: 'concrete',
				tags: ['service', 'icon', 'decorative', 'white'],
				sortOrder: 106
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
				description: 'Strong, clean, and built to last.',
				iconAssetKey: 'service-driveways-icon',
				imageAssetKey: 'hero-driveway-scene',
				contractorType: 'concrete',
			featured: true,
			sortOrder: 1
		},
			{
				name: 'Patios',
				slug: 'patios',
				description: 'Beautiful outdoor spaces that stand up to every season.',
				iconAssetKey: 'service-patios-icon',
				contractorType: 'concrete',
				featured: true,
			sortOrder: 2
		},
			{
				name: 'Sidewalks',
				slug: 'sidewalks',
				description: 'Safe, smooth, and built with precision.',
				iconAssetKey: 'service-sidewalks-icon',
				contractorType: 'concrete',
				featured: true,
			sortOrder: 3
		},
			{
				name: 'Steps & Stoops',
				slug: 'steps-stoops',
				description: 'Functional steps with a clean, solid finish.',
				iconAssetKey: 'service-steps-icon',
				contractorType: 'concrete',
				featured: true,
			sortOrder: 4
		},
			{
				name: 'Concrete Slabs',
				slug: 'concrete-slabs',
				description: 'Foundations, pads, and more.',
				iconAssetKey: 'service-slabs-icon',
				contractorType: 'concrete',
				featured: true,
			sortOrder: 5
		},
			{
				name: 'Decorative Concrete',
				slug: 'decorative-concrete',
				description: 'Stamped, stained, and custom finishes.',
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
			buttonRadius: '0.18rem',
			cardRadius: '0.18rem',
			logoSize: 'Medium'
		},
		iconStyle: 'Line',
		brandAssets: {
			logoAssetKey: 'bdr-crest-logo',
			faviconAssetKey: 'bdr-favicon-mark'
		}
	},
	navigation: {
		announcement: 'Concrete work done right',
		brandName: 'BDR Construction',
		logoAssetKey: 'bdr-crest-logo',
		faviconAssetKey: 'bdr-favicon-mark',
		links: [
			{ href: '#hero', label: 'Home', openInNewTab: false },
			{ href: '#services', label: 'Services', openInNewTab: false },
			{ href: '#process', label: 'Our Process', openInNewTab: false },
			{ href: '#trust', label: 'Why BDR', openInNewTab: false },
			{ href: '#supporting', label: 'Projects', openInNewTab: false },
			{ href: '#contact', label: 'Contact', openInNewTab: false }
		],
		primaryCtaLabel: 'Get a Free Quote',
		primaryCtaHref: '#quote-request',
		phoneNumber: '(704) 555-0100',
		showPhoneButton: true,
		showThemeControl: false,
		stickyHeader: true,
		layout: 'logo-left'
	},
	hero: {
		eyebrow: 'Concrete work done right',
		headline: 'Built strong. Built to last.',
		subheadline:
			'From driveways to patios and everything in between, BDR Construction delivers high-quality concrete work with unmatched craftsmanship.',
		primaryCtaLabel: 'Get a free quote',
		primaryCtaHref: '#quote-request',
		primaryCtaType: 'anchor',
		secondaryCtaLabel: '(704) 555-0100',
		secondaryCtaHref: '(704) 555-0100',
		secondaryCtaType: 'phone',
		heroImageAssetKey: 'hero-driveway-scene',
		heroImageAltText: 'Fresh concrete driveway work in front of a residential garage',
		backgroundImageAssetKey: 'hero-driveway-scene',
		backgroundTextureAssetKey: 'grain-overlay-texture',
		trustBadgeEyebrow: '',
		trustBadges: [
			{
				iconAssetKey: 'service-driveways-icon-white',
				title: 'Licensed & Insured',
				description: 'Fully licensed in NC'
			},
			{
				iconAssetKey: 'service-sidewalks-icon-white',
				title: '5-Star Rated',
				description: '100+ happy customers'
			},
			{
				iconAssetKey: 'service-slabs-icon-white',
				title: 'Quality Guaranteed',
				description: 'Built to last. Every time.'
			}
		],
		mediaByContractorType: []
	},
	services: {
		eyebrow: 'Our concrete services',
		title: 'Solid solutions for every project',
		copy:
			'Durable. Reliable. Built to handle what life throws at it.',
		items: [
			'Driveways',
			'Patios',
			'Sidewalks',
			'Steps and stoops',
			'Concrete slabs',
			'Decorative concrete'
		],
		ctaLabel: 'View all services ->',
		ctaHref: '#quote-request'
	},
	trust: {
		eyebrow: 'Why BDR',
		title: 'Why customers trust the work',
		copy:
			'Concrete projects need clear communication, dependable scheduling, and crews that leave the finish looking right.',
		points: [
			'Licensed and insured',
			'Free, honest estimates',
			'Clear schedule communication',
			'Quality work built to last'
		]
	},
	process: {
		eyebrow: 'Our process',
		title: 'Straightforward from start to finish',
		description:
			'Simple steps, clear communication, and concrete work done right.',
		steps: [
				{
					step: '1',
					title: 'Request a quote',
					copy: 'Tell us about your project. We will reach out within 24 hours.',
					iconAssetKey: 'service-driveways-icon-white',
					timeframe: 'Day 0'
				},
			{
					step: '2',
					title: 'Scope & estimate',
					copy: 'We visit the site, discuss your needs, and provide a clear estimate.',
					iconAssetKey: 'service-sidewalks-icon-white',
					timeframe: '1 to 3 days'
				},
			{
					step: '3',
					title: 'Schedule & build',
					copy: 'We schedule the work and build it right the first time.',
					iconAssetKey: 'service-slabs-icon-white',
					timeframe: 'Next available slot'
				}
		]
	},
	ctaBanner: {
		eyebrow: 'Ready to get started?',
		title: "Let's build something solid.",
		description:
			'Get a free, no-obligation quote for your concrete project today.',
		backgroundImageAssetKey: 'cta-finishing-scene',
		backgroundImageAltText: 'Concrete finishing equipment active on a work site',
		overlayOpacity: 0.74,
		primaryCtaLabel: 'Request a quote',
		primaryCtaHref: '#quote-request',
		secondaryCtaLabel: '(704) 555-0100',
		secondaryCtaType: 'phone',
		secondaryCtaHref: '(704) 555-0100'
	},
	quoteForm: {
		eyebrow: 'Request a quote',
		title: 'Tell BDR about the project',
		description:
			'Share the property, timing, and what is going on. The BDR office can review the request, follow up, schedule the inspection, and move your project toward a quote.',
		privacyReassurance:
			'We only use this information to review the request, follow up, and route the job into the right admin workflow.',
		benefits: [
			{
				iconAssetKey: 'service-slabs-icon',
				text: 'Takes less than 2 minutes.'
			},
			{
				iconAssetKey: 'service-driveways-icon',
				text: 'No obligation.'
			},
			{
				iconAssetKey: 'service-sidewalks-icon',
				text: 'Free, honest estimates.'
			}
		],
		fields: [
			{
				key: 'contactName',
				label: 'Full Name',
				type: 'text',
				placeholder: 'Jane Smith',
				required: true
			},
			{
				key: 'phone',
				label: 'Phone Number',
				type: 'tel',
				placeholder: '(704) 555-0100',
				required: true
			},
			{
				key: 'email',
				label: 'Email Address',
				type: 'email',
				placeholder: 'jane@example.com',
				required: true
			},
			{
				key: 'serviceType',
				label: 'Service Needed',
				type: 'select',
				required: true,
				options: [
					'Driveway',
					'Patio',
					'Sidewalk',
					'Steps / stoops',
					'Concrete slab',
					'Decorative concrete'
				]
			},
			{
				key: 'propertyType',
				label: 'Project Type',
				type: 'select',
				required: true,
				options: ['Residential', 'Commercial', 'HOA / property management', 'Multi-site / portfolio']
			},
			{
				key: 'need',
				label: 'Project Details',
				type: 'textarea',
				placeholder:
					'Tell BDR what is happening, any access notes, and the best time to reach you.',
				required: true
			},
			{
				key: 'attachments',
				label: 'Photos or files',
				type: 'file',
				required: false
			}
		],
		submitButtonLabel: 'Get My Free Quote',
		successMessage:
			'BDR now has your project details and can follow up, confirm scope, and move the request into inspection and estimate handling.',
		notificationRecipients: ['office@bdrconstruction.com', 'estimating@bdrconstruction.com'],
		queueDestination: 'External Admin Intake Queue'
	},
	supportingSections: [
		{
			eyebrow: 'Projects',
			title: 'Concrete work BDR handles',
			copy: 'The common residential and commercial flatwork customers need from first call to finished pour.',
			items: [
				{
					title: 'Driveways',
					copy: 'New pours, replacements, widening, and apron tie-ins built for daily use.'
				},
				{
					title: 'Patios and sidewalks',
					copy: 'Outdoor slabs, walkways, and repair sections with clean finish work.'
				},
				{
					title: 'Slabs and decorative concrete',
					copy: 'Pads, stoops, stamped finishes, and utility flatwork with a clear build plan.'
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
			'If you need a driveway, patio, sidewalk, slab, or decorative concrete work, BDR Construction can help you move from estimate to finished pour with a cleaner process.',
		primaryCtaLabel: 'Request a quote',
		primaryCtaHref: '#quote-request',
		secondaryCtaLabel: 'Call BDR',
		secondaryCtaHref: 'tel:7045550100'
	},
	footer: {
		eyebrow: 'Footer',
		logoAssetKey: 'bdr-crest-logo',
		brandName: 'BDR Construction',
		body:
			'Quality concrete work for homes and businesses across North Carolina.',
		serviceAreaText: 'Serving Charlotte, Concord, Mooresville, and surrounding communities.',
		navigationEyebrow: 'Quick links',
		navigationLinks: [
			{ href: '#hero', label: 'Home' },
			{ href: '#services', label: 'Services' },
			{ href: '#process', label: 'Process' },
			{ href: '#quote-request', label: 'Request a Quote' },
			{ href: '#contact', label: 'Contact' }
		],
		servicesEyebrow: 'Services',
		servicesLinks: [
			{ href: '#services', label: 'Driveways' },
			{ href: '#services', label: 'Patios' },
			{ href: '#services', label: 'Sidewalks' },
			{ href: '#services', label: 'Steps & Stoops' },
			{ href: '#services', label: 'Concrete Slabs' },
			{ href: '#services', label: 'Decorative Concrete' }
		],
		contactEyebrow: 'Contact',
		phone: '(704) 555-0100',
		email: 'office@bdrconstruction.com',
		address: 'Charlotte, NC',
		socialLinks: [
			{
				platform: 'Facebook',
				url: 'https://facebook.com/bdrconstruction',
				iconAssetKey: 'social-facebook-icon'
			},
			{
				platform: 'Instagram',
				url: 'https://instagram.com/bdrconstruction',
				iconAssetKey: 'social-instagram-icon'
			},
			{
				platform: 'LinkedIn',
				url: 'https://linkedin.com/company/bdrconstruction',
				iconAssetKey: 'social-linkedin-icon'
			}
		]
	},
	postFooter: {
		legalLinksEyebrow: 'Legal',
		legalLinks: [
			{ href: '#hero', label: 'Back to top' },
			{ href: '/privacy-policy', label: 'Privacy Policy' },
			{ href: '/terms-of-service', label: 'Terms of Service' }
		],
		copyright: '© {{year}} BDR Construction. All rights reserved.'
	}
};

export const resolveBdrCopyright = (
	contentOrYear: BdrSiteContent | number,
	year?: number
) => {
	const template =
		typeof contentOrYear === 'number'
			? bdrSiteContent.postFooter.copyright
			: contentOrYear.postFooter.copyright;
	const resolvedYear = typeof contentOrYear === 'number' ? contentOrYear : year ?? new Date().getFullYear();
	return template.replace('{{year}}', String(resolvedYear));
};

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
