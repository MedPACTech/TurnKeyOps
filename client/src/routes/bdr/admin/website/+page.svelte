<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import ContentEditorCard, {
		type EditableField,
		type EditableListItem
	} from '$lib/components/admin/ContentEditorCard.svelte';
	import {
		getBdrActiveContractorPreset,
		getBdrAsset,
		getBdrContractorPresets,
		getBdrServiceCategories,
		resolveBdrCopyright
	} from '$lib/bdr-site-content';
	import { untrack } from 'svelte';
	import type { PageProps } from './$types';

	type WebsiteSection = {
		id: string;
		label: string;
		description: string;
		previewTitle: string;
		previewBody: string;
		previewMeta?: string;
		fields?: EditableField[];
		listTitle?: string;
		listItems?: EditableListItem[];
		actions?: string[];
		areas: Array<{
			id: string;
			label: string;
			value: string;
			detail?: string;
			fields?: EditableField[];
			listTitle?: string;
			listItems?: EditableListItem[];
			actions?: string[];
		}>;
	};
	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};

	type WebsitePageForm = {
		content?: PageProps['data']['content'];
		savedSectionId?: string;
		savedMessage?: string;
		message?: string;
	};

	let { data, form }: { data: PageProps['data']; form?: WebsitePageForm } = $props();
	const content = $derived(form?.content ?? data.content);

	const year = new Date().getFullYear();
	const assetLibrary = $derived([...content.assetLibrary].sort((left, right) => left.sortOrder - right.sortOrder));
	const serviceCategories = $derived(getBdrServiceCategories(content));
	const contractorPresets = $derived(getBdrContractorPresets(content));
	const activeContractorPreset = $derived(getBdrActiveContractorPreset(content));
	const heroImageAssets = $derived(
		assetLibrary.filter((asset) =>
			asset.type === 'hero-image' ||
			asset.type === 'background-image' ||
			asset.type === 'project-photo'
		)
	);
	const footerLogoAssets = $derived(
		assetLibrary.filter((asset) => asset.type === 'logo' || asset.type === 'icon')
	);
	const textureAssets = $derived(
		assetLibrary.filter((asset) => asset.type === 'texture' || asset.type === 'background-image')
	);
	const heroBadgeAssets = $derived(
		assetLibrary.filter((asset) => asset.type === 'icon' || asset.type === 'logo')
	);

	let serviceItems = $state(untrack(() => [...content.services.items]));

	const websiteSections = $derived<WebsiteSection[]>([
		{
			id: 'navigation',
			label: 'Navigation',
			description: 'Top-level wayfinding and utility announcement copy.',
			previewTitle: content.navigation.brandName,
			previewBody: content.navigation.announcement,
			previewMeta: `${content.navigation.layout} · ${content.navigation.links.length} links`,
			fields: [
				{ label: 'Announcement', value: content.navigation.announcement },
				{ label: 'Brand label', value: content.navigation.brandName },
				{ label: 'Primary CTA', value: content.navigation.primaryCtaLabel },
				{ label: 'Phone CTA', value: content.navigation.showPhoneButton ? content.navigation.phoneNumber : 'Hidden' }
			],
			listTitle: 'Navigation links',
			listItems: content.navigation.links.map((link) => ({ label: 'Link', value: link.label, detail: `${link.href}${link.openInNewTab ? ' · new tab' : ''}` })),
			actions: ['Preview anchor flow', 'Adjust CTA / phone posture', 'Review mobile header'],
			areas: [
				{
					id: 'nav-announcement',
					label: 'Announcement strip',
					value: content.navigation.announcement,
					fields: [
						{ label: 'Announcement', value: content.navigation.announcement },
						{ label: 'Logo asset', value: content.navigation.logoAssetKey },
						{ label: 'Favicon asset', value: content.navigation.faviconAssetKey }
					]
				},
				{
					id: 'nav-links',
					label: 'Primary navigation',
					value: content.navigation.links.map((link) => link.label).join(' · '),
					listTitle: 'Navigation links',
					listItems: content.navigation.links.map((link) => ({ label: 'Link', value: link.label, detail: `${link.href}${link.openInNewTab ? ' · new tab' : ''}` })),
					actions: ['Reorder links', 'Review new-tab behavior']
				},
				{
					id: 'nav-utility',
					label: 'CTA / utility controls',
					value: `${content.navigation.primaryCtaLabel} · ${content.navigation.showPhoneButton ? content.navigation.phoneNumber : 'phone hidden'}`,
					fields: [
						{ label: 'Primary CTA label', value: content.navigation.primaryCtaLabel },
						{ label: 'Primary CTA target', value: content.navigation.primaryCtaHref },
						{ label: 'Phone number', value: content.navigation.phoneNumber },
						{ label: 'Theme control', value: content.navigation.showThemeControl ? 'Shown' : 'Hidden' },
						{ label: 'Sticky header', value: content.navigation.stickyHeader ? 'Enabled' : 'Disabled' },
						{ label: 'Layout', value: content.navigation.layout }
					]
				}
			]
		},
		{
			id: 'hero',
			label: 'Hero',
			description: 'The first-screen message, CTA behavior, media, and trust badges.',
			previewTitle: content.hero.headline,
			previewBody: content.hero.subheadline,
			previewMeta: `${content.hero.primaryCtaType} / ${content.hero.secondaryCtaType} · ${content.hero.trustBadges.length} badges`,
			fields: [
				{ label: 'Eyebrow', value: content.hero.eyebrow },
				{ label: 'Headline', value: content.hero.headline, multiline: true },
				{ label: 'Subheadline', value: content.hero.subheadline, multiline: true }
			],
			actions: ['Preview hero', 'Swap contractor media', 'Review CTA behavior'],
			areas: [
				{
					id: 'hero-copy',
					label: 'Hero copy',
					value: content.hero.headline,
					detail: content.hero.subheadline,
					fields: [
						{ label: 'Eyebrow', value: content.hero.eyebrow },
						{ label: 'Headline', value: content.hero.headline, multiline: true },
						{ label: 'Subheadline', value: content.hero.subheadline, multiline: true }
					]
				},
				{
					id: 'hero-cta',
					label: 'Hero CTAs',
					value: `${content.hero.primaryCtaLabel} / ${content.hero.secondaryCtaLabel}`,
					fields: [
						{ label: 'Primary CTA', value: `${content.hero.primaryCtaLabel} (${content.hero.primaryCtaType})` },
						{ label: 'Primary target', value: content.hero.primaryCtaHref },
						{ label: 'Secondary CTA', value: `${content.hero.secondaryCtaLabel} (${content.hero.secondaryCtaType})` },
						{ label: 'Secondary target', value: content.hero.secondaryCtaHref }
					]
				},
				{
					id: 'hero-media',
					label: 'Hero media',
					value: content.hero.heroImageAssetKey,
					detail: content.hero.heroImageAltText,
					fields: [
						{ label: 'Hero image asset', value: content.hero.heroImageAssetKey },
						{ label: 'Background image', value: content.hero.backgroundImageAssetKey || 'None' },
						{ label: 'Background texture', value: content.hero.backgroundTextureAssetKey || 'None' }
					]
				},
				{
					id: 'hero-trust',
					label: 'Trust badges',
					value: content.hero.trustBadges.map((badge) => badge.title).join(' · '),
					listTitle: 'Trust badges',
					listItems: content.hero.trustBadges.map((badge) => ({
						label: badge.iconAssetKey,
						value: badge.title,
						detail: badge.description
					})),
					actions: ['Reorder badges', 'Review contractor overrides'],
					fields: [
						{ label: 'Trust badge eyebrow', value: content.hero.trustBadgeEyebrow },
						{ label: 'Media overrides', value: `${content.hero.mediaByContractorType.length} contractor-specific entries` }
					]
				}
			]
		},
		{
			id: 'services',
			label: 'Services',
			description: 'Section copy plus the featured services card grid.',
			previewTitle: content.services.title,
			previewBody: content.services.copy,
			previewMeta: `${serviceCategories.length || serviceItems.length} service cards`,
			fields: [
				{ label: 'Eyebrow', value: content.services.eyebrow },
				{ label: 'Title', value: content.services.title },
				{ label: 'Body copy', value: content.services.copy, multiline: true },
				{ label: 'Section CTA', value: `${content.services.ctaLabel} → ${content.services.ctaHref}` }
			],
			listTitle: 'Service cards',
			listItems: (serviceCategories.length ? serviceCategories : serviceItems.map((item, index) => ({ name: item, description: '', iconAssetKey: 'n/a', sortOrder: index + 1, featured: true }))).map((item) => ({ label: item.featured ? 'Featured' : 'Standard', value: item.name, detail: item.description || item.iconAssetKey })),
			actions: ['Edit service cards', 'Adjust card mix', 'Review section CTA'],
			areas: [
				{
					id: 'services-copy',
					label: 'Services intro',
					value: content.services.title,
					detail: content.services.copy,
					fields: [
						{ label: 'Eyebrow', value: content.services.eyebrow },
						{ label: 'Title', value: content.services.title },
						{ label: 'Body copy', value: content.services.copy, multiline: true },
						{ label: 'Section CTA', value: `${content.services.ctaLabel} → ${content.services.ctaHref}` }
					]
				},
				{
					id: 'services-items',
					label: 'Service cards',
					value: (serviceCategories.length ? serviceCategories.map((item) => item.name) : serviceItems).join(' · '),
					listTitle: 'Service cards',
					listItems: (serviceCategories.length
						? serviceCategories.map((item) => ({
								label: item.featured ? 'Featured' : 'Card',
								value: item.name,
								detail: `${item.description}${item.detailPageUrl ? ` · ${item.detailPageUrl}` : ''}`
							}))
						: serviceItems.map((item) => ({ label: 'Service', value: item }))),
					actions: ['Add service card', 'Sort cards', 'Update icons']
				}
			]
		},
		{
			id: 'asset-library',
			label: 'Asset library',
			description: 'Reusable brand, hero, texture, and icon assets for contractor templates.',
			previewTitle: assetLibrary.slice(0, 3).map((asset) => asset.name).join(' · '),
			previewBody: 'Reusable media is modeled separately so sections and presets can reference shared assets.',
			previewMeta: `${assetLibrary.length} assets`,
			areas: [
				{
					id: 'asset-inventory',
					label: 'Asset inventory',
					value: assetLibrary.map((asset) => asset.type).join(' · '),
					fields: [
						{ label: 'Reusable asset types', value: 'logo · icon · hero image · background image · texture · project photo' }
					],
					listTitle: 'Assets',
					listItems: assetLibrary.map((asset) => ({
						label: asset.type,
						value: asset.name,
						detail: `${asset.contractorCategory} · ${asset.tags.join(' / ')}`
					})),
					actions: ['Review tags', 'Check reusable image mapping']
				},
				{
					id: 'brand-assets',
					label: 'Brand and social assets',
					value: assetLibrary
						.filter((asset) => asset.tags.includes('brand') || asset.tags.includes('social'))
						.map((asset) => asset.name)
						.join(' · '),
					listTitle: 'Brand assets',
					listItems: assetLibrary
						.filter((asset) => asset.tags.includes('brand') || asset.tags.includes('social'))
						.map((asset) => ({
							label: asset.tags.includes('social') ? 'Social' : 'Brand',
							value: asset.name,
							detail: asset.file
						}))
				}
			]
		},
		{
			id: 'service-taxonomy',
			label: 'Service taxonomy',
			description: 'Structured service categories that can be reused across sections and contractor presets.',
			previewTitle: serviceCategories.map((category) => category.name).join(' · '),
			previewBody: 'Categories now carry slug, description, icon, image, contractor type, featured state, and sort order.',
			previewMeta: `${serviceCategories.length} categories`,
			areas: [
				{
					id: 'service-categories',
					label: 'Service categories',
					value: serviceCategories.map((category) => category.slug).join(' · '),
					fields: [{ label: 'Contractor coverage', value: 'Concrete first, expandable to additional trades' }],
					listTitle: 'Categories',
					listItems: serviceCategories.map((category) => ({
						label: category.featured ? 'Featured' : 'Category',
						value: category.name,
						detail: `${category.slug} · ${category.description}`
					})),
					actions: ['Review service mix', 'Confirm icon mapping']
				},
				{
					id: 'service-assets',
					label: 'Category asset mapping',
					value: serviceCategories.map((category) => category.iconAssetKey).join(' · '),
					listTitle: 'Linked assets',
					listItems: serviceCategories.map((category) => ({
						label: category.name,
						value: getBdrAsset(content, category.iconAssetKey)?.name ?? category.iconAssetKey,
						detail: getBdrAsset(content, category.iconAssetKey)?.file ?? 'Asset not found'
					}))
				}
			]
		},
		{
			id: 'contractor-presets',
			label: 'Contractor presets',
			description: 'Reusable trade-specific defaults that can initialize the shared site template without structural rewrites.',
			previewTitle: activeContractorPreset?.label ?? 'No active preset',
			previewBody: activeContractorPreset?.defaultHeroHeadline ?? 'Preset defaults define hero messaging, services, and icons.',
			previewMeta: `${contractorPresets.length} presets`,
			areas: [
				{
					id: 'preset-library',
					label: 'Preset catalog',
					value: contractorPresets.map((preset) => preset.label).join(' · '),
					fields: [
						{ label: 'Active preset', value: activeContractorPreset?.label ?? 'No preset selected' },
						{ label: 'Shared structure', value: 'Presets swap content defaults while the site shell stays intact.' }
					],
					listTitle: 'Available presets',
					listItems: contractorPresets.map((preset) => ({
						label: preset.id === content.activeContractorPresetId ? 'Active' : preset.contractorType,
						value: preset.label,
						detail: preset.defaultHeroHeadline
					})),
					actions: ['Apply preset', 'Review trade-specific service defaults']
				},
				{
					id: 'preset-services',
					label: 'Preset service defaults',
					value: activeContractorPreset?.defaultServices.map((service) => service.name).join(' · ') ?? 'No services configured',
					listTitle: 'Default service cards',
					listItems:
						activeContractorPreset?.defaultServices.map((service) => ({
							label: service.slug,
							value: service.name,
							detail: `${service.description} · ${service.iconAssetKey}`
						})) ?? []
				}
			]
		},
		{
			id: 'trust',
			label: 'Trust',
			description: 'Credibility framing and proof points.',
			previewTitle: content.trust.title,
			previewBody: content.trust.copy,
			previewMeta: `${content.trust.points.length} trust points`,
			fields: [
				{ label: 'Eyebrow', value: content.trust.eyebrow },
				{ label: 'Title', value: content.trust.title },
				{ label: 'Body copy', value: content.trust.copy, multiline: true }
			],
			listTitle: 'Trust points',
			listItems: content.trust.points.map((point) => ({ label: 'Point', value: point })),
			actions: ['Review proof tone'],
			areas: [
				{
					id: 'trust-copy',
					label: 'Trust headline',
					value: content.trust.title,
					detail: content.trust.copy,
					fields: [
						{ label: 'Eyebrow', value: content.trust.eyebrow },
						{ label: 'Title', value: content.trust.title },
						{ label: 'Body copy', value: content.trust.copy, multiline: true }
					]
				},
				{
					id: 'trust-points',
					label: 'Trust points',
					value: content.trust.points.join(' · '),
					listTitle: 'Trust points',
					listItems: content.trust.points.map((point) => ({ label: 'Point', value: point }))
				}
			]
		},
		{
			id: 'process',
			label: 'Process',
			description: 'The dark timeline band that explains the customer journey.',
			previewTitle: content.process.title,
			previewBody: content.process.description,
			previewMeta: `${content.process.steps.length} steps`,
			areas: [
				{
					id: 'process-steps',
					label: 'Process strip',
					value: content.process.steps.map((step) => `${step.step}. ${step.title}`).join(' · '),
					fields: [
						{ label: 'Eyebrow', value: content.process.eyebrow },
						{ label: 'Heading', value: content.process.title },
						{ label: 'Description', value: content.process.description, multiline: true }
					],
					listTitle: 'Process steps',
					listItems: content.process.steps.map((step) => ({
						label: `Step ${step.step}`,
						value: step.title,
						detail: `${step.copy}${step.timeframe ? ` · ${step.timeframe}` : ''}`
					})),
					actions: ['Reorder process', 'Adjust timeframe labels']
				}
			]
		},
		{
			id: 'supporting',
			label: 'Supporting',
			description: 'Featured work and supporting narrative blocks.',
			previewTitle: content.supportingSections.map((section) => section.title).join(' · '),
			previewBody: 'Secondary modules round out the sales story.',
			previewMeta: `${content.supportingSections.reduce((sum, section) => sum + section.items.length, 0)} support items`,
			areas: [
				{
					id: 'support-blocks',
					label: 'Supporting modules',
					value: content.supportingSections.map((section) => section.title).join(' · '),
					listTitle: 'Support blocks',
					listItems: content.supportingSections.flatMap((section) =>
						section.items.map((item) => ({ label: section.title, value: item.title, detail: item.copy }))
					),
					actions: ['Add support block']
				}
			]
		},
		{
			id: 'contact',
			label: 'Contact CTA',
			description: 'Bottom conversion block and direct-response controls.',
			previewTitle: content.contact.title,
			previewBody: content.contact.body,
			previewMeta: `${content.contact.primaryCtaLabel} + ${content.contact.secondaryCtaLabel}`,
			areas: [
				{
					id: 'contact-copy',
					label: 'Contact CTA copy',
					value: content.contact.title,
					detail: content.contact.body,
					fields: [
						{ label: 'Eyebrow', value: content.contact.eyebrow },
						{ label: 'Title', value: content.contact.title },
						{ label: 'Body copy', value: content.contact.body, multiline: true }
					]
				},
				{
					id: 'contact-cta',
					label: 'Contact CTA buttons',
					value: `${content.contact.primaryCtaLabel} / ${content.contact.secondaryCtaLabel}`,
					fields: [
						{ label: 'Primary CTA', value: content.contact.primaryCtaLabel },
						{ label: 'Primary target', value: content.contact.primaryCtaHref },
						{ label: 'Secondary CTA', value: content.contact.secondaryCtaLabel },
						{ label: 'Secondary target', value: content.contact.secondaryCtaHref }
					]
				}
			]
		},
		{
			id: 'footer',
			label: 'Footer',
			description: 'Footer brand, service links, contact info, social links, and legal row.',
			previewTitle: content.footer.brandName,
			previewBody: content.footer.body,
			previewMeta: `${content.footer.navigationLinks.length + content.footer.servicesLinks.length} managed links · ${content.footer.socialLinks.length} social links`,
			areas: [
				{
					id: 'footer-brand',
					label: 'Footer brand block',
					value: content.footer.brandName,
					detail: content.footer.body,
					fields: [
						{ label: 'Eyebrow', value: content.footer.eyebrow },
						{ label: 'Logo asset', value: content.footer.logoAssetKey },
						{ label: 'Brand name', value: content.footer.brandName },
						{ label: 'Body copy', value: content.footer.body, multiline: true },
						{ label: 'Service area text', value: content.footer.serviceAreaText, multiline: true }
					]
				},
				{
					id: 'footer-links',
					label: 'Navigation and service links',
					value: [...content.footer.navigationLinks, ...content.footer.servicesLinks].map((link) => link.label).join(' · '),
					fields: [
						{ label: 'Navigation label', value: content.footer.navigationEyebrow },
						{ label: 'Services label', value: content.footer.servicesEyebrow }
					],
					listTitle: 'Managed links',
					listItems: [
						...content.footer.navigationLinks.map((link) => ({ label: 'Quick link', value: link.label, detail: link.href })),
						...content.footer.servicesLinks.map((link) => ({ label: 'Service link', value: link.label, detail: link.href }))
					]
				},
				{
					id: 'footer-contact',
					label: 'Contact and social details',
					value: [content.footer.phone, content.footer.email, content.footer.address].filter(Boolean).join(' · '),
					fields: [
						{ label: 'Contact label', value: content.footer.contactEyebrow },
						{ label: 'Phone', value: content.footer.phone || 'Not set' },
						{ label: 'Email', value: content.footer.email || 'Not set' },
						{ label: 'Address', value: content.footer.address || 'Not set' }
					],
					listTitle: 'Social links',
					listItems: content.footer.socialLinks.map((link) => ({
						label: link.platform,
						value: link.url,
						detail: link.iconAssetKey
					}))
				},
				{
					id: 'footer-utility',
					label: 'Post-footer legal row',
					value: resolveBdrCopyright(content, year),
					fields: [
						{ label: 'Copyright template', value: content.postFooter.copyright, help: 'Use {{year}} to keep the year dynamic.' }
					],
					listTitle: 'Legal links',
					listItems: content.postFooter.legalLinks.map((link) => ({ label: 'Legal link', value: link.label, detail: link.href }))
				}
			]
		}
	]);

	let selectedSectionId = $state('navigation');
	let selectedAreaId = $state('nav-announcement');

	const selectedSection = $derived(websiteSections.find((section) => section.id === selectedSectionId) ?? websiteSections[0]);
	const selectedArea = $derived(
		selectedSection.areas.find((area) => area.id === selectedAreaId) ?? selectedSection.areas[0]
	);

	$effect(() => {
		if (selectedSection && !selectedSection.areas.some((area) => area.id === selectedAreaId)) {
			selectedAreaId = selectedSection.areas[0]?.id ?? '';
		}
	});

	$effect(() => {
		serviceItems = [...content.services.items];
	});

	$effect(() => {
		if (form?.savedSectionId && form.savedSectionId !== selectedSectionId) {
			selectedSectionId = form.savedSectionId;
			selectedAreaId =
				form.savedSectionId === 'contractor-presets'
					? 'preset-library'
					: (websiteSections.find((section) => section.id === form.savedSectionId)?.areas[0]?.id ?? selectedAreaId);
		}
	});

	const metrics = $derived([
		{ label: 'Managed sections', value: String(websiteSections.length), detail: 'Navigation, sales sections, CTA, and footer are represented directly in the shell' },
		{ label: 'Editable areas', value: String(websiteSections.reduce((sum, section) => sum + section.areas.length, 0)), detail: 'Clickable areas in preview switch the lower work surface into edit mode' },
		{ label: 'Preview mode', value: 'Live scaffold', detail: 'Work area preview is rendered from the same structured content used by the public site' }
	]);
	const bobMoves = $derived.by(() => {
		return [
			{
				label: 'Polish current section',
				detail: selectedSection.previewTitle,
				href: '#content-preview'
			},
			{
				label: 'Review selected area',
				detail: selectedArea.label,
				href: '#content-editor'
			},
			{
				label: 'Check missing content',
				detail: selectedSection.actions?.join(' · ') ?? 'Review publish readiness',
				href: '#content-editor'
			}
		] satisfies BobMove[];
	});

	const selectSection = (section: WebsiteSection) => {
		selectedSectionId = section.id;
		selectedAreaId = section.id === 'services' ? 'services-items' : (section.areas[0]?.id ?? '');
	};

	const openServicesCrud = () => {
		selectedSectionId = 'services';
		selectedAreaId = 'services-items';
	};
</script>

<AdminWorkspace
	kicker="External Admin / Website"
	title="Website"
	description="Keep website sections, edit targets, and publish-readiness visible in one compact utility workflow."
	{metrics}
	focusLabel="Site sections"
>
	{#snippet focus()}
		<div class="space-y-2">
			<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
				{websiteSections.length} sections
			</p>
			{#each websiteSections as section}
				<button
					type="button"
					class={`w-full rounded-lg border px-3 py-3 text-left transition ${selectedSection.id === section.id ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
					onclick={() => selectSection(section)}
				>
					<div class="flex items-start justify-between gap-3">
						<div class="min-w-0">
							<p class="text-sm font-semibold text-[var(--text-strong)]">{section.label}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{section.previewTitle}</p>
						</div>
						{#if section.previewMeta}
							<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--text-base)]">
								{section.previewMeta}
							</span>
						{/if}
					</div>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		<div class="space-y-4">
			<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
				<div class="flex items-start justify-between gap-3">
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Bob content assist</p>
						<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{selectedSection.label}</p>
					</div>
					<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm">
						✨
					</span>
				</div>
				<div class="mt-3 grid gap-2 md:grid-cols-3">
					{#each bobMoves as move}
						<a
							href={move.href}
							class="block rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2.5 shadow-sm transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]"
						>
							<p class="text-sm font-semibold text-[var(--text-strong)]">{move.label}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{move.detail}</p>
						</a>
					{/each}
				</div>
			</div>

			<div id="content-preview" class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
				<div class="flex flex-wrap items-start justify-between gap-3">
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Section preview</p>
						<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedSection.label}</h4>
						<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{selectedSection.previewTitle}</p>
					</div>
					{#if selectedSection.previewMeta}
						<div class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">
							{selectedSection.previewMeta}
						</div>
					{/if}
				</div>

				<div class="mt-5 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-5">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Live representation</p>
					<h5 class="mt-3 text-2xl font-semibold text-[var(--text-strong)]">{selectedSection.previewTitle}</h5>
					<p class="mt-3 max-w-3xl text-sm leading-6 text-[var(--text-base)]">{selectedSection.previewBody}</p>

					<div class="mt-6 grid gap-3 lg:grid-cols-2">
						{#each selectedSection.areas as area}
							<button
								type="button"
								class={`rounded-lg border p-4 text-left transition ${selectedArea.id === area.id ? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]' : 'border-transparent bg-white/80 shadow-sm hover:bg-white'}`}
								onclick={() => {
									selectedAreaId = area.id;
									if (area.id === 'services-items') {
										openServicesCrud();
									}
								}}
							>
								<div class="flex items-start justify-between gap-3">
									<div>
										<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Editable area</p>
										<p class="mt-2 text-base font-semibold text-[var(--text-strong)]">{area.label}</p>
									</div>
									<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Edit</span>
								</div>
								<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">{area.value}</p>
								{#if area.detail}
									<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{area.detail}</p>
								{/if}
							</button>
						{/each}
					</div>
				</div>

				{#if selectedSection.id === 'navigation'}
					<article
						id="content-editor"
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-navigation-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Navigation · header CMS</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Header controls</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Manage brand assets, navigation links, CTA posture, phone CTA, sticky behavior, and the header layout from one compact editor.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">
								{content.navigation.layout}
							</span>
						</div>

						{#if form?.savedSectionId === 'navigation' && form?.savedMessage}
							<div class="mt-4 rounded-lg border border-emerald-300/60 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
								{form.savedMessage}
							</div>
						{/if}

						{#if form?.savedSectionId === 'navigation' && form?.message}
							<div class="mt-4 rounded-lg border border-rose-300/60 bg-rose-50 px-4 py-3 text-sm text-rose-700">
								{form.message}
							</div>
						{/if}

						<form method="POST" action="?/updateNavigation" class="mt-5 grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Brand and utility</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Brand name</span>
										<input name="brandName" value={content.navigation.brandName} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Announcement</span>
										<input name="announcement" value={content.navigation.announcement} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Logo asset key</span>
											<input name="logoAssetKey" value={content.navigation.logoAssetKey} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Favicon asset key</span>
											<input name="faviconAssetKey" value={content.navigation.faviconAssetKey} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
									</div>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Primary CTA label</span>
											<input name="primaryCtaLabel" value={content.navigation.primaryCtaLabel} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Primary CTA target</span>
											<input name="primaryCtaHref" value={content.navigation.primaryCtaHref} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
									</div>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Phone number</span>
											<input name="phoneNumber" value={content.navigation.phoneNumber} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Header layout</span>
											<select name="layout" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
												<option value="logo-left" selected={content.navigation.layout === 'logo-left'}>Logo left</option>
												<option value="centered" selected={content.navigation.layout === 'centered'}>Centered</option>
												<option value="right-aligned" selected={content.navigation.layout === 'right-aligned'}>Right-aligned</option>
											</select>
										</label>
									</div>
									<div class="grid gap-3 md:grid-cols-3">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Show phone button</span>
											<select name="showPhoneButton" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
												<option value="true" selected={content.navigation.showPhoneButton}>Yes</option>
												<option value="false" selected={!content.navigation.showPhoneButton}>No</option>
											</select>
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Show theme control</span>
											<select name="showThemeControl" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
												<option value="true" selected={content.navigation.showThemeControl}>Yes</option>
												<option value="false" selected={!content.navigation.showThemeControl}>No</option>
											</select>
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Sticky header</span>
											<select name="stickyHeader" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
												<option value="true" selected={content.navigation.stickyHeader}>Enabled</option>
												<option value="false" selected={!content.navigation.stickyHeader}>Disabled</option>
											</select>
										</label>
									</div>
								</div>
							</section>

							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Navigation links</p>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">One link per line using `Label|href|openInNewTab`. Reorder lines to change sort order and remove a line to delete it.</p>
								<textarea
									name="navigationLinks"
									rows="10"
									class="mt-3 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6"
								>{content.navigation.links.map((link) => `${link.label}|${link.href}|${link.openInNewTab ? 'true' : 'false'}`).join('\n')}</textarea>

								<div class="mt-4 rounded-md bg-[var(--shell-panel-strong)] p-3">
									<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Current utility posture</p>
									<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
										<li>Theme control: {content.navigation.showThemeControl ? 'shown' : 'hidden'}</li>
										<li>Phone button: {content.navigation.showPhoneButton ? content.navigation.phoneNumber : 'hidden'}</li>
										<li>Primary CTA: {content.navigation.primaryCtaLabel} → {content.navigation.primaryCtaHref}</li>
									</ul>
								</div>

								<div class="mt-4 flex justify-end">
									<button
										type="submit"
										class="rounded-lg bg-[var(--accent-text)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:brightness-95"
									>
										Save navigation
									</button>
								</div>
							</section>
						</form>
					</article>
				{:else if selectedSection.id === 'hero'}
					<article
						id="content-editor"
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-hero-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Hero · CMS controls</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Hero content and media</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Manage the first-screen message, CTA behavior, image treatment, contractor-specific media swaps, and the trust badges shown directly below the CTA row.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">
								{activeContractorPreset?.contractorType ?? 'shared'}
							</span>
						</div>

						{#if form?.savedSectionId === 'hero' && form?.savedMessage}
							<div class="mt-4 rounded-lg border border-emerald-300/60 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
								{form.savedMessage}
							</div>
						{/if}

						{#if form?.savedSectionId === 'hero' && form?.message}
							<div class="mt-4 rounded-lg border border-rose-300/60 bg-rose-50 px-4 py-3 text-sm text-rose-700">
								{form.message}
							</div>
						{/if}

						<form method="POST" action="?/updateHero" class="mt-5 grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Copy and CTA behavior</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Eyebrow</span>
										<input name="eyebrow" value={content.hero.eyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Headline</span>
										<textarea name="headline" rows="3" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.hero.headline}</textarea>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Subheadline</span>
										<textarea name="subheadline" rows="4" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.hero.subheadline}</textarea>
									</label>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Primary CTA label</span>
											<input name="primaryCtaLabel" value={content.hero.primaryCtaLabel} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Primary CTA type</span>
											<select name="primaryCtaType" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
												<option value="anchor" selected={content.hero.primaryCtaType === 'anchor'}>Anchor</option>
												<option value="link" selected={content.hero.primaryCtaType === 'link'}>Link</option>
												<option value="phone" selected={content.hero.primaryCtaType === 'phone'}>Phone</option>
											</select>
										</label>
									</div>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Primary CTA target</span>
										<input name="primaryCtaHref" value={content.hero.primaryCtaHref} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Secondary CTA label</span>
											<input name="secondaryCtaLabel" value={content.hero.secondaryCtaLabel} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Secondary CTA type</span>
											<select name="secondaryCtaType" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
												<option value="anchor" selected={content.hero.secondaryCtaType === 'anchor'}>Anchor</option>
												<option value="link" selected={content.hero.secondaryCtaType === 'link'}>Link</option>
												<option value="phone" selected={content.hero.secondaryCtaType === 'phone'}>Phone</option>
											</select>
										</label>
									</div>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Secondary CTA target</span>
										<input name="secondaryCtaHref" value={content.hero.secondaryCtaHref} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
								</div>
							</section>

							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Media and trust badge mapping</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Hero image asset</span>
										<select name="heroImageAssetKey" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
											{#each heroImageAssets as asset}
												<option value={asset.key} selected={content.hero.heroImageAssetKey === asset.key}>{asset.name} · {asset.key}</option>
											{/each}
										</select>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Hero image alt text</span>
										<input name="heroImageAltText" value={content.hero.heroImageAltText} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Background image asset</span>
										<select name="backgroundImageAssetKey" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
											<option value="" selected={!content.hero.backgroundImageAssetKey}>None</option>
											{#each heroImageAssets as asset}
												<option value={asset.key} selected={content.hero.backgroundImageAssetKey === asset.key}>{asset.name} · {asset.key}</option>
											{/each}
										</select>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Background texture / overlay asset</span>
										<select name="backgroundTextureAssetKey" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
											<option value="" selected={!content.hero.backgroundTextureAssetKey}>None</option>
											{#each textureAssets as asset}
												<option value={asset.key} selected={content.hero.backgroundTextureAssetKey === asset.key}>{asset.name} · {asset.key}</option>
											{/each}
										</select>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Trust badge eyebrow</span>
										<input name="trustBadgeEyebrow" value={content.hero.trustBadgeEyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Trust badges</span>
										<textarea
											name="trustBadges"
											rows="7"
											class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6"
										>{content.hero.trustBadges.map((badge) => `${badge.iconAssetKey}|${badge.title}|${badge.description}`).join('\n')}</textarea>
										<span class="text-xs leading-5 text-[var(--text-muted)]">One badge per line: `iconAssetKey|title|description`. Line order controls display order.</span>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Contractor-specific media overrides</span>
										<textarea
											name="mediaByContractorType"
											rows="7"
											class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6"
										>{content.hero.mediaByContractorType.map((override) => `${override.contractorType}|${override.heroImageAssetKey}|${override.backgroundImageAssetKey ?? ''}|${override.backgroundTextureAssetKey ?? ''}|${override.heroImageAltText ?? ''}`).join('\n')}</textarea>
										<span class="text-xs leading-5 text-[var(--text-muted)]">One override per line: `contractorType|heroImageAssetKey|backgroundImageAssetKey|backgroundTextureAssetKey|heroImageAltText`.</span>
									</label>
								</div>

								<div class="mt-4 grid gap-3 md:grid-cols-2">
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Hero-ready assets</p>
										<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
											{#each heroImageAssets as asset}
												<li>{asset.key} · {asset.name}</li>
											{/each}
										</ul>
									</div>
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Trust badge icons</p>
										<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
											{#each heroBadgeAssets as asset}
												<li>{asset.key} · {asset.name}</li>
											{/each}
										</ul>
									</div>
								</div>

								<div class="mt-4 flex justify-end">
									<button
										type="submit"
										class="rounded-lg bg-[var(--accent-text)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:brightness-95"
									>
										Save hero
									</button>
								</div>
							</section>
						</form>
					</article>
				{:else if selectedSection.id === 'footer'}
					<article
						id="content-editor"
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-footer-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Footer · CMS controls</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Footer content and legal row</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Manage the brand summary, quick links, service links, contact details, social icons, and copyright/legal links from one footer editor.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">
								{content.footer.socialLinks.length} social links
							</span>
						</div>

						{#if form?.savedSectionId === 'footer' && form?.savedMessage}
							<div class="mt-4 rounded-lg border border-emerald-300/60 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
								{form.savedMessage}
							</div>
						{/if}

						{#if form?.savedSectionId === 'footer' && form?.message}
							<div class="mt-4 rounded-lg border border-rose-300/60 bg-rose-50 px-4 py-3 text-sm text-rose-700">
								{form.message}
							</div>
						{/if}

						<form method="POST" action="?/updateFooter" class="mt-5 grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Brand and contact columns</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Footer eyebrow</span>
										<input name="footerEyebrow" value={content.footer.eyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Logo asset</span>
										<select name="footerLogoAssetKey" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm">
											{#each footerLogoAssets as asset}
												<option value={asset.key} selected={content.footer.logoAssetKey === asset.key}>{asset.name} · {asset.key}</option>
											{/each}
										</select>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Brand name</span>
										<input name="footerBrandName" value={content.footer.brandName} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Company description</span>
										<textarea name="footerBody" rows="4" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.footer.body}</textarea>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Service-area text</span>
										<input name="footerServiceAreaText" value={content.footer.serviceAreaText} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Phone</span>
											<input name="footerPhone" value={content.footer.phone} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Email</span>
											<input name="footerEmail" value={content.footer.email} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
									</div>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Address</span>
										<input name="footerAddress" value={content.footer.address} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Contact column label</span>
										<input name="footerContactEyebrow" value={content.footer.contactEyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
								</div>
							</section>

							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Link groups, social icons, and legal row</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Navigation group label</span>
										<input name="footerNavigationEyebrow" value={content.footer.navigationEyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Navigation links</span>
										<textarea name="footerNavigationLinks" rows="5" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.footer.navigationLinks.map((link) => `${link.label}|${link.href}|${link.openInNewTab ? 'true' : 'false'}`).join('\n')}</textarea>
										<span class="text-xs leading-5 text-[var(--text-muted)]">One per line: `Label|href|openInNewTab`.</span>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Services group label</span>
										<input name="footerServicesEyebrow" value={content.footer.servicesEyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Service links</span>
										<textarea name="footerServicesLinks" rows="5" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.footer.servicesLinks.map((link) => `${link.label}|${link.href}|${link.openInNewTab ? 'true' : 'false'}`).join('\n')}</textarea>
										<span class="text-xs leading-5 text-[var(--text-muted)]">One per line: `Label|href|openInNewTab`.</span>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Social links</span>
										<textarea name="footerSocialLinks" rows="5" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.footer.socialLinks.map((link) => `${link.platform}|${link.url}|${link.iconAssetKey}`).join('\n')}</textarea>
										<span class="text-xs leading-5 text-[var(--text-muted)]">One per line: `platform|url|iconAssetKey`.</span>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Legal group label</span>
										<input name="footerLegalEyebrow" value={content.postFooter.legalLinksEyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Legal links</span>
										<textarea name="footerLegalLinks" rows="4" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.postFooter.legalLinks.map((link) => `${link.label}|${link.href}|${link.openInNewTab ? 'true' : 'false'}`).join('\n')}</textarea>
										<span class="text-xs leading-5 text-[var(--text-muted)]">One per line: `Label|href|openInNewTab`.</span>
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Copyright text</span>
										<input name="footerCopyright" value={content.postFooter.copyright} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
								</div>

								<div class="mt-4 grid gap-3 md:grid-cols-2">
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Logo and icon assets</p>
										<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
											{#each footerLogoAssets as asset}
												<li>{asset.key} · {asset.name}</li>
											{/each}
										</ul>
									</div>
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Live copyright</p>
										<p class="mt-2 text-sm text-[var(--text-base)]">{resolveBdrCopyright(content, year)}</p>
									</div>
								</div>

								<div class="mt-4 flex justify-end">
									<button
										type="submit"
										class="rounded-lg bg-[var(--accent-text)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:brightness-95"
									>
										Save footer
									</button>
								</div>
							</section>
						</form>
					</article>
				{:else if selectedSection.id === 'process'}
					<article
						id="content-editor"
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-process-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Process · CMS controls</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Process strip and timeline steps</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Manage the section copy and the repeatable process timeline shown between services and the CTA banner.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">
								{content.process.steps.length} steps
							</span>
						</div>

						{#if form?.savedSectionId === 'process' && form?.savedMessage}
							<div class="mt-4 rounded-lg border border-emerald-300/60 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
								{form.savedMessage}
							</div>
						{/if}

						{#if form?.savedSectionId === 'process' && form?.message}
							<div class="mt-4 rounded-lg border border-rose-300/60 bg-rose-50 px-4 py-3 text-sm text-rose-700">
								{form.message}
							</div>
						{/if}

						<form method="POST" action="?/updateProcessSection" class="mt-5 grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Section copy</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Eyebrow</span>
										<input name="processEyebrow" value={content.process.eyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Heading</span>
										<input name="processTitle" value={content.process.title} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Description</span>
										<textarea name="processDescription" rows="4" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.process.description}</textarea>
									</label>
								</div>
							</section>

							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Timeline steps</p>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">One step per line using `stepNumber|title|description|iconAssetKey|timeframe`.</p>
								<textarea
									name="processSteps"
									rows="10"
									class="mt-3 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6"
								>{content.process.steps.map((step) => `${step.step}|${step.title}|${step.copy}|${step.iconAssetKey}|${step.timeframe ?? ''}`).join('\n')}</textarea>

								<div class="mt-4 grid gap-3 md:grid-cols-2">
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Available icons</p>
										<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
											{#each assetLibrary.filter((asset) => asset.type === 'icon') as asset}
												<li>{asset.key} · {asset.name}</li>
											{/each}
										</ul>
									</div>
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Timeline guidance</p>
										<p class="mt-2 text-sm text-[var(--text-base)]">Keep this between 3 and 5 steps so the desktop timeline stays balanced and mobile stacking stays readable.</p>
									</div>
								</div>

								<div class="mt-4 flex justify-end">
									<button
										type="submit"
										class="rounded-lg bg-[var(--accent-text)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:brightness-95"
									>
										Save process
									</button>
								</div>
							</section>
						</form>
					</article>
				{:else if selectedSection.id === 'contractor-presets'}
					<article
						id="content-editor"
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-contractor-preset-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Contractor presets · apply surface</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Preset launcher</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Initialize the shared site template with trade-specific defaults for hero messaging, featured services, and icon mapping.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">
								{activeContractorPreset?.label ?? 'No active preset'}
							</span>
						</div>

						{#if form?.savedMessage}
							<div class="mt-4 rounded-lg border border-emerald-300/60 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
								{form.savedMessage}
							</div>
						{/if}

						{#if form?.message}
							<div class="mt-4 rounded-lg border border-rose-300/60 bg-rose-50 px-4 py-3 text-sm text-rose-700">
								{form.message}
							</div>
						{/if}

						<div class="mt-5 grid gap-4 xl:grid-cols-2">
							{#each contractorPresets as preset}
								<form method="POST" action="?/applyContractorPreset" class="rounded-lg bg-white/80 p-4 shadow-sm">
									<input type="hidden" name="presetId" value={preset.id} />
									<div class="flex items-start justify-between gap-3">
										<div>
											<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{preset.contractorType}</p>
											<h5 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">{preset.label}</h5>
										</div>
										<span class={`rounded-full px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.16em] ${preset.id === content.activeContractorPresetId ? 'bg-[#fff4ea] text-[var(--accent-text)] ring-1 ring-[rgba(249,115,22,0.32)]' : 'bg-[var(--shell-panel-strong)] text-[var(--text-muted)]'}`}>
											{preset.id === content.activeContractorPresetId ? 'Active' : 'Ready'}
										</span>
									</div>
									<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">{preset.defaultHeroHeadline}</p>
									<div class="mt-4 grid gap-3 md:grid-cols-2">
										<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
											<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Default services</p>
											<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
												{#each preset.defaultServices as service}
													<li>{service.name}</li>
												{/each}
											</ul>
										</div>
										<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
											<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Default icons</p>
											<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
												{#each preset.defaultIconAssetKeys as iconKey}
													<li>{getBdrAsset(content, iconKey)?.name ?? iconKey}</li>
												{/each}
											</ul>
										</div>
									</div>
									<div class="mt-4 flex justify-end">
										<button
											type="submit"
											class="rounded-lg bg-[var(--accent-text)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:brightness-95"
										>
											Apply preset
										</button>
									</div>
								</form>
							{/each}
						</div>
					</article>
				{:else if selectedSection.id === 'services'}
					<article
						id="content-editor"
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-service-crud-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Services · CMS controls</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Services section and card grid</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Manage the section copy, CTA, and the flexible services card grid from one compact editor.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">
								{serviceCategories.length || serviceItems.length} cards
							</span>
						</div>

						{#if form?.savedSectionId === 'services' && form?.savedMessage}
							<div class="mt-4 rounded-lg border border-emerald-300/60 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
								{form.savedMessage}
							</div>
						{/if}

						{#if form?.savedSectionId === 'services' && form?.message}
							<div class="mt-4 rounded-lg border border-rose-300/60 bg-rose-50 px-4 py-3 text-sm text-rose-700">
								{form.message}
							</div>
						{/if}

						<form method="POST" action="?/updateServicesSection" class="mt-5 grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Section copy and CTA</p>
								<div class="mt-3 grid gap-3">
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Eyebrow</span>
										<input name="servicesEyebrow" value={content.services.eyebrow} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Heading</span>
										<input name="servicesTitle" value={content.services.title} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
									</label>
									<label class="grid gap-1 text-sm text-[var(--text-base)]">
										<span class="font-semibold text-[var(--text-strong)]">Description</span>
										<textarea name="servicesCopy" rows="4" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6">{content.services.copy}</textarea>
									</label>
									<div class="grid gap-3 md:grid-cols-2">
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Section CTA label</span>
											<input name="servicesCtaLabel" value={content.services.ctaLabel} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
										<label class="grid gap-1 text-sm text-[var(--text-base)]">
											<span class="font-semibold text-[var(--text-strong)]">Section CTA target</span>
											<input name="servicesCtaHref" value={content.services.ctaHref} class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm" />
										</label>
									</div>
								</div>
							</section>

							<section class="rounded-lg bg-white/80 p-4 shadow-sm">
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Service cards</p>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">One card per line using `name|slug|description|iconAssetKey|imageAssetKey|detailPageUrl|featured|sortOrder`.</p>
								<textarea
									name="serviceCards"
									rows="12"
									class="mt-3 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6"
								>{(serviceCategories.length
									? serviceCategories
									: serviceItems.map((item, index) => ({
											name: item,
											slug: item.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''),
											description: '',
											iconAssetKey: 'service-driveways-icon',
											imageAssetKey: '',
											detailPageUrl: '',
											featured: true,
											sortOrder: index + 1
										}))
									).map((card) => `${card.name}|${card.slug}|${card.description}|${card.iconAssetKey}|${card.imageAssetKey ?? ''}|${card.detailPageUrl ?? ''}|${card.featured ? 'true' : 'false'}|${card.sortOrder}`).join('\n')}</textarea>

								<div class="mt-4 grid gap-3 md:grid-cols-2">
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Available icon / image assets</p>
										<ul class="mt-2 space-y-1.5 text-sm text-[var(--text-base)]">
											{#each assetLibrary.filter((asset) => asset.type === 'icon' || asset.type === 'hero-image' || asset.type === 'background-image' || asset.type === 'project-photo') as asset}
												<li>{asset.key} · {asset.name}</li>
											{/each}
										</ul>
									</div>
									<div class="rounded-md bg-[var(--shell-panel-strong)] p-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Current card count</p>
										<p class="mt-2 text-sm text-[var(--text-base)]">{serviceCategories.length || serviceItems.length} cards configured. Keep this between 3 and 8.</p>
									</div>
								</div>

								<div class="mt-4 flex justify-end">
									<button
										type="submit"
										class="rounded-lg bg-[var(--accent-text)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:brightness-95"
									>
										Save services
									</button>
								</div>
							</section>
						</form>
					</article>
				{:else}
					<div id="content-editor">
						<ContentEditorCard
							eyebrow={`${selectedSection.label} · Editing surface`}
							title={selectedArea.label}
							description={selectedArea.detail ?? `Editing controls for the ${selectedArea.label.toLowerCase()} area.`}
							fields={selectedArea.fields ?? selectedSection.fields ?? []}
							listTitle={selectedArea.listTitle ?? selectedSection.listTitle}
							listItems={selectedArea.listItems ?? selectedSection.listItems ?? []}
							actions={selectedArea.actions ?? selectedSection.actions ?? []}
						/>
					</div>
				{/if}
			</div>
		</div>
	{/snippet}
</AdminWorkspace>
