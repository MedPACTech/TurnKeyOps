<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import ContentEditorCard, {
		type EditableField,
		type EditableListItem
	} from '$lib/components/admin/ContentEditorCard.svelte';
	import { resolveBdrCopyright } from '$lib/bdr-site-content';
	import { ArrowDown, ArrowUp, Plus, Save, Trash2 } from 'lucide-svelte';
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

	let { data }: PageProps = $props();

	const year = new Date().getFullYear();

	let serviceItems = $state(untrack(() => [...data.content.services.items]));
	let selectedServiceIndex = $state(0);
	let serviceDraft = $state(untrack(() => data.content.services.items[0] ?? ''));
	let serviceError = $state('');
	let serviceStatus = $state('Select a service to edit it.');
	let serviceIsSaving = $state(false);

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
			previewMeta: `${serviceItems.length} service bullets`,
			fields: [
				{ label: 'Eyebrow', value: data.content.services.eyebrow },
				{ label: 'Title', value: data.content.services.title },
				{ label: 'Body copy', value: data.content.services.copy, multiline: true }
			],
			listTitle: 'Service bullets',
			listItems: serviceItems.map((item) => ({ label: 'Service', value: item })),
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
					value: serviceItems.join(' · '),
					listTitle: 'Service bullets',
					listItems: serviceItems.map((item) => ({ label: 'Service', value: item })),
					actions: ['Add service', 'Edit selected', 'Delete selected', 'Reorder services']
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
	const isServicesCrudSelected = $derived(selectedSection.id === 'services' && selectedArea.id === 'services-items');
	const isCreatingService = $derived(selectedServiceIndex >= serviceItems.length);
	const selectedServiceLabel = $derived(isCreatingService ? 'New service' : (serviceItems[selectedServiceIndex] ?? 'No service selected'));

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

	const selectSection = (section: WebsiteSection) => {
		selectedSectionId = section.id;
		selectedAreaId = section.id === 'services' ? 'services-items' : (section.areas[0]?.id ?? '');
	};

	const openServicesCrud = () => {
		selectedSectionId = 'services';
		selectedAreaId = 'services-items';
	};

	const selectService = (index: number) => {
		openServicesCrud();
		selectedServiceIndex = index;
		serviceDraft = serviceItems[index] ?? '';
		serviceError = '';
		serviceStatus = `Editing service ${index + 1}.`;
	};

	const startNewService = () => {
		openServicesCrud();
		selectedServiceIndex = serviceItems.length;
		serviceDraft = '';
		serviceError = '';
		serviceStatus = 'New service draft ready.';
	};

	const persistServices = async (next: string[], successStatus: string, previous: string[]) => {
		const formData = new FormData();
		next.forEach((service) => formData.append('services', service));

		serviceIsSaving = true;

		try {
			const response = await fetch('?/updateServices', {
				method: 'POST',
				body: formData
			});

			if (!response.ok) {
				throw new Error(`Services save failed with ${response.status}`);
			}

			serviceStatus = successStatus;
			serviceError = '';
			return true;
		} catch (cause) {
			console.error('Unable to persist service changes.', cause);
			serviceItems = previous;
			selectedServiceIndex = Math.max(0, Math.min(selectedServiceIndex, serviceItems.length - 1));
			serviceDraft = serviceItems[selectedServiceIndex] ?? '';
			serviceError = 'Services could not be saved. Please try again.';
			serviceStatus = 'The last change was not saved.';
			return false;
		} finally {
			serviceIsSaving = false;
		}
	};

	const saveService = async () => {
		const value = serviceDraft.trim();

		if (!value) {
			serviceError = 'Service name is required.';
			serviceStatus = 'Add a service name before saving.';
			return;
		}

		const previous = [...serviceItems];

		if (isCreatingService) {
			const next = [...serviceItems, value];
			serviceItems = next;
			selectedServiceIndex = next.length - 1;
			serviceDraft = value;
			serviceError = '';
			serviceStatus = `Saving "${value}"...`;
			await persistServices(next, `Added "${value}".`, previous);
			return;
		}

		const next = [...serviceItems];
		next[selectedServiceIndex] = value;
		serviceItems = next;
		serviceDraft = value;
		serviceError = '';
		serviceStatus = `Saving "${value}"...`;
		await persistServices(next, `Updated "${value}".`, previous);
	};

	const deleteSelectedService = async () => {
		if (isCreatingService || serviceItems.length === 0) {
			serviceDraft = '';
			serviceError = '';
			serviceStatus = 'No selected service to delete.';
			return;
		}

		const previous = [...serviceItems];
		const removed = serviceItems[selectedServiceIndex];
		const next = serviceItems.filter((_, index) => index !== selectedServiceIndex);
		serviceItems = next;

		const nextIndex = Math.max(0, Math.min(selectedServiceIndex, next.length - 1));
		selectedServiceIndex = nextIndex;
		serviceDraft = next[nextIndex] ?? '';
		serviceError = '';
		serviceStatus = `Saving deletion of "${removed}"...`;
		await persistServices(
			next,
			next.length ? `Deleted "${removed}".` : 'No services remain. Add a service to rebuild the list.',
			previous
		);
	};

	const moveService = async (index: number, direction: -1 | 1) => {
		const targetIndex = index + direction;

		if (targetIndex < 0 || targetIndex >= serviceItems.length) {
			return;
		}

		const previous = [...serviceItems];
		const next = [...serviceItems];
		[next[index], next[targetIndex]] = [next[targetIndex], next[index]];
		serviceItems = next;
		selectedServiceIndex = targetIndex;
		serviceDraft = next[targetIndex];
		serviceError = '';
		serviceStatus = `Saving service order...`;
		await persistServices(next, `Moved service to position ${targetIndex + 1}.`, previous);
	};
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
					onclick={() => selectSection(section)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{section.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{section.description}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		<div class="space-y-4">
			<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
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

				<div class="mt-5 rounded-lg border border-[var(--shell-border)] bg-[linear-gradient(180deg,rgba(15,23,42,0.02),rgba(15,23,42,0.06))] p-5">
					<div class="rounded-lg border border-[var(--shell-border)] bg-white/80 p-5 shadow-[0_16px_40px_rgba(15,23,42,0.08)]">
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-slate-500">Live representation</p>
						<h5 class="mt-3 text-2xl font-semibold text-slate-900">{selectedSection.previewTitle}</h5>
						<p class="mt-3 max-w-3xl text-sm leading-6 text-slate-600">{selectedSection.previewBody}</p>

						<div class="mt-6 grid gap-3 lg:grid-cols-2">
							{#each selectedSection.areas as area}
									<button
										type="button"
										class={`rounded-lg border p-4 text-left transition ${selectedArea.id === area.id ? 'border-indigo-300 bg-indigo-50 shadow-[0_12px_24px_rgba(64,80,230,0.12)]' : 'border-slate-200 bg-white hover:border-indigo-200 hover:bg-indigo-50/40'}`}
										onclick={() => {
											selectedAreaId = area.id;
											if (area.id === 'services-items') {
												openServicesCrud();
											}
										}}
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

				{#if isServicesCrudSelected}
					<article
						class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]"
						data-testid="cms-service-crud-panel"
					>
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Services · CRUD surface</p>
								<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">Service catalog</h4>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Select a customer-facing service, edit its label, add a new service, delete stale entries, or reorder the public services list.</p>
							</div>
							<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">Interactive CRUD</span>
						</div>

						<div class="mt-5 grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
							<section class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
								<div class="flex flex-wrap items-center justify-between gap-3">
									<div>
										<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Managed services</p>
										<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{serviceItems.length} services on the customer site.</p>
									</div>
									<button
										type="button"
										class="inline-flex items-center gap-2 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-text)] disabled:cursor-not-allowed disabled:opacity-50"
										disabled={serviceIsSaving}
										onclick={startNewService}
									>
										<Plus size={15} />
										New
									</button>
								</div>

								<div class="mt-3 grid gap-2">
									{#if serviceItems.length === 0}
										<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4 text-sm leading-6 text-[var(--text-muted)]">
											No services are currently listed. Use New to create the first public service.
										</div>
									{/if}

									{#each serviceItems as service, index}
										<div
											class={`rounded-md border p-3 transition ${selectedServiceIndex === index ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel-strong)]'}`}
											data-testid={`cms-service-row-${index}`}
										>
											<div class="flex items-start justify-between gap-3">
												<button
													type="button"
													class="min-w-0 flex-1 text-left"
													aria-pressed={selectedServiceIndex === index}
													onclick={() => selectService(index)}
												>
													<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Service {index + 1}</p>
													<p class="mt-2 text-sm font-medium text-[var(--text-strong)]">{service}</p>
												</button>
												<div class="flex shrink-0 gap-1">
													<button
														type="button"
														class="inline-flex h-8 w-8 items-center justify-center rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] text-[var(--text-base)] disabled:cursor-not-allowed disabled:opacity-40"
														aria-label={`Move ${service} up`}
														disabled={serviceIsSaving || index === 0}
														onclick={() => moveService(index, -1)}
													>
														<ArrowUp size={14} />
													</button>
													<button
														type="button"
														class="inline-flex h-8 w-8 items-center justify-center rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] text-[var(--text-base)] disabled:cursor-not-allowed disabled:opacity-40"
														aria-label={`Move ${service} down`}
														disabled={serviceIsSaving || index === serviceItems.length - 1}
														onclick={() => moveService(index, 1)}
													>
														<ArrowDown size={14} />
													</button>
												</div>
											</div>
										</div>
									{/each}
								</div>
							</section>

							<section class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
								<div class="flex flex-wrap items-start justify-between gap-3">
									<div>
										<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Selected service</p>
										<h5 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">{selectedServiceLabel}</h5>
									</div>
									<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">
										{isCreatingService ? 'Create' : 'Update'}
									</span>
								</div>

								<label class="mt-4 block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Service name</span>
									<input
										class="mt-2 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm text-[var(--text-base)] outline-none focus:border-[var(--accent-border)]"
										bind:value={serviceDraft}
										disabled={serviceIsSaving}
										placeholder="Example: Roof maintenance program"
									/>
									{#if serviceError}
										<p class="mt-2 text-xs leading-5 text-rose-600">{serviceError}</p>
									{/if}
								</label>

								<div class="mt-4 flex flex-wrap gap-2">
									<button
										type="button"
										class="inline-flex items-center gap-2 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-text)] disabled:cursor-not-allowed disabled:opacity-50"
										disabled={serviceIsSaving}
										onclick={saveService}
									>
										<Save size={15} />
										{isCreatingService ? 'Create service' : 'Update service'}
									</button>
									<button
										type="button"
										class="inline-flex items-center gap-2 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-base)] disabled:cursor-not-allowed disabled:opacity-50"
										disabled={serviceIsSaving}
										onclick={startNewService}
									>
										<Plus size={15} />
										New draft
									</button>
									<button
										type="button"
										class="inline-flex items-center gap-2 rounded-md border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-rose-700 disabled:cursor-not-allowed disabled:opacity-50"
										disabled={serviceIsSaving || isCreatingService || serviceItems.length === 0}
										onclick={deleteSelectedService}
									>
										<Trash2 size={15} />
										Delete
									</button>
								</div>

								<p class="mt-4 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2 text-xs leading-5 text-[var(--text-muted)]" aria-live="polite">
									{serviceStatus}
								</p>

								<div class="mt-4 rounded-md border border-[var(--shell-border)] bg-white/70 p-3">
									<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-slate-500">Public services preview</p>
									<div class="mt-3 grid gap-2">
										{#each serviceItems as service, index}
											<div class="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700">
												<span class="font-semibold text-slate-900">{index + 1}.</span> {service}
											</div>
										{/each}
									</div>
								</div>
							</section>
						</div>
					</article>
				{:else}
					<ContentEditorCard
						eyebrow={`${selectedSection.label} · Editing surface`}
						title={selectedArea.label}
						description={selectedArea.detail ?? `Editing controls for the ${selectedArea.label.toLowerCase()} area.`}
						fields={selectedArea.fields ?? selectedSection.fields ?? []}
						listTitle={selectedArea.listTitle ?? selectedSection.listTitle}
						listItems={selectedArea.listItems ?? selectedSection.listItems ?? []}
						actions={selectedArea.actions ?? selectedSection.actions ?? []}
					/>
				{/if}
			</div>
		{/snippet}
	</AdminWorkspace>
