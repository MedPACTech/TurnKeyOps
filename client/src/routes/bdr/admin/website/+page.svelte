<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import ContentEditorCard, {
		type EditableField,
		type EditableListItem
	} from '$lib/components/admin/ContentEditorCard.svelte';
	import { resolveBdrCopyright } from '$lib/bdr-site-content';
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

	let { data }: PageProps = $props();

	const year = new Date().getFullYear();

	const websiteSections = $derived<WebsiteSection[]>([
		{
			id: 'navigation',
			label: 'Navigation',
			description: 'Top-level wayfinding and utility announcement copy.',
			previewTitle: data.content.navigation.brandName,
			previewBody: data.content.navigation.announcement,
			previewMeta: `${data.content.navigation.links.length} links`,
			fields: [
				{ label: 'Announcement', value: data.content.navigation.announcement },
				{ label: 'Brand label', value: data.content.navigation.brandName }
			],
			listTitle: 'Navigation links',
			listItems: data.content.navigation.links.map((link) => ({ label: 'Link', value: link.label, detail: link.href })),
			actions: ['Preview anchor flow', 'Reorder nav'],
			areas: [
				{
					id: 'nav-announcement',
					label: 'Announcement strip',
					value: data.content.navigation.announcement,
					fields: [{ label: 'Announcement', value: data.content.navigation.announcement }]
				},
				{
					id: 'nav-links',
					label: 'Primary navigation',
					value: data.content.navigation.links.map((link) => link.label).join(' · '),
					listTitle: 'Navigation links',
					listItems: data.content.navigation.links.map((link) => ({ label: 'Link', value: link.label, detail: link.href }))
				}
			]
		},
		{
			id: 'hero',
			label: 'Hero',
			description: 'The first-screen sales message and CTA pair.',
			previewTitle: data.content.hero.headline,
			previewBody: data.content.hero.body,
			previewMeta: `${data.content.hero.primaryCtaLabel} + ${data.content.hero.secondaryCtaLabel}`,
			fields: [
				{ label: 'Eyebrow', value: data.content.hero.eyebrow },
				{ label: 'Headline', value: data.content.hero.headline, multiline: true },
				{ label: 'Body copy', value: data.content.hero.body, multiline: true }
			],
			actions: ['Preview hero', 'Create seasonal variant'],
			areas: [
				{
					id: 'hero-copy',
					label: 'Hero copy',
					value: data.content.hero.headline,
					detail: data.content.hero.body,
					fields: [
						{ label: 'Eyebrow', value: data.content.hero.eyebrow },
						{ label: 'Headline', value: data.content.hero.headline, multiline: true },
						{ label: 'Body copy', value: data.content.hero.body, multiline: true }
					]
				},
				{
					id: 'hero-cta',
					label: 'Hero CTAs',
					value: `${data.content.hero.primaryCtaLabel} / ${data.content.hero.secondaryCtaLabel}`,
					fields: [
						{ label: 'Primary CTA', value: data.content.hero.primaryCtaLabel },
						{ label: 'Primary target', value: data.content.hero.primaryCtaHref },
						{ label: 'Secondary CTA', value: data.content.hero.secondaryCtaLabel },
						{ label: 'Secondary target', value: data.content.hero.secondaryCtaHref }
					]
				}
			]
		},
		{
			id: 'services',
			label: 'Services',
			description: 'The demand-facing summary of service lines.',
			previewTitle: data.content.services.title,
			previewBody: data.content.services.copy,
			previewMeta: `${data.content.services.items.length} service bullets`,
			fields: [
				{ label: 'Eyebrow', value: data.content.services.eyebrow },
				{ label: 'Title', value: data.content.services.title },
				{ label: 'Body copy', value: data.content.services.copy, multiline: true }
			],
			listTitle: 'Service bullets',
			listItems: data.content.services.items.map((item) => ({ label: 'Service', value: item })),
			actions: ['Add service', 'Reorder services'],
			areas: [
				{
					id: 'services-copy',
					label: 'Services intro',
					value: data.content.services.title,
					detail: data.content.services.copy,
					fields: [
						{ label: 'Eyebrow', value: data.content.services.eyebrow },
						{ label: 'Title', value: data.content.services.title },
						{ label: 'Body copy', value: data.content.services.copy, multiline: true }
					]
				},
				{
					id: 'services-items',
					label: 'Service bullets',
					value: data.content.services.items.join(' · '),
					listTitle: 'Service bullets',
					listItems: data.content.services.items.map((item) => ({ label: 'Service', value: item })),
					actions: ['Reorder bullets']
				}
			]
		},
		{
			id: 'trust',
			label: 'Trust',
			description: 'Credibility framing and proof points.',
			previewTitle: data.content.trust.title,
			previewBody: data.content.trust.copy,
			previewMeta: `${data.content.trust.points.length} trust points`,
			fields: [
				{ label: 'Eyebrow', value: data.content.trust.eyebrow },
				{ label: 'Title', value: data.content.trust.title },
				{ label: 'Body copy', value: data.content.trust.copy, multiline: true }
			],
			listTitle: 'Trust points',
			listItems: data.content.trust.points.map((point) => ({ label: 'Point', value: point })),
			actions: ['Review proof tone'],
			areas: [
				{
					id: 'trust-copy',
					label: 'Trust headline',
					value: data.content.trust.title,
					detail: data.content.trust.copy,
					fields: [
						{ label: 'Eyebrow', value: data.content.trust.eyebrow },
						{ label: 'Title', value: data.content.trust.title },
						{ label: 'Body copy', value: data.content.trust.copy, multiline: true }
					]
				},
				{
					id: 'trust-points',
					label: 'Trust points',
					value: data.content.trust.points.join(' · '),
					listTitle: 'Trust points',
					listItems: data.content.trust.points.map((point) => ({ label: 'Point', value: point }))
				}
			]
		},
		{
			id: 'process',
			label: 'Process',
			description: 'How the public site explains the quote-to-schedule journey.',
			previewTitle: data.content.process.steps.map((step) => step.title).join(' → '),
			previewBody: 'Three steps explain request, inspection, and approval.',
			previewMeta: `${data.content.process.steps.length} steps`,
			areas: [
				{
					id: 'process-steps',
					label: 'Process steps',
					value: data.content.process.steps.map((step) => `${step.step}. ${step.title}`).join(' · '),
					fields: [{ label: 'Eyebrow', value: data.content.process.eyebrow }],
					listTitle: 'Process steps',
					listItems: data.content.process.steps.map((step) => ({ label: `Step ${step.step}`, value: step.title, detail: step.copy })),
					actions: ['Reorder process']
				}
			]
		},
		{
			id: 'supporting',
			label: 'Supporting',
			description: 'Featured work and supporting narrative blocks.',
			previewTitle: data.content.supportingSections.map((section) => section.title).join(' · '),
			previewBody: 'Secondary modules round out the sales story.',
			previewMeta: `${data.content.supportingSections.reduce((sum, section) => sum + section.items.length, 0)} support items`,
			areas: [
				{
					id: 'support-blocks',
					label: 'Supporting modules',
					value: data.content.supportingSections.map((section) => section.title).join(' · '),
					listTitle: 'Support blocks',
					listItems: data.content.supportingSections.flatMap((section) =>
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
			previewTitle: data.content.contact.title,
			previewBody: data.content.contact.body,
			previewMeta: `${data.content.contact.primaryCtaLabel} + ${data.content.contact.secondaryCtaLabel}`,
			areas: [
				{
					id: 'contact-copy',
					label: 'Contact CTA copy',
					value: data.content.contact.title,
					detail: data.content.contact.body,
					fields: [
						{ label: 'Eyebrow', value: data.content.contact.eyebrow },
						{ label: 'Title', value: data.content.contact.title },
						{ label: 'Body copy', value: data.content.contact.body, multiline: true }
					]
				},
				{
					id: 'contact-cta',
					label: 'Contact CTA buttons',
					value: `${data.content.contact.primaryCtaLabel} / ${data.content.contact.secondaryCtaLabel}`,
					fields: [
						{ label: 'Primary CTA', value: data.content.contact.primaryCtaLabel },
						{ label: 'Primary target', value: data.content.contact.primaryCtaHref },
						{ label: 'Secondary CTA', value: data.content.contact.secondaryCtaLabel },
						{ label: 'Secondary target', value: data.content.contact.secondaryCtaHref }
					]
				}
			]
		},
		{
			id: 'footer',
			label: 'Footer',
			description: 'Footer brand summary, managed links, and utility strip.',
			previewTitle: data.content.footer.brandName,
			previewBody: data.content.footer.body,
			previewMeta: `${data.content.footer.links.length} footer links`,
			areas: [
				{
					id: 'footer-brand',
					label: 'Footer brand block',
					value: data.content.footer.brandName,
					detail: data.content.footer.body,
					fields: [
						{ label: 'Eyebrow', value: data.content.footer.eyebrow },
						{ label: 'Brand name', value: data.content.footer.brandName },
						{ label: 'Body copy', value: data.content.footer.body, multiline: true }
					]
				},
				{
					id: 'footer-links',
					label: 'Footer links',
					value: data.content.footer.links.map((link) => link.label).join(' · '),
					fields: [{ label: 'Links label', value: data.content.footer.linksEyebrow }],
					listTitle: 'Footer links',
					listItems: data.content.footer.links.map((link) => ({ label: 'Footer link', value: link.label, detail: link.href }))
				},
				{
					id: 'footer-utility',
					label: 'Post-footer utility',
					value: resolveBdrCopyright(year),
					fields: [
						{ label: 'Copyright template', value: data.content.postFooter.copyright, help: 'Use {{year}} to keep the year dynamic.' }
					],
					listTitle: 'Utility links',
					listItems: data.content.postFooter.utilityLinks.map((link) => ({ label: 'Utility link', value: link.label, detail: link.href }))
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

	const metrics = $derived([
		{ label: 'Managed sections', value: String(websiteSections.length), detail: 'Navigation, sales sections, CTA, and footer are represented directly in the shell' },
		{ label: 'Editable areas', value: String(websiteSections.reduce((sum, section) => sum + section.areas.length, 0)), detail: 'Clickable areas in preview switch the lower work surface into edit mode' },
		{ label: 'Preview mode', value: 'Live scaffold', detail: 'Work area preview is rendered from the same structured content used by the public site' }
	]);
</script>

<AdminWorkspace
	kicker="Website"
	title="Public site sections in a section-first editing workspace"
	description="Website uses the 4-part admin pattern with no context rail. The focus rail represents public site sections, the upper work area shows the current section preview, and clicking an editable area opens the lower editing surface."
	{metrics}
	focusLabel="Site sections"
>
	{#snippet focus()}
		<div class="space-y-2">
			{#each websiteSections as section}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedSection.id === section.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedSectionId = section.id)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{section.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{section.description}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		<div class="space-y-4">
			<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
				<div class="flex flex-wrap items-start justify-between gap-3">
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Section preview</p>
						<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedSection.label}</h4>
						<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{selectedSection.description}</p>
					</div>
					{#if selectedSection.previewMeta}
						<div class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">
							{selectedSection.previewMeta}
						</div>
					{/if}
				</div>

				<div class="mt-5 rounded-2xl border border-[var(--shell-border)] bg-[linear-gradient(180deg,rgba(15,23,42,0.02),rgba(15,23,42,0.06))] p-5">
					<div class="rounded-2xl border border-[var(--shell-border)] bg-white/80 p-5 shadow-[0_16px_40px_rgba(15,23,42,0.08)]">
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-slate-500">Live representation</p>
						<h5 class="mt-3 text-2xl font-semibold text-slate-900">{selectedSection.previewTitle}</h5>
						<p class="mt-3 max-w-3xl text-sm leading-6 text-slate-600">{selectedSection.previewBody}</p>

						<div class="mt-6 grid gap-3 lg:grid-cols-2">
							{#each selectedSection.areas as area}
								<button
									type="button"
									class={`rounded-xl border p-4 text-left transition ${selectedArea.id === area.id ? 'border-orange-300 bg-orange-50 shadow-[0_12px_24px_rgba(234,88,12,0.12)]' : 'border-slate-200 bg-white hover:border-orange-200 hover:bg-orange-50/40'}`}
									onclick={() => (selectedAreaId = area.id)}
								>
									<div class="flex items-start justify-between gap-3">
										<div>
											<p class="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Editable area</p>
											<p class="mt-2 text-base font-semibold text-slate-900">{area.label}</p>
										</div>
										<span class="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-slate-500">Edit</span>
									</div>
									<p class="mt-3 text-sm leading-6 text-slate-700">{area.value}</p>
									{#if area.detail}
										<p class="mt-2 text-sm leading-6 text-slate-500">{area.detail}</p>
									{/if}
								</button>
							{/each}
						</div>
					</div>
				</div>
			</div>

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
	{/snippet}
</AdminWorkspace>
