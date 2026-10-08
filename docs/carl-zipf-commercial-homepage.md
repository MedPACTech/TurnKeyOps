# Carl Zipf commercial homepage implementation

## Implemented locally

One SvelteKit application retains the existing Carl Zipf residential page as a component and adds the supplied commercial design. BDR remains a separate client: its domain, pages, public content and intake endpoint are unchanged by this work. No production deployment or CMS record mutation was performed.

| Surface | Carl Zipf domain | Platform/local preview |
| --- | --- | --- |
| Commercial homepage | `/` | `/carlzipf/public` |
| Preserved residential homepage | `/residential` | `/carlzipf/public/residential` |
| Website editor | Existing authenticated tenant admin | `/carlzipf/admin/website` |
| Sitemap | `/sitemap.xml` | `/carlzipf/public/sitemap.xml` |

Domain routing uses `carlzipflockshop.com`, recorded in the existing contractor scope and logo provenance. The `www` domain and tenant-prefixed public aliases redirect to the canonical domain and clean paths. The platform root and BDR domain retain their existing routing. Deployment/DNS configuration and the Node adapter’s HTTPS origin/proxy configuration must be checked separately before launch. Local HTTP form checks against the build used an explicit local `ORIGIN` so CSRF checks matched the browser origin.

## Residential preservation

The starting working-copy page, including the existing project portal link, was copied to `Residential.svelte`. Its markup, styling, service options, validation, attachment limits, assessment/callback controls and submission infrastructure remain in place. Static copy now reads from defaults through the existing content API; defaults preserve that wording. Only homepage/self links and metadata change for the new residential route. The logo links back to commercial; no duplicate residential page remains at the old homepage route. There were no separate residential service-page routes in this implementation to migrate.

## Commercial implementation and intake

The supplied charcoal/white/orange design is implemented as a Svelte component, using the existing official logo, bundled images and installed Lucide icons. Sections include navigation, hero, capability highlights, services, industry solutions, planning/source/installation steps, residential promotion, history/trust, quote intake and footer. Images have fixed dimensions where applicable and below-the-fold service images load lazily. Primary content is server-rendered; mobile navigation and form enhancement use Svelte.

The form reuses the existing Carl Zipf public quote action, idempotent submission ID, honeypot, validation, attachment handling and error recovery. It captures contact/company, email/phone, location, commercial project type, description and timeline. Requests use `/api/public/quote-requests/carlzipf` and carry `propertyType: commercial`; residential requests keep their existing controls. No BDR request is created. Plans/drawings can currently be attached as JPG, PNG or WebP images. PDF/CAD uploads are not supported by the existing attachment pathway; the form asks customers to arrange PDF delivery with the shop.

## CMS

Carl Zipf uses its existing tenant settings API and tenant ID `88888888-8888-4888-8888-888888888883`. The existing public-content document contains a `carlZipfSite` namespace. Other document properties are preserved on update. The existing API requires navigation/hero/services/quoteForm/footer objects; empty compatibility objects are provided only when absent. No new storage backend, CMS service, dependencies or tenant records are introduced.

The authenticated editor is linked from Carl Zipf settings. It provides labeled controls for commercial text and CTA targets, images, industry/process cards, residential text and separate commercial/residential SEO metadata. It includes unpublished service-page draft outlines for review; no thin public service pages were added. The public-content document is public: draft fields must never contain private information. Existing settings read/write permissions, tenant admin checks and API authorization apply. Version checks reject stale saves, including the first save of an empty settings document. Unsafe image/CTA URLs are rejected through normalization. Residential promotional links remain fixed to the correct residential route.

Service card copy/images are editable in the existing three-card presentation. Reordering or adding arbitrary service-card blocks and a general image upload library are not included. Residential service-option descriptions and form field definitions remain in the preserved component, while its static surrounding copy is editable. Business identity JSON-LD and address/phone markup retain the existing reviewed facts; changes to these require updating the shared implementation consistently.

## SEO and GEO

Commercial and residential pages have distinct titles/descriptions, canonical URLs, Open Graph fields, sharing imagery, Twitter card metadata and index/follow directives. Important service explanations and headings are present in rendered HTML. Geographic language is limited to Columbus rather than asserting statewide service coverage.

JSON-LD includes Locksmith (a LocalBusiness subtype), WebSite and WebPage entities with stable business identifiers, the existing shop address/telephone and founding year. No ratings, testimonials, awards, customer counts or specific projects are invented. No FAQ schema or rich-result/AI citation promises are made.

A two-page XML sitemap lists the canonical commercial and residential paths. Only the Carl Zipf domain gets the tenant-specific robots response in the server robots route; other domains receive the previous shared robots content unchanged. Legitimate search crawlers, including AI search crawlers, inherit the allow rule. No new training-crawler restriction or exception is introduced: training and search policy choices should be reviewed independently with the client. Admin, tech, portal and estimate paths are excluded from crawling; authentication remains enforced. No experimental llms.txt is added.

## Verification

- `npm --prefix client run check`: 0 errors, 0 warnings.
- `npm --prefix client run build`: production build and adapter-node output pass.
- `npm --prefix client run test:carlzipf`: 26 tests pass.
- `npm --prefix client run test:session`: 17 session/domain/access tests and 2 quote-tenant tests pass.
- `node client/tests/carlzipf-content.integration.mjs`: local API fixtures verify CMS first save/reload, preservation of existing settings, version conflicts, unsafe URL normalization, and commercial endpoint/payload/receipt routing.
- `node client/tests/carlzipf-commercial.integration.mjs`: local browser against both the development server and production build verifies commercial/residential rendering, residential links, mobile menu, mobile overflow, metadata/JSON-LD, one H1, form validation and sitemap. Desktop/mobile screenshots were visually inspected.
- `node client/tests/carlzipf-domain.integration.mjs`: built-app host-header smoke checks cover the clean Carl Zipf routes, sitemap, robots, alias redirects and separate BDR homepage.
- Existing real-server intake scenarios were updated to use the preserved residential form. They require a configured local API/Azurite and were not run against durable storage in this session. CMS persistence and successful lead submission were verified with fixtures, not production credentials or live records.
- No dedicated lint command exists in the client package. Svelte diagnostics and `git diff --check` were run.

## Launch requirements and client verification

1. Replace the supplied AI-generated concept crops with approved company photography. The ZIP explicitly identifies them as prototype imagery. The preview discloses this visibly, and image alt text describes concepts. Remove or revise the disclosure only after approved replacements exist. Some supplied backgrounds contain baked-in text; CSS frames the image portions to avoid competing text.
2. Confirm the existing address, telephone, business hours, founding year and exact commercial service coverage. Existing application content and the client-provided task are the factual sources; no external business verification was performed.
3. Review electronic-access scope, specialty sourcing and new-construction capabilities before launch. Review service drafts for useful project-specific information before creating public detail pages.
4. Confirm production domain/DNS mapping, authorized CMS save/reload and end-to-end lead/attachment delivery against the configured API. Existing authentication, CSRF controls and tenant isolation have not been weakened.
5. Review legal/privacy copy and analytics requirements; no new analytics service or unsupported marketing claims were introduced. Production deployment requires separate authorization.

## Files created or changed

- `client/src/lib/carlzipf-site-content.ts`: typed tenant content defaults.
- `client/src/lib/server/carlzipf-site-content.ts`: existing API loading and content normalization.
- `client/src/lib/components/carlzipf/{Commercial,CommercialForm,Residential,Seo}.svelte`: public presentation, preserved form and metadata.
- `client/src/routes/carlzipf/public/+page.{svelte,server.ts}` and `residential/+page.{svelte,server.ts}`: page mapping and shared intake.
- `client/src/routes/carlzipf/admin/website/+page.{svelte,server.ts}` and `admin/settings/+page.svelte`: tenant editor and entry link.
- `client/src/lib/config/domains.ts`, `client/src/routes/robots.txt/+server.ts`: Carl Zipf domain/alias handling and scoped robots response.
- `client/src/routes/sitemap.xml/+server.ts`, `client/src/routes/carlzipf/public/sitemap.xml/+server.ts`: sitemap. The old `client/static/robots.txt` is replaced by the server route because adapter-node serves static assets before hooks.
- `client/static/carlzipf/commercial/*.jpg`: six supplied concept assets.
- `client/tests/{domain-routing.test.ts,carlzipf-public.integration.mjs,carlzipf-commercial.integration.mjs,carlzipf-content.integration.mjs,carlzipf-domain.integration.mjs}`: routing, preservation, browser and API-fixture verification.
- This completion report.
