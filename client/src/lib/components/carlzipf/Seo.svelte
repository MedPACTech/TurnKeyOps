<script lang="ts">
 import type { CarlZipfSiteContent } from '$lib/carlzipf-site-content';
 let { content, origin, base, residential = false }: { content: CarlZipfSiteContent; origin: string; base: string; residential?: boolean } = $props();
 const seo = $derived(residential ? content.residentialSeo : content.commercialSeo);
 const canonical = $derived(origin + (base || '') + (residential ? '/residential' : base ? '' : '/'));
 const entity = $derived(origin + base + '/#business');
 const schema = $derived(JSON.stringify({ '@context': 'https://schema.org', '@graph': [
  { '@type': 'Locksmith', '@id': entity, name: 'Carl Zipf Lock Shop', foundingDate: '1911', telephone: '+1-614-299-7303', url: origin + base + '/', address: { '@type': 'PostalAddress', streetAddress: '161 East Fifth Avenue', addressLocality: 'Columbus', addressRegion: 'OH', postalCode: '43201', addressCountry: 'US' } },
  { '@type': 'WebSite', '@id': origin + base + '/#website', url: origin + base + '/', name: 'Carl Zipf Lock Shop', publisher: { '@id': entity } },
  { '@type': 'WebPage', '@id': canonical + '#page', url: canonical, name: seo.title, description: seo.description, about: { '@id': entity } }
 ] }).replace(/</g, '\\u003c'));
</script>
<svelte:head>
 <title>{seo.title}</title><meta name="description" content={seo.description} /><link rel="canonical" href={canonical} />
 <meta name="robots" content="index,follow" /><meta property="og:type" content="website" /><meta property="og:site_name" content="Carl Zipf Lock Shop" /><meta property="og:title" content={seo.title} /><meta property="og:description" content={seo.description} /><meta property="og:url" content={canonical} /><meta property="og:image" content={origin + (residential ? '/carlzipf/logo.jpg' : content.images.hero)} /><meta name="twitter:card" content="summary_large_image" />
 {@html `<script type="application/ld+json">${schema}</script>`}
</svelte:head>
