<script lang="ts">
	import { BadgeCheck, CalendarDays, ClipboardList, MessageSquareText, ShieldCheck, Star } from 'lucide-svelte';
	import {
		getBdrActiveContractorPreset,
		getBdrAsset,
		getBdrServiceCategories,
		resolveBdrCopyright
	} from '$lib/bdr-site-content';
	import type { PageData } from './$types';

	let { data, form }: { data: PageData; form: any } = $props();

	const content = $derived(data.content);
	const year = new Date().getFullYear();
	const serviceCategories = $derived(getBdrServiceCategories(content));
	const activeContractorPreset = $derived(getBdrActiveContractorPreset(content));
	const themeSettings = $derived(content.themeSettings);
	const navigationLogoAsset = $derived(
		getBdrAsset(content, content.navigation.logoAssetKey) ?? getBdrAsset(content, 'bdr-crest-logo')
	);
	const faviconAsset = $derived(
		getBdrAsset(content, content.navigation.faviconAssetKey) ?? navigationLogoAsset
	);
	const footerLogoAsset = $derived(
		getBdrAsset(content, content.footer.logoAssetKey) ?? navigationLogoAsset
	);
	const activeHeroMediaOverride = $derived(
		content.hero.mediaByContractorType.find(
			(override) => override.contractorType === activeContractorPreset?.contractorType
		) ?? null
	);
	const heroImageAsset = $derived(
		getBdrAsset(content, activeHeroMediaOverride?.heroImageAssetKey || content.hero.heroImageAssetKey)
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
	const heroBackgroundUrl = $derived(
		heroBackgroundImageAsset?.file ?? heroImageAsset?.file ?? '/clientFiles/image17.jpeg'
	);
	const ctaBackgroundUrl = $derived(ctaBannerImageAsset?.file ?? '/clientFiles/image29.jpeg');
	const headlineLead = $derived(
		content.hero.headline.toLowerCase().includes('built to last')
			? 'Built strong.'
			: content.hero.headline
	);
	const headlineAccent = $derived(
		content.hero.headline.toLowerCase().includes('built to last') ? 'Built to last.' : ''
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
			`--bdr-heading-font:${themeSettings.typography.headingFont}, Impact, Arial Narrow, sans-serif`,
			`--bdr-body-font:${themeSettings.typography.bodyFont}, Inter, sans-serif`
		].join(';')
	);
	const serviceTypeOptions = $derived(
		serviceCategories.length
			? serviceCategories.map((category) => category.name)
			: content.services.items
	);
	const quoteFormFields = $derived(content.quoteForm.fields);
	const quoteFormBenefits = $derived(content.quoteForm.benefits);

	let navMenuOpen = $state(false);

	const resolveCtaHref = (ctaType: 'anchor' | 'link' | 'phone', target: string) => {
		if (ctaType !== 'phone') return target;
		if (target.startsWith('tel:')) return target;
		return `tel:${target.replace(/[^0-9+]/g, '')}`;
	};

	const getQuoteFormFieldOptions = (field: (typeof content.quoteForm.fields)[number]) =>
		field.options?.length
			? field.options
			: field.key === 'serviceType'
				? serviceTypeOptions
				: field.key === 'priority'
					? ['standard', 'priority', 'emergency']
					: [];

	const isQuoteFormFieldFullWidth = (field: (typeof content.quoteForm.fields)[number]) =>
		field.type === 'textarea' || field.type === 'file';
</script>

<svelte:head>
	<title>BDR Construction</title>
	<meta
		name="description"
		content="BDR Construction public site for concrete driveways, patios, sidewalks, slabs, and estimate requests."
	/>
	<link rel="icon" href={faviconAsset?.file ?? '/clientFiles/logo.png'} />
</svelte:head>

<div
	class="bdr-public-site"
	data-theme={themeSettings.mode.toLowerCase()}
	data-style-preset={themeSettings.preset.toLowerCase()}
	style={publicThemeStyle}
>
	<div class="nav-shell">
		<nav class="top-nav site-frame" aria-label="Primary navigation">
			<a href="#hero" class="brand-lockup" aria-label="BDR Construction home">
				<img
					src={navigationLogoAsset?.file ?? '/clientFiles/BDRLogo-transparent.png'}
					alt={navigationLogoAsset?.altText ?? 'BDR Construction logo'}
				/>
			</a>

			<div class="nav-links">
				{#each content.navigation.links as link}
					<a
						href={link.href}
						target={link.openInNewTab ? '_blank' : undefined}
						rel={link.openInNewTab ? 'noreferrer' : undefined}
					>
						{link.label}
					</a>
				{/each}
			</div>

			<div class="nav-actions">
				<a class="btn btn-primary btn-small" href={content.navigation.primaryCtaHref}>
					{content.navigation.primaryCtaLabel}
				</a>
				{#if content.navigation.showPhoneButton}
					<a
						class="btn btn-outline btn-small"
						href={`tel:${content.navigation.phoneNumber.replace(/[^0-9+]/g, '')}`}
					>
						{content.navigation.phoneNumber}
					</a>
				{/if}
			</div>

			<button
				type="button"
				class="menu-toggle"
				aria-expanded={navMenuOpen}
				onclick={() => (navMenuOpen = !navMenuOpen)}
			>
				{navMenuOpen ? 'Close' : 'Menu'}
			</button>
		</nav>

		{#if navMenuOpen}
			<div class="mobile-menu site-frame">
				{#each content.navigation.links as link}
					<a href={link.href} onclick={() => (navMenuOpen = false)}>{link.label}</a>
				{/each}
				<a class="btn btn-primary" href={content.navigation.primaryCtaHref}>
					{content.navigation.primaryCtaLabel}
				</a>
			</div>
		{/if}
	</div>

	<header id="hero" class="hero" style={`--hero-image:url('${heroBackgroundUrl}')`}>
		{#if heroTextureAsset?.file}
			<div
				class="hero-texture"
				style={`background-image:url('${heroTextureAsset.file}')`}
				aria-hidden="true"
			></div>
		{/if}
		<div class="hero-overlay" aria-hidden="true"></div>

		<div class="site-frame hero-frame">
			<div class="hero-content">
				<p class="eyebrow">{content.hero.eyebrow}</p>
				<h1>
					<span>{headlineLead}</span>
					{#if headlineAccent}
						<span class="accent-line">{headlineAccent}</span>
					{/if}
				</h1>
				<p class="hero-copy">{content.hero.subheadline}</p>

				<div class="hero-actions">
					<a
						href={resolveCtaHref(content.hero.primaryCtaType, content.hero.primaryCtaHref)}
						class="btn btn-primary"
					>
						{content.hero.primaryCtaLabel}
					</a>
					<a
						href={resolveCtaHref(content.hero.secondaryCtaType, content.hero.secondaryCtaHref)}
						class="btn btn-outline"
					>
						{content.hero.secondaryCtaLabel}
					</a>
				</div>
			</div>

			{#if content.hero.trustBadges.length}
				<div class="trust-row" aria-label="BDR trust signals">
					{#each content.hero.trustBadges as badge}
						<div class="trust-item">
							<div class="trust-icon">
								{#if badge.title.toLowerCase().includes('star')}
									<Star size={28} strokeWidth={1.8} aria-hidden="true" />
								{:else if badge.title.toLowerCase().includes('quality')}
									<BadgeCheck size={28} strokeWidth={1.8} aria-hidden="true" />
								{:else}
									<ShieldCheck size={28} strokeWidth={1.8} aria-hidden="true" />
								{/if}
							</div>
							<div>
								<p>{badge.title}</p>
								<span>{badge.description}</span>
							</div>
						</div>
					{/each}
				</div>
			{/if}
		</div>
	</header>

	<main>
		<section id="services" class="section section-light">
			<div class="site-frame">
				<div class="section-heading centered">
					<p class="eyebrow orange">{content.services.eyebrow}</p>
					<h2>{content.services.title}</h2>
					<p>{content.services.copy}</p>
				</div>

				<div class="service-grid">
					{#each serviceCategories as category}
						{@const iconAsset = getBdrAsset(content, category.iconAssetKey)}
						<article class="service-card">
							<div class="service-icon">
								{#if iconAsset}
									<img src={iconAsset.file} alt={iconAsset.altText} />
								{:else}
									<span></span>
								{/if}
							</div>
							<h3>{category.name}</h3>
							<p>{category.description}</p>
						</article>
					{/each}
				</div>

				{#if content.services.ctaLabel && content.services.ctaHref}
					<div class="section-action">
						<a href={content.services.ctaHref} class="btn btn-ghost-dark">
							{content.services.ctaLabel}
						</a>
					</div>
				{/if}
			</div>
		</section>

		<section id="process" class="section process-section">
			<div class="site-frame">
				<div class="section-heading centered dark">
					<p class="eyebrow orange">{content.process.eyebrow}</p>
					<h2>{content.process.title}</h2>
				</div>

				<div class="process-grid">
					{#each content.process.steps as step}
						<article class="process-step">
							<div class="step-number">0{step.step}</div>
							<div class="process-icon">
								{#if step.step === '1'}
									<MessageSquareText size={40} strokeWidth={1.55} aria-hidden="true" />
								{:else if step.step === '2'}
									<ClipboardList size={40} strokeWidth={1.55} aria-hidden="true" />
								{:else}
									<CalendarDays size={40} strokeWidth={1.55} aria-hidden="true" />
								{/if}
							</div>
							<div>
								<h3>{step.title}</h3>
								<p>{step.copy}</p>
							</div>
						</article>
					{/each}
				</div>
			</div>
		</section>

		<section id="trust" class="section section-light proof-section">
			<div class="site-frame proof-layout">
				<div>
					<p class="eyebrow orange">{content.trust.eyebrow}</p>
					<h2>{content.trust.title}</h2>
					<p>{content.trust.copy}</p>
				</div>

				<div class="proof-grid">
					{#each content.trust.points as point}
						<article class="proof-card">
							<BadgeCheck size={22} strokeWidth={1.8} aria-hidden="true" />
							<p>{point}</p>
						</article>
					{/each}
				</div>
			</div>
		</section>

		<section id="supporting" class="section projects-section">
			<div class="site-frame">
				<div class="section-heading centered dark">
					<p class="eyebrow orange">{content.supportingSections[0]?.eyebrow}</p>
					<h2>{content.supportingSections[0]?.title}</h2>
					<p>{content.supportingSections[0]?.copy}</p>
				</div>

				<div class="project-grid">
					{#each content.supportingSections[0]?.items ?? [] as item}
						<article class="project-card">
							<h3>{item.title}</h3>
							<p>{item.copy}</p>
						</article>
					{/each}
				</div>
			</div>
		</section>

		<section
			id="contact"
			class="section cta-band"
			style={`--cta-image:url('${ctaBackgroundUrl}')`}
		>
			<div class="site-frame cta-content">
				<p class="eyebrow orange">{content.ctaBanner.eyebrow}</p>
				<h2>{content.ctaBanner.title}</h2>
				<p>{content.ctaBanner.description}</p>
				<div class="hero-actions">
					<a href={content.ctaBanner.primaryCtaHref} class="btn btn-primary">
						{content.ctaBanner.primaryCtaLabel}
					</a>
					<a
						href={resolveCtaHref(content.ctaBanner.secondaryCtaType, content.ctaBanner.secondaryCtaHref)}
						class="btn btn-outline"
					>
						{content.ctaBanner.secondaryCtaLabel}
					</a>
				</div>
			</div>
		</section>

		<section id="quote-request" class="section section-light quote-section">
			<div class="site-frame quote-layout">
				<div class="quote-benefits">
					<p class="eyebrow orange">{content.quoteForm.eyebrow}</p>
					<h2>{content.quoteForm.title}</h2>
					<ul>
						{#each quoteFormBenefits as benefit}
							{@const benefitIcon = getBdrAsset(content, benefit.iconAssetKey)}
							<li>
								{#if benefitIcon}
									<img src={benefitIcon.file} alt="" />
								{/if}
								<span>{benefit.text}</span>
							</li>
						{/each}
					</ul>
				</div>

				<form method="POST" action="?/submitQuoteRequest" enctype="multipart/form-data" class="quote-form">
					{#if data.submitted}
						<div class="form-message success">{content.quoteForm.successMessage}</div>
					{/if}
					{#if form?.errors?.form}
						<div class="form-message error">{form.errors.form}</div>
					{/if}

					<div class="form-grid">
						{#each quoteFormFields as field}
							<div class={`field ${isQuoteFormFieldFullWidth(field) ? 'full' : ''}`}>
								<label for={field.key}>{field.label}</label>
								{#if field.type === 'textarea'}
									<textarea
										id={field.key}
										name={field.key}
										rows="5"
										placeholder={field.placeholder ?? ''}
										required={field.required}
									>{form?.values?.[field.key] ?? ''}</textarea>
								{:else if field.type === 'select'}
									<select id={field.key} name={field.key} required={field.required}>
										<option value="">Select {field.label.toLowerCase()}</option>
										{#each getQuoteFormFieldOptions(field) as option}
											<option value={option} selected={form?.values?.[field.key] === option}>
												{option}
											</option>
										{/each}
									</select>
								{:else if field.type === 'file'}
									<input id={field.key} name={field.key} type="file" multiple />
								{:else}
									<input
										id={field.key}
										name={field.key}
										type={field.type}
										value={form?.values?.[field.key] ?? ''}
										placeholder={field.placeholder ?? ''}
										required={field.required}
									/>
								{/if}
								{#if form?.errors?.[field.key]}
									<p class="field-error">{form.errors[field.key]}</p>
								{/if}
							</div>
						{/each}
					</div>

					<button type="submit" class="btn btn-primary submit-button">
						{content.quoteForm.submitButtonLabel}
					</button>
				</form>
			</div>
		</section>
	</main>

	<footer class="site-footer">
		<div class="site-frame footer-grid">
			<div>
				<img
					src={footerLogoAsset?.file ?? '/clientFiles/BDRLogo-transparent.png'}
					alt={footerLogoAsset?.altText ?? 'BDR Construction logo'}
					class="footer-logo"
				/>
				<p>{content.footer.body}</p>
			</div>

			<div>
				<h3>{content.footer.servicesEyebrow}</h3>
				{#each content.footer.servicesLinks as link}
					<a href={link.href}>{link.label}</a>
				{/each}
			</div>

			<div>
				<h3>{content.footer.navigationEyebrow}</h3>
				{#each content.footer.navigationLinks as link}
					<a href={link.href}>{link.label}</a>
				{/each}
			</div>

			<div>
				<h3>{content.footer.contactEyebrow}</h3>
				<a href={`tel:${content.footer.phone.replace(/[^0-9+]/g, '')}`}>{content.footer.phone}</a>
				<a href={`mailto:${content.footer.email}`}>{content.footer.email}</a>
				<p>{content.footer.address}</p>
			</div>
		</div>

		<div class="site-frame footer-bottom">
			<p>{resolveBdrCopyright(content, year)}</p>
			<div>
				{#each content.postFooter.legalLinks as link}
					<a href={link.href}>{link.label}</a>
				{/each}
			</div>
		</div>
	</footer>
</div>

<style>
	:global(html) {
		scroll-behavior: smooth;
	}

	:global(#hero),
	:global(#services),
	:global(#process),
	:global(#trust),
	:global(#supporting),
	:global(#contact),
	:global(#quote-request) {
		scroll-margin-top: 92px;
	}

	.bdr-public-site {
		min-height: 100vh;
		background: #0b0b0a;
		color: #fff;
		font-family: var(--bdr-body-font);
	}

	.site-frame {
		width: min(1180px, calc(100vw - 28px));
		margin: 0 auto;
	}

	.hero {
		position: relative;
		min-height: 690px;
		overflow: hidden;
		background-image: linear-gradient(90deg, rgba(8, 8, 7, 0.98) 0%, rgba(9, 9, 8, 0.9) 36%, rgba(9, 9, 8, 0.38) 66%, rgba(9, 9, 8, 0.1) 100%), var(--hero-image);
		background-position: center;
		background-size: cover;
	}

	.hero-overlay {
		position: absolute;
		inset: 0;
		background:
			radial-gradient(circle at 18% 20%, rgba(249, 115, 22, 0.13), transparent 34%),
			linear-gradient(180deg, rgba(0, 0, 0, 0.34), rgba(0, 0, 0, 0.68));
	}

	.hero-texture {
		position: absolute;
		inset: 0;
		opacity: 0.46;
		mix-blend-mode: multiply;
		background-size: 760px;
	}

	.hero-frame {
		position: relative;
		z-index: 1;
		padding: 54px 0 30px;
	}

	.nav-shell {
		position: sticky;
		top: 0;
		z-index: 40;
		width: 100%;
		border-bottom: 1px solid rgba(255, 255, 255, 0.1);
		background: rgba(10, 9, 8, 0.9);
		box-shadow: 0 18px 42px rgba(0, 0, 0, 0.26);
		backdrop-filter: blur(16px);
	}

	.top-nav {
		display: grid;
		grid-template-columns: auto 1fr auto;
		align-items: center;
		gap: 14px;
		min-height: 94px;
		padding: 0 12px;
	}

	.brand-lockup {
		display: grid;
		place-items: center;
		width: 142px;
		height: 88px;
		overflow: hidden;
	}

	.brand-lockup img {
		display: block;
		width: 132px;
		max-height: 86px;
		object-fit: contain;
	}

	.nav-links,
	.nav-actions {
		display: flex;
		align-items: center;
		gap: 4px;
	}

	.nav-links {
		justify-content: center;
	}

	.nav-links a {
		color: #fff;
		font-size: 0.6rem;
		font-weight: 800;
		letter-spacing: 0.04em;
		padding: 28px 7px 25px;
		text-transform: uppercase;
		transition: color 150ms ease, box-shadow 150ms ease;
	}

	.nav-links a:hover,
	.nav-links a:first-child {
		color: var(--bdr-primary);
		box-shadow: inset 0 -2px 0 var(--bdr-primary);
	}

	.btn {
		display: inline-flex;
		align-items: center;
		justify-content: center;
		gap: 8px;
		border-radius: var(--bdr-button-radius);
		font-size: 0.73rem;
		font-weight: 900;
		letter-spacing: 0.035em;
		line-height: 1;
		min-height: 44px;
		padding: 0 20px;
		text-transform: uppercase;
		transition: transform 150ms ease, filter 150ms ease, background 150ms ease;
	}

	.btn:hover {
		transform: translateY(-1px);
	}

	.btn-primary {
		background: var(--bdr-primary);
		color: #fff;
		box-shadow: 0 14px 32px rgba(249, 115, 22, 0.22);
	}

	.btn-outline {
		border: 1px solid rgba(249, 115, 22, 0.86);
		color: #fff;
		background: rgba(0, 0, 0, 0.42);
	}

	.btn-small {
		min-height: 36px;
		padding: 0 12px;
		font-size: 0.64rem;
	}

	.btn-ghost-dark {
		border: 1px solid #111;
		color: #111;
		background: transparent;
	}

	.menu-toggle,
	.mobile-menu {
		display: none;
	}

	.hero-content {
		max-width: 640px;
		padding: 74px 0 52px 38px;
	}

	.eyebrow {
		margin: 0;
		color: rgba(255, 255, 255, 0.76);
		font-size: 0.72rem;
		font-weight: 900;
		letter-spacing: 0.18em;
		text-transform: uppercase;
	}

	.eyebrow.orange {
		color: var(--bdr-primary);
	}

	.hero h1,
	.section-heading h2,
	.cta-content h2,
	.quote-benefits h2,
	.proof-layout h2 {
		font-family: var(--bdr-heading-font);
		letter-spacing: 0;
		text-transform: uppercase;
	}

	.hero h1 {
		margin: 10px 0 0;
		color: #fff;
		font-size: clamp(4.3rem, 8vw, 7.35rem);
		font-weight: 900;
		line-height: 0.88;
		max-width: 680px;
		text-shadow: 0 24px 48px rgba(0, 0, 0, 0.34);
	}

	.hero h1 span {
		display: block;
	}

	.accent-line {
		color: var(--bdr-primary);
	}

	.hero-copy {
		max-width: 470px;
		margin: 22px 0 0;
		color: rgba(255, 255, 255, 0.88);
		font-size: 0.96rem;
		line-height: 1.65;
	}

	.hero-actions {
		display: flex;
		flex-wrap: wrap;
		gap: 12px;
		margin-top: 26px;
	}

	.trust-row {
		display: grid;
		grid-template-columns: repeat(3, minmax(0, 1fr));
		gap: 24px;
		margin-top: 34px;
		padding: 0 28px 0 34px;
	}

	.trust-item {
		display: grid;
		grid-template-columns: 44px 1fr;
		gap: 12px;
		align-items: center;
		color: #fff;
		min-width: 0;
	}

	.trust-icon {
		display: grid;
		place-items: center;
		width: 44px;
		height: 44px;
		border: 1px solid rgba(255, 255, 255, 0.72);
		border-radius: 999px;
		color: #fff;
	}

	.trust-icon :global(svg) {
		display: block;
	}

	.trust-item p {
		margin: 0;
		font-size: 0.72rem;
		font-weight: 900;
		letter-spacing: 0.04em;
		text-transform: uppercase;
	}

	.trust-item span {
		display: block;
		margin-top: 4px;
		color: rgba(255, 255, 255, 0.74);
		font-size: 0.72rem;
		line-height: 1.4;
	}

	.section {
		padding: 54px 0;
	}

	#services {
		padding: 30px 0 34px;
	}

	.section-light {
		background:
			radial-gradient(circle at top, rgba(249, 115, 22, 0.08), transparent 30%),
			#f7f5f1;
		color: #121212;
	}

	.section-heading {
		max-width: 760px;
		margin-bottom: 32px;
	}

	.section-heading.centered {
		margin-right: auto;
		margin-left: auto;
		text-align: center;
	}

	.section-heading.dark {
		color: #fff;
	}

	.section-heading h2,
	.cta-content h2,
	.quote-benefits h2,
	.proof-layout h2 {
		margin: 8px 0 0;
		font-size: clamp(2rem, 4vw, 3.25rem);
		font-weight: 900;
		line-height: 0.95;
	}

	.section-heading p:not(.eyebrow) {
		margin: 9px 0 0;
		color: #555;
		font-size: 0.92rem;
	}

	#services .section-heading {
		margin-bottom: 22px;
	}

	#services .section-heading h2 {
		font-size: clamp(1.75rem, 3vw, 2.45rem);
	}

	#services .section-heading p:not(.eyebrow) {
		margin-top: 6px;
	}

	.service-grid {
		display: grid;
		grid-template-columns: repeat(6, minmax(0, 1fr));
		gap: 16px;
	}

	.service-card {
		min-height: 182px;
		border: 1px solid #d8d5cf;
		background: rgba(255, 255, 255, 0.82);
		padding: 22px 16px 18px;
		text-align: center;
	}

	.service-icon {
		display: grid;
		place-items: center;
		height: 64px;
		margin: 0 auto 18px;
	}

	.service-icon img {
		max-width: 68px;
		max-height: 60px;
		filter: brightness(0);
		object-fit: contain;
	}

	.service-card h3 {
		margin: 0;
		color: #111;
		font-size: 0.75rem;
		font-weight: 900;
		letter-spacing: 0.04em;
		text-transform: uppercase;
	}

	.service-card p {
		margin: 10px auto 0;
		max-width: 142px;
		color: #4f4f4f;
		font-size: 0.72rem;
		line-height: 1.48;
	}

	.section-action {
		display: flex;
		justify-content: center;
		margin-top: 24px;
	}

	.section-action .btn {
		min-height: 34px;
		border-color: #111;
		border-radius: 0.12rem;
		padding: 0 18px;
		font-size: 0.67rem;
	}

	.process-section {
		position: relative;
		overflow: hidden;
		background:
			linear-gradient(90deg, rgba(8, 8, 8, 0.96), rgba(8, 8, 8, 0.9)),
			url('/clientFiles/assets/grain-overlay-texture.svg');
		border-top: 1px solid rgba(255, 255, 255, 0.06);
		border-bottom: 1px solid rgba(255, 255, 255, 0.06);
		color: #fff;
		padding: 34px 0 42px;
	}

	.process-section::before {
		position: absolute;
		inset: 0;
		background:
			radial-gradient(circle at 12% 20%, rgba(249, 115, 22, 0.1), transparent 28%),
			linear-gradient(180deg, rgba(255, 255, 255, 0.03), transparent 44%);
		content: '';
		pointer-events: none;
	}

	.process-section .site-frame {
		position: relative;
		z-index: 1;
	}

	.process-section .section-heading {
		margin-bottom: 24px;
	}

	.process-section .section-heading h2 {
		font-size: clamp(1.7rem, 3.4vw, 2.55rem);
	}

	.process-grid {
		display: grid;
		grid-template-columns: repeat(3, minmax(0, 1fr));
		gap: 32px;
		margin-top: 0;
	}

	.process-step {
		display: grid;
		grid-template-columns: 44px 56px 1fr;
		gap: 16px;
		align-items: center;
		min-width: 0;
	}

	.step-number {
		color: var(--bdr-primary);
		font-family: var(--bdr-heading-font);
		font-size: 2.35rem;
		font-weight: 900;
		line-height: 1;
	}

	.process-icon {
		display: grid;
		place-items: center;
		width: 56px;
		height: 56px;
		color: #fff;
	}

	.process-icon :global(svg) {
		display: block;
	}

	.process-step h3 {
		margin: 0;
		font-size: 0.86rem;
		font-weight: 900;
		letter-spacing: 0.05em;
		text-transform: uppercase;
	}

	.process-step p {
		margin: 7px 0 0;
		color: rgba(255, 255, 255, 0.76);
		font-size: 0.78rem;
		line-height: 1.55;
	}

	.proof-section {
		padding: 38px 0;
	}

	.proof-layout {
		display: grid;
		grid-template-columns: 0.9fr 1.35fr;
		gap: 34px;
		align-items: center;
	}

	.proof-layout h2 {
		color: #111;
		font-size: clamp(1.8rem, 3vw, 2.55rem);
	}

	.proof-layout p {
		max-width: 480px;
		margin: 12px 0 0;
		color: #555;
		font-size: 0.9rem;
		line-height: 1.6;
	}

	.proof-grid {
		display: grid;
		grid-template-columns: repeat(2, minmax(0, 1fr));
		gap: 12px;
	}

	.proof-card {
		display: grid;
		grid-template-columns: 30px 1fr;
		gap: 12px;
		align-items: center;
		border: 1px solid #d9d5cf;
		background: rgba(255, 255, 255, 0.78);
		padding: 14px 16px;
	}

	.proof-card :global(svg) {
		color: var(--bdr-primary);
	}

	.proof-card p {
		margin: 0;
		color: #191919;
		font-size: 0.76rem;
		font-weight: 900;
		letter-spacing: 0.03em;
		line-height: 1.35;
		text-transform: uppercase;
	}

	.projects-section {
		background:
			linear-gradient(rgba(10, 10, 9, 0.88), rgba(10, 10, 9, 0.9)),
			url('/clientFiles/assets/grain-overlay-texture.svg');
		color: #fff;
		padding: 40px 0 48px;
	}

	.projects-section .section-heading {
		margin-bottom: 24px;
	}

	.projects-section .section-heading h2 {
		font-size: clamp(1.7rem, 3.2vw, 2.55rem);
	}

	.projects-section .section-heading p:not(.eyebrow) {
		color: rgba(255, 255, 255, 0.68);
	}

	.project-grid {
		display: grid;
		grid-template-columns: repeat(3, minmax(0, 1fr));
		gap: 16px;
	}

	.project-card {
		border: 1px solid rgba(255, 255, 255, 0.16);
		background:
			linear-gradient(180deg, rgba(255, 255, 255, 0.08), rgba(255, 255, 255, 0.03)),
			rgba(16, 16, 15, 0.92);
		padding: 22px 20px;
	}

	.project-card h3 {
		margin: 0;
		font-size: 0.86rem;
		font-weight: 900;
		letter-spacing: 0.05em;
		text-transform: uppercase;
	}

	.project-card p {
		margin: 10px 0 0;
		color: rgba(255, 255, 255, 0.72);
		font-size: 0.78rem;
		line-height: 1.6;
	}

	.cta-band {
		position: relative;
		overflow: hidden;
		background-image: linear-gradient(90deg, rgba(10, 10, 9, 0.9), rgba(10, 10, 9, 0.76)), var(--cta-image);
		background-position: center;
		background-size: cover;
		color: #fff;
	}

	.cta-content {
		max-width: 760px;
		padding: 30px 0;
	}

	.cta-content p:not(.eyebrow) {
		max-width: 520px;
		color: rgba(255, 255, 255, 0.78);
		font-size: 0.95rem;
		line-height: 1.6;
	}

	.quote-layout {
		display: grid;
		grid-template-columns: 0.9fr 1.55fr;
		gap: 38px;
		align-items: start;
	}

	.quote-benefits h2 {
		color: #171717;
		font-size: clamp(1.55rem, 2.8vw, 2.35rem);
	}

	.quote-benefits ul {
		display: grid;
		gap: 12px;
		margin: 24px 0 0;
		padding: 0;
		list-style: none;
	}

	.quote-benefits li {
		display: grid;
		grid-template-columns: 24px 1fr;
		gap: 10px;
		align-items: center;
		color: #555;
		font-size: 0.86rem;
		line-height: 1.45;
	}

	.quote-benefits img {
		width: 20px;
		height: 20px;
		object-fit: contain;
	}

	.quote-form {
		display: grid;
		gap: 18px;
	}

	.form-grid {
		display: grid;
		grid-template-columns: repeat(3, minmax(0, 1fr));
		gap: 14px;
	}

	.field {
		display: grid;
		gap: 6px;
	}

	.field.full {
		grid-column: 1 / -1;
	}

	.field label {
		color: #171717;
		font-size: 0.66rem;
		font-weight: 900;
		letter-spacing: 0.04em;
	}

	.field input,
	.field select,
	.field textarea {
		width: 100%;
		border: 1px solid #d7d2ca;
		background: #fff;
		color: #171717;
		font: inherit;
		font-size: 0.82rem;
		min-height: 42px;
		outline: none;
		padding: 0 12px;
	}

	.field textarea {
		min-height: 116px;
		padding: 12px;
		resize: vertical;
	}

	.field input:focus,
	.field select:focus,
	.field textarea:focus {
		border-color: var(--bdr-primary);
		box-shadow: 0 0 0 2px rgba(249, 115, 22, 0.16);
	}

	.field-error,
	.form-message.error {
		color: #b91c1c;
	}

	.form-message {
		padding: 12px 14px;
		font-size: 0.84rem;
		font-weight: 700;
	}

	.form-message.success {
		background: #dcfce7;
		color: #166534;
	}

	.form-message.error {
		background: #fee2e2;
	}

	.submit-button {
		justify-self: start;
		min-width: 210px;
	}

	.site-footer {
		background: #101110;
		color: #fff;
		padding: 30px 0 18px;
	}

	.footer-grid {
		display: grid;
		grid-template-columns: 1.25fr 0.8fr 0.8fr 1fr;
		gap: 42px;
		padding-bottom: 26px;
	}

	.footer-logo {
		width: 132px;
		height: auto;
	}

	.site-footer h3 {
		margin: 0 0 12px;
		color: #fff;
		font-size: 0.72rem;
		font-weight: 900;
		letter-spacing: 0.08em;
		text-transform: uppercase;
	}

	.site-footer p,
	.site-footer a {
		display: block;
		margin: 0;
		color: rgba(255, 255, 255, 0.68);
		font-size: 0.78rem;
		line-height: 1.65;
	}

	.site-footer a:hover {
		color: #fff;
	}

	.footer-bottom {
		display: flex;
		justify-content: space-between;
		gap: 16px;
		border-top: 1px solid rgba(255, 255, 255, 0.09);
		padding-top: 16px;
	}

	.footer-bottom div {
		display: flex;
		flex-wrap: wrap;
		gap: 14px;
	}

	@media (max-width: 840px) {
		.top-nav {
			grid-template-columns: auto 1fr auto;
		}

		.nav-links,
		.nav-actions {
			display: none;
		}

		.menu-toggle {
			display: inline-flex;
			justify-self: end;
			border: 1px solid rgba(255, 255, 255, 0.18);
			background: rgba(0, 0, 0, 0.44);
			color: #fff;
			font-size: 0.72rem;
			font-weight: 900;
			padding: 10px 14px;
			text-transform: uppercase;
		}

		.mobile-menu {
			display: grid;
			gap: 8px;
			border-top: 1px solid rgba(255, 255, 255, 0.08);
			background: rgba(10, 9, 8, 0.94);
			padding: 14px 12px 16px;
		}

		.mobile-menu a:not(.btn) {
			color: #fff;
			font-size: 0.82rem;
			font-weight: 800;
			padding: 10px 0;
			text-transform: uppercase;
		}

		.hero-content {
			padding-left: 0;
		}

		.trust-row,
		.process-grid,
		.proof-layout,
		.project-grid,
		.quote-layout,
		.footer-grid {
			grid-template-columns: 1fr;
		}

		.service-grid {
			grid-template-columns: repeat(2, minmax(0, 1fr));
		}

		.form-grid {
			grid-template-columns: 1fr;
		}
	}

	@media (max-width: 640px) {
		.site-frame {
			width: min(100vw - 24px, 1180px);
		}

		.hero {
			min-height: auto;
		}

		.brand-lockup img {
			width: 108px;
			max-height: 74px;
		}

		.hero h1 {
			font-size: clamp(3.2rem, 18vw, 4.8rem);
		}

		.trust-row {
			gap: 14px;
			padding: 0;
		}

		.service-grid {
			grid-template-columns: 1fr;
		}

		.process-step {
			grid-template-columns: auto 1fr;
		}

		.process-icon {
			display: none;
		}

		.footer-bottom {
			flex-direction: column;
		}
	}
</style>
