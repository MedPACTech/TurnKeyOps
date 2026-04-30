<script lang="ts">
	import {
		getBdrActiveContractorPreset,
		getBdrAsset,
		getBdrServiceCategories,
		resolveBdrCopyright
	} from '$lib/bdr-site-content';
	import SectionCard from '$lib/components/SectionCard.svelte';
	import { fallbackMvpSnapshot } from '$lib/mvp-data';
	import { buildPublicProof } from '$lib/mvp-display';
	import type { PageData } from './$types';

	let { data, form }: { data: PageData; form: any } = $props();

	const content = $derived(data.content);
	const year = new Date().getFullYear();
	const logoAsset = $derived(getBdrAsset(content, 'bdr-crest-logo'));
	const navigationLogoAsset = $derived(
		getBdrAsset(content, content.navigation.logoAssetKey) ?? logoAsset
	);
	const faviconAsset = $derived(
		getBdrAsset(content, content.navigation.faviconAssetKey) ?? navigationLogoAsset
	);
	const footerLogoAsset = $derived(
		getBdrAsset(content, content.footer.logoAssetKey) ?? navigationLogoAsset
	);
	const serviceCategories = $derived(getBdrServiceCategories(content));
	const activeContractorPreset = $derived(getBdrActiveContractorPreset(content));
	const themeSettings = $derived(content.themeSettings);
	const isLightTheme = $derived(themeSettings.mode === 'Light');
	const activeHeroMediaOverride = $derived(
		content.hero.mediaByContractorType.find(
			(override) => override.contractorType === activeContractorPreset?.contractorType
		) ?? null
	);
	const heroImageAsset = $derived(
		getBdrAsset(
			content,
			activeHeroMediaOverride?.heroImageAssetKey || content.hero.heroImageAssetKey
		)
	);
	const heroBackgroundImageAsset = $derived(
		getBdrAsset(
			content,
			activeHeroMediaOverride?.backgroundImageAssetKey || content.hero.backgroundImageAssetKey
		)
	);
	const heroTextureAsset = $derived(
		getBdrAsset(
			content,
			activeHeroMediaOverride?.backgroundTextureAssetKey || content.hero.backgroundTextureAssetKey
		)
	);
	const ctaBannerImageAsset = $derived(
		getBdrAsset(content, content.ctaBanner.backgroundImageAssetKey)
	);
	const heroImageAltText = $derived(
		activeHeroMediaOverride?.heroImageAltText?.trim() ||
			content.hero.heroImageAltText.trim() ||
			heroImageAsset?.altText ||
			'TurnKey contractor hero image'
	);
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
	let navMenuOpen = $state(false);

	const resolveHeroCtaHref = (ctaType: 'anchor' | 'link' | 'phone', target: string) => {
		if (ctaType === 'phone') {
			if (target.startsWith('tel:')) {
				return target;
			}

			return `tel:${target.replace(/[^0-9+]/g, '')}`;
		}

		return target;
	};

	const heroBackgroundStyle = $derived(
		[
			heroBackgroundImageAsset?.file
				? `background-image:linear-gradient(135deg, rgba(15,23,42,0.68), rgba(15,23,42,0.18)), url(${heroBackgroundImageAsset.file})`
				: '',
			heroBackgroundImageAsset?.file ? 'background-size:cover' : '',
			heroBackgroundImageAsset?.file ? 'background-position:center' : ''
		]
			.filter(Boolean)
			.join(';')
	);
	const primaryHeroHref = $derived(
		resolveHeroCtaHref(content.hero.primaryCtaType, content.hero.primaryCtaHref)
	);
	const secondaryHeroHref = $derived(
		resolveHeroCtaHref(content.hero.secondaryCtaType, content.hero.secondaryCtaHref)
	);
	const ctaBannerSecondaryHref = $derived(
		resolveHeroCtaHref(content.ctaBanner.secondaryCtaType, content.ctaBanner.secondaryCtaHref)
	);
</script>

<svelte:head>
	<title>BDR Construction</title>
	<meta name="description" content="BDR Construction public site for roofing, exterior work, and fast estimate requests." />
	<link rel="icon" href={faviconAsset?.file ?? '/clientFiles/logo.png'} />
</svelte:head>

<div
	class="min-h-screen bg-[radial-gradient(circle_at_top,_rgba(249,115,22,0.16),_transparent_24%),linear-gradient(180deg,_#050505_0%,_#101010_46%,_#181818_100%)] text-white"
	data-theme={themeSettings.mode.toLowerCase()}
	data-style-preset={themeSettings.preset.toLowerCase()}
	style={`color: var(--bdr-text); background-color: var(--bdr-background); font-family: var(--bdr-body-font); ${publicThemeStyle}`}
>
	<div class="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
		<nav
			id="top"
			class={`${content.navigation.stickyHeader ? 'sticky top-4' : ''} z-30 rounded-xl border border-white/10 bg-slate-950/90 px-4 py-3 shadow-[0_20px_60px_rgba(15,23,42,0.32)] backdrop-blur`}
		>
			<div
				class={`flex flex-col gap-4 ${content.navigation.layout === 'centered' ? 'items-center text-center' : 'lg:flex-row lg:items-center'} ${content.navigation.layout === 'right-aligned' ? 'lg:justify-end' : 'lg:justify-between'}`}
			>
				<div class={`flex items-center gap-3 ${content.navigation.layout === 'centered' ? 'justify-center' : ''}`}>
					<div class="flex h-11 w-11 items-center justify-center overflow-hidden rounded-xl border border-orange-300/25 bg-white p-2">
						<img src={navigationLogoAsset?.file ?? '/clientFiles/BDRLogo.jpeg'} alt={navigationLogoAsset?.altText ?? 'BDR Construction logo'} class="h-full w-full object-contain" />
					</div>
					<div>
						<p class="text-[0.68rem] uppercase tracking-[0.22em] text-orange-200/80">{content.navigation.announcement}</p>
						<p class="text-base font-semibold text-white">{content.navigation.brandName}</p>
					</div>
				</div>

				<div class={`hidden flex-wrap items-center gap-2 lg:flex ${content.navigation.layout === 'centered' ? 'justify-center' : ''}`}>
					{#each content.navigation.links as link}
						<a
							href={link.href}
							target={link.openInNewTab ? '_blank' : undefined}
							rel={link.openInNewTab ? 'noreferrer' : undefined}
							class="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-slate-200 transition hover:border-orange-300/40 hover:bg-white/6 hover:text-white"
						>
							{link.label}
						</a>
					{/each}
					<a
						href={content.navigation.primaryCtaHref}
						class="px-4 py-2 text-sm font-semibold text-black transition hover:brightness-110"
						style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
					>
						{content.navigation.primaryCtaLabel}
					</a>
					{#if content.navigation.showPhoneButton}
						<a href={`tel:${content.navigation.phoneNumber.replace(/[^0-9+]/g, '')}`} class="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-white transition hover:border-orange-300/40 hover:bg-white/6">
							{content.navigation.phoneNumber}
						</a>
					{/if}
					{#if content.navigation.showThemeControl}
						<button type="button" class="rounded-full border border-white/12 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-slate-200 transition hover:border-orange-300/40 hover:bg-white/6">
							{themeSettings.mode}
						</button>
					{/if}
				</div>

				<div class="flex items-center justify-between gap-3 lg:hidden">
					<div class="flex items-center gap-2">
						{#if content.navigation.showPhoneButton}
							<a href={`tel:${content.navigation.phoneNumber.replace(/[^0-9+]/g, '')}`} class="rounded-full border border-white/12 px-3 py-2 text-xs font-semibold text-white">
								Call
							</a>
						{/if}
						{#if content.navigation.showThemeControl}
							<button type="button" class="rounded-full border border-white/12 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-slate-200">
								{themeSettings.mode}
							</button>
						{/if}
					</div>
					<button
						type="button"
						class="rounded-full border border-white/12 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-slate-200"
						aria-expanded={navMenuOpen}
						onclick={() => (navMenuOpen = !navMenuOpen)}
					>
						{navMenuOpen ? 'Close' : 'Menu'}
					</button>
				</div>
			</div>

			{#if navMenuOpen}
				<div class="mt-4 grid gap-2 lg:hidden">
					{#each content.navigation.links as link}
						<a
							href={link.href}
							target={link.openInNewTab ? '_blank' : undefined}
							rel={link.openInNewTab ? 'noreferrer' : undefined}
							class="rounded-2xl border border-white/12 px-4 py-3 text-sm font-medium text-slate-200 transition hover:border-orange-300/40 hover:bg-white/6 hover:text-white"
						>
							{link.label}
						</a>
					{/each}
					<a
						href={content.navigation.primaryCtaHref}
						class="px-4 py-3 text-center text-sm font-semibold text-black transition hover:brightness-110"
						style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
					>
						{content.navigation.primaryCtaLabel}
					</a>
				</div>
			{/if}
		</nav>

		<section
			id="hero"
			class={`relative mt-6 overflow-hidden rounded-[2rem] px-6 py-8 lg:px-8 lg:py-10 ${isLightTheme ? 'bg-white/78' : 'bg-slate-950/38'}`}
			style={heroBackgroundStyle}
		>
			<div class={`absolute inset-0 ${isLightTheme ? 'bg-[linear-gradient(120deg,rgba(255,255,255,0.92),rgba(255,255,255,0.78),rgba(255,255,255,0.48))]' : 'bg-[linear-gradient(120deg,rgba(2,6,23,0.88),rgba(2,6,23,0.7),rgba(2,6,23,0.34))]'}`}></div>
			{#if heroTextureAsset?.file}
				<div class="absolute inset-0 opacity-35 mix-blend-soft-light" style={`background-image:url(${heroTextureAsset.file}); background-size:cover; background-position:center;`}></div>
			{/if}

			<div class="relative grid gap-8 lg:grid-cols-[1.05fr_0.95fr] lg:items-center">
				<div class="space-y-6">
					<div class="space-y-4">
						<p class={`text-[0.72rem] uppercase tracking-[0.28em] ${isLightTheme ? 'text-[color:var(--bdr-primary)]' : 'text-orange-200/85'}`}>{content.hero.eyebrow}</p>
						<h1
							class={`max-w-3xl text-4xl font-semibold tracking-tight sm:text-5xl ${isLightTheme ? 'text-slate-950' : 'text-white'}`}
							style="font-family: var(--bdr-heading-font);"
						>
							{content.hero.headline}
						</h1>
						<p class={`max-w-2xl text-base leading-7 ${isLightTheme ? 'text-slate-700' : 'text-slate-200'}`}>
							{content.hero.subheadline}
						</p>
					</div>

					<div class="flex flex-wrap gap-3">
						<a
							href={primaryHeroHref}
							class="px-5 py-3 text-sm font-semibold text-black transition hover:brightness-110"
							style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
						>
							{content.hero.primaryCtaLabel}
						</a>
						<a
							href={secondaryHeroHref}
							class={`rounded-full px-5 py-3 text-sm font-semibold transition ${isLightTheme ? 'border border-slate-300 text-slate-900 hover:border-[color:var(--bdr-primary)] hover:bg-white' : 'border border-white/14 text-white hover:border-orange-300/45 hover:bg-white/6'}`}
						>
							{content.hero.secondaryCtaLabel}
						</a>
					</div>

					<div>
						<p class={`text-[0.66rem] uppercase tracking-[0.24em] ${isLightTheme ? 'text-slate-500' : 'text-orange-100/80'}`}>{content.hero.trustBadgeEyebrow}</p>
						<div class="mt-4 grid gap-3 sm:grid-cols-3">
							{#each content.hero.trustBadges as badge}
								{@const badgeAsset = getBdrAsset(content, badge.iconAssetKey)}
								<div class={`rounded-[1.35rem] px-4 py-4 ${isLightTheme ? 'bg-white/88 shadow-[0_18px_45px_rgba(15,23,42,0.08)]' : 'bg-white/6 backdrop-blur'}`}>
									<div class="flex items-start gap-3">
										<div class={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${isLightTheme ? 'bg-slate-950/5' : 'bg-black/20'} p-2`}>
											{#if badgeAsset}
												<img src={badgeAsset.file} alt={badgeAsset.altText} class="h-full w-full object-contain" />
											{:else}
												<span class={`text-lg ${isLightTheme ? 'text-[color:var(--bdr-primary)]' : 'text-orange-200'}`}>▣</span>
											{/if}
										</div>
										<div>
											<p class={`text-sm font-semibold ${isLightTheme ? 'text-slate-950' : 'text-white'}`}>{badge.title}</p>
											<p class={`mt-1 text-sm leading-6 ${isLightTheme ? 'text-slate-600' : 'text-slate-200'}`}>{badge.description}</p>
										</div>
									</div>
								</div>
							{/each}
						</div>
					</div>
				</div>

				<div class="relative">
					<div class={`absolute -inset-6 rounded-[2.2rem] blur-3xl ${isLightTheme ? 'bg-[radial-gradient(circle,rgba(249,115,22,0.14),transparent_65%)]' : 'bg-[radial-gradient(circle,rgba(249,115,22,0.22),transparent_65%)]'}`}></div>
					<div class={`relative overflow-hidden rounded-[2rem] ${isLightTheme ? 'bg-slate-100/80 shadow-[0_32px_70px_rgba(15,23,42,0.12)]' : 'bg-slate-950/60 shadow-[0_32px_90px_rgba(2,6,23,0.45)]'}`}>
						{#if heroImageAsset}
							<img
								src={heroImageAsset.file}
								alt={heroImageAltText}
								class="aspect-[4/3] h-full w-full object-cover"
							/>
						{:else}
							<div class={`flex aspect-[4/3] items-center justify-center ${isLightTheme ? 'text-slate-500' : 'text-slate-300'}`}>
								Hero image unavailable
							</div>
						{/if}
					</div>
				</div>
			</div>
		</section>

		<section id="services" class="mt-8 grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
			<SectionCard title={content.services.title} eyebrow={content.services.eyebrow} copy={content.services.copy}>
				{#if serviceCategories.length}
					<ul class="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
						{#each serviceCategories as category}
							{@const iconAsset = getBdrAsset(content, category.iconAssetKey)}
							<li class="rounded-[1.4rem] bg-white/90 px-4 py-4 text-sm text-slate-700 shadow-[0_18px_40px_rgba(15,23,42,0.08)]">
								<div class="flex items-start gap-3">
									<div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-slate-950/5 p-2">
										{#if iconAsset}
											<img src={iconAsset.file} alt={iconAsset.altText} class="h-full w-full object-contain" />
										{:else}
											<span class="text-lg text-[color:var(--bdr-primary)]">▣</span>
										{/if}
									</div>
									<div>
										<div class="flex flex-wrap items-center gap-2">
											<p class="text-base font-semibold text-slate-950">{category.name}</p>
											{#if category.featured}
												<span class="rounded-full bg-[color:var(--bdr-primary)]/12 px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-[color:var(--bdr-primary)]">Featured</span>
											{/if}
										</div>
										<p class="mt-1 text-sm leading-6 text-slate-600">{category.description}</p>
										{#if category.detailPageUrl}
											<a href={category.detailPageUrl} class="mt-3 inline-flex text-sm font-semibold text-[color:var(--bdr-primary)] transition hover:brightness-90">Learn more</a>
										{/if}
									</div>
								</div>
							</li>
						{/each}
					</ul>
				{:else}
					<ul class="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
						{#each content.services.items as item}
							<li class="rounded-[1.4rem] bg-white/90 px-4 py-3 text-sm text-slate-700 shadow-[0_18px_40px_rgba(15,23,42,0.08)]">{item}</li>
						{/each}
					</ul>
				{/if}

				{#if content.services.ctaLabel && content.services.ctaHref}
					<div class="mt-5">
						<a
							href={content.services.ctaHref}
							class="inline-flex px-5 py-3 text-sm font-semibold text-black transition hover:brightness-110"
							style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
						>
							{content.services.ctaLabel}
						</a>
					</div>
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

		<section id="process" class="mt-8 rounded-[2rem] border border-white/10 bg-slate-950/82 p-6 backdrop-blur lg:p-8">
			<div class="max-w-3xl">
				<p class="text-[0.68rem] uppercase tracking-[0.24em] text-orange-200/75">{content.process.eyebrow}</p>
				<h2 class="mt-3 text-3xl font-semibold text-white" style="font-family: var(--bdr-heading-font);">{content.process.title}</h2>
				<p class="mt-3 text-sm leading-6 text-slate-300">{content.process.description}</p>
			</div>
			<div class="mt-6 grid gap-4 lg:grid-cols-3">
				{#each content.process.steps as item}
					{@const stepIcon = getBdrAsset(content, item.iconAssetKey)}
					<div class="relative rounded-[1.6rem] border border-white/8 bg-white/5 p-5">
						<div class="flex items-start gap-4">
							<div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-white/10 p-2">
								{#if stepIcon}
									<img src={stepIcon.file} alt={stepIcon.altText} class="h-full w-full object-contain" />
								{:else}
									<span class="text-lg text-orange-200">▣</span>
								{/if}
							</div>
							<div class="min-w-0 flex-1">
								<div class="flex flex-wrap items-center gap-3">
									<p class="text-2xl font-semibold text-orange-200">0{item.step}</p>
									{#if item.timeframe}
										<span class="rounded-full border border-white/12 px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-slate-300">{item.timeframe}</span>
									{/if}
								</div>
								<h3 class="mt-3 text-xl font-semibold text-white">{item.title}</h3>
								<p class="mt-2 text-sm leading-6 text-slate-300">{item.copy}</p>
							</div>
						</div>
					</div>
				{/each}
			</div>
		</section>

		<section
			class="relative mt-8 overflow-hidden rounded-[2rem] px-6 py-8 lg:px-8"
			style={`background-image:linear-gradient(rgba(2,6,23,${content.ctaBanner.overlayOpacity}),rgba(2,6,23,${content.ctaBanner.overlayOpacity})),url(${ctaBannerImageAsset?.file ?? ''}); background-size:cover; background-position:center;`}
		>
			<div class="relative flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
				<div class="max-w-3xl">
					<p class="text-[0.68rem] uppercase tracking-[0.24em] text-orange-200/80">{content.ctaBanner.eyebrow}</p>
					<h2 class="mt-3 text-3xl font-semibold text-white" style="font-family: var(--bdr-heading-font);">{content.ctaBanner.title}</h2>
					<p class="mt-3 max-w-3xl text-sm leading-6 text-slate-200">{content.ctaBanner.description}</p>
				</div>
				<div class="flex flex-wrap gap-3">
					<a
						href={content.ctaBanner.primaryCtaHref}
						class="px-5 py-3 text-sm font-semibold text-black transition hover:brightness-110"
						style="background-color: var(--bdr-primary); border-radius: var(--bdr-button-radius);"
					>
						{content.ctaBanner.primaryCtaLabel}
					</a>
					<a
						href={ctaBannerSecondaryHref}
						class="rounded-full border border-white/14 px-5 py-3 text-sm font-semibold text-white transition hover:border-orange-300/45 hover:bg-white/6"
					>
						{content.ctaBanner.secondaryCtaLabel}
					</a>
				</div>
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

		<footer class="mt-8 rounded-[2rem] border border-white/10 bg-slate-950/68 p-6 backdrop-blur">
			<div class="grid gap-6 lg:grid-cols-[1.15fr_0.85fr_0.8fr_0.9fr]">
				<div>
					<p class="text-[0.68rem] uppercase tracking-[0.24em] text-orange-200/75">{content.footer.eyebrow}</p>
					<div class="mt-4 flex items-center gap-3">
						<div class="flex h-12 w-12 items-center justify-center overflow-hidden rounded-2xl border border-white/10 bg-white p-2">
							<img src={footerLogoAsset?.file ?? '/clientFiles/BDRLogo.jpeg'} alt={footerLogoAsset?.altText ?? 'BDR Construction logo'} class="h-full w-full object-contain" />
						</div>
						<div>
							<p class="text-xl font-semibold text-white">{content.footer.brandName}</p>
							{#if content.footer.serviceAreaText}
								<p class="mt-1 text-sm text-slate-400">{content.footer.serviceAreaText}</p>
							{/if}
						</div>
					</div>
					<p class="mt-4 max-w-2xl text-sm leading-6 text-slate-300">{content.footer.body}</p>
					{#if content.footer.socialLinks.length}
						<div class="mt-4 flex flex-wrap gap-3">
							{#each content.footer.socialLinks as social}
								{@const socialAsset = getBdrAsset(content, social.iconAssetKey)}
								<a
									href={social.url}
									target="_blank"
									rel="noreferrer"
									aria-label={social.platform}
									class="flex h-11 w-11 items-center justify-center rounded-full border border-white/12 bg-white/4 transition hover:border-orange-300/40 hover:bg-white/8"
								>
									{#if socialAsset}
										<img src={socialAsset.file} alt={social.platform} class="h-5 w-5 object-contain" />
									{:else}
										<span class="text-xs font-semibold uppercase text-white">{social.platform.slice(0, 2)}</span>
									{/if}
								</a>
							{/each}
						</div>
					{/if}
				</div>

				{#if content.footer.servicesLinks.length}
					<div>
						<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">{content.footer.servicesEyebrow}</p>
						<div class="mt-3 grid gap-2">
							{#each content.footer.servicesLinks as link}
								<a href={link.href} class="text-sm text-slate-200 transition hover:text-white">{link.label}</a>
							{/each}
						</div>
					</div>
				{/if}

				{#if content.footer.navigationLinks.length}
					<div>
						<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">{content.footer.navigationEyebrow}</p>
						<div class="mt-3 grid gap-2">
							{#each content.footer.navigationLinks as link}
								<a href={link.href} class="text-sm text-slate-200 transition hover:text-white">{link.label}</a>
							{/each}
						</div>
					</div>
				{/if}

				{#if content.footer.phone || content.footer.email || content.footer.address}
					<div>
						<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">{content.footer.contactEyebrow}</p>
						<div class="mt-3 grid gap-2 text-sm text-slate-200">
							{#if content.footer.phone}
								<a href={`tel:${content.footer.phone.replace(/[^0-9+]/g, '')}`} class="transition hover:text-white">{content.footer.phone}</a>
							{/if}
							{#if content.footer.email}
								<a href={`mailto:${content.footer.email}`} class="transition hover:text-white">{content.footer.email}</a>
							{/if}
							{#if content.footer.address}
								<p class="leading-6 text-slate-300">{content.footer.address}</p>
							{/if}
						</div>
					</div>
				{/if}
			</div>
		</footer>

		<div class="mt-4 flex flex-col gap-3 rounded-xl border border-white/8 bg-black/35 px-4 py-3 text-sm text-slate-400 sm:flex-row sm:items-center sm:justify-between">
			<div class="flex flex-wrap gap-3">
				{#each content.postFooter.legalLinks as link}
					<a href={link.href} class="transition hover:text-white">{link.label}</a>
				{/each}
			</div>
			<p>{resolveBdrCopyright(content, year)}</p>
		</div>
	</div>
</div>
