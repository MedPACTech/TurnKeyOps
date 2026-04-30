<script lang="ts">
	import { getBdrAsset, getBdrServiceCategories, resolveBdrCopyright } from '$lib/bdr-site-content';
	import SectionCard from '$lib/components/SectionCard.svelte';
	import { fallbackMvpSnapshot } from '$lib/mvp-data';
	import { buildPublicProof } from '$lib/mvp-display';
	import type { PageData } from './$types';

	let { data, form }: { data: PageData; form: any } = $props();

	const content = $derived(data.content);
	const year = new Date().getFullYear();
	const logoAsset = $derived(getBdrAsset(content, 'bdr-crest-logo'));
	const serviceCategories = $derived(getBdrServiceCategories(content));
	const themeSettings = $derived(content.themeSettings);
	const publicThemeStyle = $derived(
		[
			`--bdr-primary:${themeSettings.colors.primary}`,
			`--bdr-secondary:${themeSettings.colors.secondary}`,
			`--bdr-accent:${themeSettings.colors.accent}`,
			`--bdr-background:${themeSettings.colors.background}`,
			`--bdr-surface:${themeSettings.colors.surface}`,
			`--bdr-text:${themeSettings.colors.text}`,
			`--bdr-border:${themeSettings.colors.border}`,
			`--bdr-button-radius:${themeSettings.sizing.buttonRadius}`,
			`--bdr-card-radius:${themeSettings.sizing.cardRadius}`,
			`--bdr-heading-font:${themeSettings.typography.headingFont}, Inter, sans-serif`,
			`--bdr-body-font:${themeSettings.typography.bodyFont}, Inter, sans-serif`
		].join(';')
	);
	const priorityOptions = [
		{ value: 'standard', label: 'Standard project' },
		{ value: 'priority', label: 'Priority quote' },
		{ value: 'emergency', label: 'Storm / leak emergency' }
	] as const;
	const serviceTypeOptions = $derived(
		serviceCategories.length ? serviceCategories.map((category) => category.name) : content.services.items
	);
	const fieldClass =
		'rounded-2xl border border-white/10 bg-black/35 px-4 py-3 text-sm text-white outline-none transition placeholder:text-slate-500 focus:border-orange-300/50 focus:bg-black/50';
</script>

<svelte:head>
	<title>BDR Construction</title>
	<meta name="description" content="BDR Construction public site for roofing, exterior work, and fast estimate requests." />
</svelte:head>

<div
	class="min-h-screen bg-[radial-gradient(circle_at_top,_rgba(249,115,22,0.16),_transparent_24%),linear-gradient(180deg,_#050505_0%,_#101010_46%,_#181818_100%)] text-white"
	data-theme={themeSettings.mode.toLowerCase()}
	data-style-preset={themeSettings.preset.toLowerCase()}
	style={`color: var(--bdr-text); background-color: var(--bdr-background); font-family: var(--bdr-body-font); ${publicThemeStyle}`}
>
	<div class="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
		<nav id="top" class="sticky top-4 z-30 rounded-xl border border-white/10 bg-slate-950/90 px-4 py-3 shadow-[0_20px_60px_rgba(15,23,42,0.32)] backdrop-blur">
			<div class="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
				<div class="flex items-center gap-3">
					<div class="flex h-11 w-11 items-center justify-center overflow-hidden rounded-xl border border-orange-300/25 bg-white p-2">
						<img src={logoAsset?.file ?? '/clientFiles/BDRLogo.jpeg'} alt={logoAsset?.altText ?? 'BDR Construction logo'} class="h-full w-full object-contain" />
					</div>
					<div>
						<p class="text-[0.68rem] uppercase tracking-[0.22em] text-orange-200/80">{content.navigation.announcement}</p>
						<p class="text-base font-semibold text-white">{content.navigation.brandName}</p>
					</div>
				</div>

				<div class="flex flex-wrap items-center gap-2">
					{#each content.navigation.links as link}
						<a href={link.href} class="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-slate-200 transition hover:border-orange-300/40 hover:bg-white/6 hover:text-white">{link.label}</a>
					{/each}
					<a
						href="#quote-request"
						class="px-4 py-2 text-sm font-semibold text-black transition hover:brightness-110"
						style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
					>
						Request a Quote
					</a>
				</div>
			</div>
		</nav>

		<section id="hero" class="mt-6 grid gap-6 rounded-3xl border border-white/10 bg-slate-950/55 p-6 shadow-[0_30px_110px_rgba(15,23,42,0.35)] backdrop-blur lg:grid-cols-[1.15fr_0.85fr] lg:p-8">
			<div class="space-y-5">
				<p class="text-[0.72rem] uppercase tracking-[0.28em] text-orange-200/80">{content.hero.eyebrow}</p>
				<h1 class="max-w-3xl text-4xl font-semibold tracking-tight text-white sm:text-5xl" style="font-family: var(--bdr-heading-font);">{content.hero.headline}</h1>
				<p class="max-w-2xl text-base leading-7 text-slate-300">{content.hero.body}</p>
				<div class="flex flex-wrap gap-3">
					<a
						href="#quote-request"
						class="px-5 py-3 text-sm font-semibold text-black transition hover:brightness-110"
						style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
					>
						Get a Free Quote
					</a>
					<a href={content.hero.secondaryCtaHref} class="rounded-full border border-white/14 px-5 py-3 text-sm font-semibold text-white transition hover:border-orange-300/45 hover:bg-white/6">{content.hero.secondaryCtaLabel}</a>
				</div>
			</div>

			<div class="grid gap-4">
				<div class="overflow-hidden rounded-[1.8rem] border border-orange-400/20 bg-black/60">
					<img src={logoAsset?.file ?? '/clientFiles/BDRLogo.jpeg'} alt={logoAsset?.altText ?? 'BDR Construction logo'} class="h-full w-full object-contain bg-white p-4" />
				</div>
				<div class="rounded-[1.8rem] border border-orange-300/14 bg-orange-300/8 p-5">
					<p class="text-[0.66rem] uppercase tracking-[0.24em] text-orange-100/75">{content.hero.proofEyebrow}</p>
					<div class="mt-4 grid gap-3">
						<div class="rounded-[1.1rem] border border-white/8 bg-white/4 px-4 py-3">
							<p class="text-sm font-semibold text-white">Fast response on urgent needs</p>
							<p class="mt-1 text-sm leading-6 text-slate-200">Storm damage, leaks, and time-sensitive exterior issues can move quickly from intake to follow-up.</p>
						</div>
						<div class="rounded-[1.1rem] border border-white/8 bg-white/4 px-4 py-3">
							<p class="text-sm font-semibold text-white">Residential and commercial experience</p>
							<p class="mt-1 text-sm leading-6 text-slate-200">From family homes to commercial properties and HOA work, BDR can scope projects clearly and professionally.</p>
						</div>
						<div class="rounded-[1.1rem] border border-white/8 bg-white/4 px-4 py-3">
							<p class="text-sm font-semibold text-white">Clear project follow-through</p>
							<p class="mt-1 text-sm leading-6 text-slate-200">Customers get a smoother handoff from inspection and scope review into quote, scheduling, and billing communication.</p>
						</div>
					</div>
				</div>
			</div>
		</section>

		<section id="services" class="mt-8 grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
			<SectionCard title={content.services.title} eyebrow={content.services.eyebrow} copy={content.services.copy}>
				{#if serviceCategories.length}
					<ul class="mt-4 grid gap-3 sm:grid-cols-2">
						{#each serviceCategories as category}
							{@const iconAsset = getBdrAsset(content, category.iconAssetKey)}
							<li class="rounded-[1.1rem] border border-white/8 bg-white/4 px-4 py-4 text-sm text-slate-200">
								<div class="flex items-start gap-3">
									<div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl border border-orange-300/18 bg-black/25 p-2">
										{#if iconAsset}
											<img src={iconAsset.file} alt={iconAsset.altText} class="h-full w-full object-contain" />
										{:else}
											<span class="text-lg text-orange-200">▣</span>
										{/if}
									</div>
									<div>
										<p class="text-base font-semibold text-white">{category.name}</p>
										<p class="mt-1 text-sm leading-6 text-slate-300">{category.description}</p>
									</div>
								</div>
							</li>
						{/each}
					</ul>
				{:else}
					<ul class="mt-4 grid gap-3 sm:grid-cols-2">
						{#each content.services.items as item}
							<li class="rounded-[1.1rem] border border-white/8 bg-white/4 px-4 py-3 text-sm text-slate-200">{item}</li>
						{/each}
					</ul>
				{/if}
			</SectionCard>

			<div id="trust">
				<SectionCard title={content.trust.title} eyebrow={content.trust.eyebrow} copy={content.trust.copy}>
					<ul class="mt-4 grid gap-3 sm:grid-cols-2">
						{#each content.trust.points as point}
							<li class="rounded-[1.1rem] border border-orange-300/14 bg-orange-300/6 px-4 py-3 text-sm text-slate-100">{point}</li>
						{/each}
					</ul>
				</SectionCard>
			</div>
		</section>

		<section id="process" class="mt-8 rounded-[2rem] border border-white/10 bg-slate-950/45 p-6 backdrop-blur">
			<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">{content.process.eyebrow}</p>
			<div class="mt-5 grid gap-4 md:grid-cols-3">
				{#each content.process.steps as item}
					<div class="rounded-[1.5rem] border border-white/8 bg-white/4 p-5">
						<p class="text-sm font-semibold text-orange-200">Step {item.step}</p>
						<h2 class="mt-2 text-xl font-semibold text-white">{item.title}</h2>
						<p class="mt-2 text-sm leading-6 text-slate-300">{item.copy}</p>
					</div>
				{/each}
			</div>
		</section>

		<section class="mt-8 grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
			{#each content.supportingSections as section}
				<SectionCard title={section.title} eyebrow={section.eyebrow} copy={section.copy}>
					<div class="mt-4 grid gap-3">
						{#each section.items as item}
							<div class="rounded-[1.2rem] border border-white/8 bg-white/4 p-4">
								<p class="text-lg font-semibold text-white">{item.title}</p>
								<p class="mt-2 text-sm leading-6 text-slate-300">{item.copy}</p>
							</div>
						{/each}
					</div>
				</SectionCard>
			{/each}
		</section>

		<section id="contact" class="mt-8 grid gap-6 lg:grid-cols-[0.92fr_1.08fr]">
			<div class="rounded-[2rem] border border-orange-300/16 bg-orange-300/8 p-6 backdrop-blur">
				<p class="text-[0.68rem] uppercase tracking-[0.24em] text-orange-100/75">{content.contact.eyebrow}</p>
				<h2 class="mt-3 text-3xl font-semibold text-white">{content.contact.title}</h2>
				<p class="mt-3 max-w-3xl text-base leading-7 text-slate-200">{content.contact.body}</p>
				<div class="mt-5 grid gap-3 text-sm text-slate-200">
					<div class="rounded-[1.2rem] border border-white/10 bg-black/20 p-4">
						<p class="text-[0.64rem] uppercase tracking-[0.2em] text-orange-100/70">Fastest path</p>
						<p class="mt-2 text-base font-semibold text-white">Use the quote form and BDR can drop your request directly into the admin queue.</p>
					</div>
					<div class="rounded-[1.2rem] border border-white/10 bg-black/20 p-4">
						<p class="text-[0.64rem] uppercase tracking-[0.2em] text-orange-100/70">Need a human right now?</p>
						<a href={content.contact.secondaryCtaHref} class="mt-2 inline-flex text-base font-semibold text-white underline decoration-orange-300/50 underline-offset-4">Call BDR</a>
					</div>
					<div class="rounded-[1.2rem] border border-white/10 bg-black/20 p-4">
						<p class="text-[0.64rem] uppercase tracking-[0.2em] text-orange-100/70">Office follow-through</p>
						<p class="mt-2">Requests move from intake to contact, inspection scheduling, estimate drafting, and quote delivery in the same workflow.</p>
					</div>
				</div>
			</div>

			<div id="quote-request" class="rounded-[2rem] border border-white/10 bg-slate-950/65 p-6 shadow-[0_24px_80px_rgba(15,23,42,0.38)] backdrop-blur">
				<div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
					<div>
						<p class="text-[0.68rem] uppercase tracking-[0.24em] text-orange-100/75">Request a quote</p>
						<h3 class="mt-2 text-2xl font-semibold text-white">Tell BDR about the project</h3>
						<p class="mt-2 max-w-2xl text-sm leading-6 text-slate-300">Share the property, timing, and what is going on. The BDR office can review the request, follow up, schedule the inspection, and move your project toward a quote.</p>
					</div>
					<div class="rounded-[1.2rem] border border-white/10 bg-white/4 px-4 py-3 text-sm text-slate-200 lg:max-w-sm">
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-orange-100/75">What happens next</p>
						<p class="mt-2 leading-6">After you submit, BDR can review the request, contact you, confirm scope, and move the job into inspection and estimate follow-up.</p>
					</div>
				</div>

				{#if data.submitted}
					<div class="mt-5 rounded-[1.2rem] border border-emerald-400/25 bg-emerald-400/10 p-4 text-sm text-emerald-100">
						<p class="font-semibold">Quote request sent.</p>
						<p class="mt-1">BDR now has your project details and can follow up, confirm scope, and move the request into inspection and estimate handling.</p>
					</div>
				{/if}

				<form method="POST" action="?/submitQuoteRequest" enctype="multipart/form-data" class="mt-6 grid gap-4">
					{#if form?.errors?.form}
						<div class="rounded-[1.2rem] border border-amber-300/30 bg-amber-300/10 p-4 text-sm text-amber-100">{form.errors.form}</div>
					{/if}
					<div class="grid gap-4 md:grid-cols-2">
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="companyName">Company / customer</label>
							<input id="companyName" name="companyName" class={fieldClass} value={form?.values?.companyName ?? ''} placeholder="Lakeview HOA or Jane Smith" />
							{#if form?.errors?.companyName}<p class="text-xs text-orange-200">{form.errors.companyName}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="contactName">Contact</label>
							<input id="contactName" name="contactName" class={fieldClass} value={form?.values?.contactName ?? ''} placeholder="Jane Smith" />
							{#if form?.errors?.contactName}<p class="text-xs text-orange-200">{form.errors.contactName}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="email">Email</label>
							<input id="email" name="email" type="email" class={fieldClass} value={form?.values?.email ?? ''} placeholder="jane@example.com" />
							{#if form?.errors?.email}<p class="text-xs text-orange-200">{form.errors.email}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="phone">Phone</label>
							<input id="phone" name="phone" type="tel" class={fieldClass} value={form?.values?.phone ?? ''} placeholder="(704) 555-0100" />
							{#if form?.errors?.phone}<p class="text-xs text-orange-200">{form.errors.phone}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="siteName">Site</label>
							<input id="siteName" name="siteName" class={fieldClass} value={form?.values?.siteName ?? ''} placeholder="Building A, main clubhouse, retail center…" />
							{#if form?.errors?.siteName}<p class="text-xs text-orange-200">{form.errors.siteName}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="serviceAddress">Site address</label>
							<input id="serviceAddress" name="serviceAddress" class={fieldClass} value={form?.values?.serviceAddress ?? ''} placeholder="123 Main St, Charlotte, NC" />
							{#if form?.errors?.serviceAddress}<p class="text-xs text-orange-200">{form.errors.serviceAddress}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="serviceType">Service type</label>
							<select id="serviceType" name="serviceType" class={fieldClass}>
								<option value="">Select service type</option>
								{#each serviceTypeOptions as option}
									<option value={option} selected={form?.values?.serviceType === option}>{option}</option>
								{/each}
							</select>
							{#if form?.errors?.serviceType}<p class="text-xs text-orange-200">{form.errors.serviceType}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="propertyType">Property type</label>
							<input id="propertyType" name="propertyType" class={fieldClass} value={form?.values?.propertyType ?? ''} placeholder="Residential, commercial, HOA…" />
							{#if form?.errors?.propertyType}<p class="text-xs text-orange-200">{form.errors.propertyType}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="requestedTimeline">Requested timeline</label>
							<input id="requestedTimeline" name="requestedTimeline" class={fieldClass} value={form?.values?.requestedTimeline ?? ''} placeholder="ASAP, this week, before board meeting…" />
							{#if form?.errors?.requestedTimeline}<p class="text-xs text-orange-200">{form.errors.requestedTimeline}</p>{/if}
						</div>
						<div class="grid gap-2">
							<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="priority">Request priority</label>
							<select id="priority" name="priority" class={fieldClass}>
								<option value="">Select priority</option>
								{#each priorityOptions as option}
									<option value={option.value} selected={form?.values?.priority === option.value}>{option.label}</option>
								{/each}
							</select>
							{#if form?.errors?.priority}<p class="text-xs text-orange-200">{form.errors.priority}</p>{/if}
						</div>
					</div>

					<div class="grid gap-2">
						<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="need">What do you need?</label>
						<textarea id="need" name="need" rows="5" class={`${fieldClass} min-h-32`} placeholder="Tell BDR what is happening, any damage, insurance context, access notes, and the best time to reach you.">{form?.values?.need ?? ''}</textarea>
						{#if form?.errors?.need}<p class="text-xs text-orange-200">{form.errors.need}</p>{/if}
					</div>

					<div class="grid gap-2">
						<label class="text-xs font-semibold uppercase tracking-[0.18em] text-slate-300" for="attachments">Attachments</label>
						<input
							id="attachments"
							name="attachments"
							type="file"
							multiple
							class="rounded-2xl border border-dashed border-white/14 bg-black/25 px-4 py-4 text-sm text-slate-200 file:mr-4 file:rounded-full file:border-0 file:bg-orange-400 file:px-4 file:py-2 file:text-sm file:font-semibold file:text-black hover:border-orange-300/35"
						/>
						<p class="text-xs leading-5 text-slate-400">Attach photos, reports, drawings, or insurance files. The operator queue records file names and sizes with the submitted payload.</p>
					</div>

					<div class="flex flex-col gap-3 rounded-[1.2rem] border border-white/10 bg-white/4 p-4 text-sm text-slate-300 md:flex-row md:items-center md:justify-between">
						<p>Use this form to put the project in front of BDR quickly with the details needed for first follow-up and quote prep.</p>
						<button
							type="submit"
							class="px-5 py-3 text-sm font-semibold text-black transition hover:brightness-110"
							style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
						>
							Submit quote request
						</button>
					</div>
				</form>
			</div>
		</section>

		<footer class="mt-8 rounded-[2rem] border border-white/10 bg-slate-950/60 p-6 backdrop-blur">
			<div class="grid gap-6 md:grid-cols-[1.2fr_0.8fr]">
				<div>
					<p class="text-[0.68rem] uppercase tracking-[0.24em] text-orange-200/75">{content.footer.eyebrow}</p>
					<p class="mt-3 text-xl font-semibold text-white">{content.footer.brandName}</p>
					<p class="mt-3 max-w-2xl text-sm leading-6 text-slate-300">{content.footer.body}</p>
				</div>
				<div>
					<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">{content.footer.linksEyebrow}</p>
					<div class="mt-3 flex flex-wrap gap-2">
						{#each content.footer.links as link}
							<a href={link.href} class="rounded-full border border-white/12 px-4 py-2 text-sm text-slate-200 transition hover:border-orange-300/40 hover:bg-white/6 hover:text-white">{link.label}</a>
						{/each}
					</div>
				</div>
			</div>
		</footer>

		<div class="mt-4 flex flex-col gap-3 rounded-xl border border-white/8 bg-black/35 px-4 py-3 text-sm text-slate-400 sm:flex-row sm:items-center sm:justify-between">
			<div class="flex flex-wrap gap-3">
				{#each content.postFooter.utilityLinks as link}
					<a href={link.href} class="transition hover:text-white">{link.label}</a>
				{/each}
			</div>
			<p>{resolveBdrCopyright(year)}</p>
		</div>
	</div>
</div>
