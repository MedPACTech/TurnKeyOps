<script lang="ts">
import { Building2, Fingerprint, DoorOpen, PackageSearch } from 'lucide-svelte';
import CommercialForm from './CommercialForm.svelte';
import type { PageData, ActionData } from '../../../routes/carlzipf/public/$types';
let { data, form }: { data: PageData; form: ActionData } = $props();
let menuOpen = $state(false);
const content = $derived(data.content);
const residentialPath = $derived(data.publicBase + '/residential');
</script>
<div class="commercial">
  <a class="skip-link" href="#main">{content.text["skip_to_content"]}</a>
  <div class="topbar"><div class="container topbar__inner"><span>{content.text["ohio_s_lock_doctor_since_1911"]}</span><span class="topbar__right">{content.text["established_in_columbus"]}</span></div></div>
  <header class="site-header" id="top">
    <div class="container nav">
      <a class="wordmark" href="#top" aria-label="Carl Zipf Lock Shop home"><img class="official-logo" src="/carlzipf/logo.jpg" width="210" height="71" alt="Carl Zipf Lock Shop, established 1911" /></a>
      <button class="nav-toggle" type="button" aria-label="Toggle navigation" aria-expanded={menuOpen} onclick={() => menuOpen = !menuOpen} aria-controls="main-navigation"><span></span><span></span><span></span></button>
      <nav class="nav-links" class:is-open={menuOpen} id="main-navigation" aria-label="Main navigation">
        <a href={content.links.capabilities}>{content.text["commercial_solutions"]}</a><a href={content.links.services}>{content.text["hardware_products"]}</a><a href={content.links.about}>{content.text["about"]}</a><a href={content.links.quote}>{content.text["contact"]}</a>
        <a class="btn btn--residential" data-residential-link href={residentialPath}>⌂ <span>{content.text["residential_services"]}</span></a>
        <a class="btn btn--primary nav-quote" href={content.links.quote}>{content.text["request_a_quote"]}<span aria-hidden="true">→</span></a>
      </nav>
    </div>
  </header>
  <main id="main">
    <section class="hero" aria-labelledby="hero-heading"><div class="hero__photo" style:background-image={`url("${content.images.hero}")`} role="img" aria-label="Concept illustration of commercial door hardware installation"></div><div class="hero__shade"></div><div class="container hero__content">
      <p class="eyebrow">{content.text["commercial_door_hardware_security_solutions"]}</p>
      <h1 id="hero-heading">{content.text["more_than_locks"]}<br/><em>{content.text["complete_opening_solutions"]}</em></h1>
      <p class="hero__lede">{content.text["commercial_hardware_full_installations_electronic_locking_and_har"]}</p>
      <div class="button-row"><a class="btn btn--primary" href={content.links.quote}>{content.text["discuss_a_commercial_project"]}<span aria-hidden="true">→</span></a><a class="btn btn--outline-light" href={content.links.capabilities}>{content.text["explore_capabilities"]}</a></div>
      <p class="imagery-note">{content.imageryNote}</p><div class="hero__proof"><span>{content.text["experienced_since_1911"]}</span><span>{content.text["serving_columbus_businesses"]}</span><span>{content.text["more_than_locks20"]}</span></div>
    </div></section>
    <section class="feature-strip" aria-label="Key specialties"><div class="container feature-strip__grid"><a href={content.links.services}><span class="feature-icon"><Building2 size={28} aria-hidden="true" /></span><span><strong>{content.text["complete_commercial_installations"]}</strong><small>{content.text["from_single_doors_to_entire_facilities"]}</small></span></a><a href={content.links.services}><span class="feature-icon"><Fingerprint size={28} aria-hidden="true" /></span><span><strong>{content.text["electronic_locks_access"]}</strong><small>{content.text["modern_security_for_modern_buildings"]}</small></span></a><a href={content.links.services}><span class="feature-icon"><DoorOpen size={28} aria-hidden="true" /></span><span><strong>{content.text["architectural_door_hardware"]}</strong><small>{content.text["specification_supply_and_installation"]}</small></span></a><a href={content.links.services}><span class="feature-icon"><PackageSearch size={28} aria-hidden="true" /></span><span><strong>{content.text["hard_to_find_hardware"]}</strong><small>{content.text["specialty_and_replacement_parts"]}</small></span></a></div></section>
    <section class="section services" id="capabilities"><div class="container"><div class="section-heading"><p class="eyebrow">{content.text["our_commercial_capabilities"]}</p><h2>{content.text["from_single_openings_to_complete_facilities"]}</h2><p>{content.text["we_partner_with_general_contractors_facility_managers_property_ow"]}</p></div><div class="service-grid" id="services">
      <article class="service-card"><img src={content.images["installation"]} width="640" height="426" alt="Concept illustration of exit hardware installation on a commercial door" loading="lazy"/><div class="service-card__body"><h3>{content.text["commercial_installations"]}</h3><p>{content.text["complete_door_hardware_installation_for_new_construction_renovati"]}</p><a href={content.links.quote}>{content.text["discuss_this_service"]}<span aria-hidden="true">→</span></a></div></article>
      <article class="service-card"><img src={content.images["access-control"]} width="640" height="426" alt="Concept illustration of an electronic card reader at a doorway" loading="lazy"/><div class="service-card__body"><h3>{content.text["electronic_access_locking"]}</h3><p>{content.text["electronic_locks_keypads_card_reader_hardware_and_security_upgrad"]}</p><a href={content.links.quote}>{content.text["discuss_this_service"]}<span aria-hidden="true">→</span></a></div></article>
      <article class="service-card"><img src={content.images["hardware"]} width="640" height="426" alt="Concept illustration of specialty commercial door hardware" loading="lazy"/><div class="service-card__body"><h3>{content.text["specialty_hardware_sourcing"]}</h3><p>{content.text["looking_for_something_specific_let_us_help_find_hard_to_source_or"]}</p><a href={content.links.quote}>{content.text["discuss_this_service"]}<span aria-hidden="true">→</span></a></div></article>
    </div></div></section>
    <section class="section"><div class="container"><h2>{content.text["industry_heading"]}</h2><div class="service-grid">{#each content.industries as item}<article class="service-card"><div class="service-card__body"><h3>{item.title}</h3><p>{item.description}</p></div></article>{/each}</div></div></section>
<section class="section"><div class="container"><h2>{content.text["process_heading"]}</h2><div class="service-grid">{#each content.process as item, i}<article><p class="eyebrow">0{i + 1}</p><h3>{item.title}</h3><p>{item.description}</p></article>{/each}</div></div></section><section class="split-panel" id="why-us"><div class="split-panel__commercial" style:--panel-image={`url("${content.images.commercial}")`}><div class="split-panel__overlay"><p class="eyebrow eyebrow--white">{content.text["trusted_for_commercial_projects"]}</p><h2>{content.text["real_projects"]}<br/>{content.text["real_partnerships"]}</h2><p>{content.text["from_commercial_retrofits_to_new_facilities_we_help_teams_plan_so"]}</p><a class="btn btn--primary" href={content.links.quote}>{content.text["talk_about_your_project"]}<span aria-hidden="true">→</span></a></div></div><div class="split-panel__residential" style:--panel-image={`url("${content.images.residential}")`}><div class="split-panel__residential-inner"><p class="eyebrow">{content.text["home_security_matters_too"]}</p><h2>{content.text["here_for_homeowners_too"]}</h2><p>{content.text["the_same_trusted_name_for_residential_locking_and_hardware_needs"]}</p><a class="btn btn--outline-dark" data-residential-link href={residentialPath}>{content.text["explore_residential_services"]}<span aria-hidden="true">→</span></a></div></div></section>
    <section class="section trust"><div class="container trust__grid"><div><p class="eyebrow">{content.text["why_carl_zipf"]}</p><h2>{content.text["experience_you_can_build_on"]}</h2></div><div class="trust__items"><div><strong>{content.text["since_1911"]}</strong><span>{content.text["a_history_of_service"]}</span></div><div><strong>{content.text["commercial_focus"]}</strong><span>{content.text["full_opening_solutions"]}</span></div><div><strong>{content.text["residential_care"]}</strong><span>{content.text["serving_homeowners_too"]}</span></div></div></div></section>
    <section class="quote-section" id="quote"><div class="container quote-section__grid"><div><p class="eyebrow">{content.text["start_a_conversation"]}</p><h2>{content.text["let_s_talk_about_your_next_project"]}</h2><p>{content.text["tell_us_what_you_need_whether_it_s_a_full_installation_electronic"]}</p><p>{content.text["161_east_fifth_avenue_columbus_ohio_43201"]}<br/><a href="tel:+16142997303">{content.text["614_299_7303"]}</a><br/>{content.text["monday_friday_7_30_am_4_30_pm"]}</p></div><CommercialForm {data} {form} /></div></section>
  </main>
  <footer class="footer"><div class="container footer__grid"><div><a class="wordmark wordmark--footer" href="#top"><img class="official-logo" src="/carlzipf/logo.jpg" width="210" height="71" alt="Carl Zipf Lock Shop, established 1911" /></a><p>{content.text["ohio_s_lock_doctor_since_1911"]}</p></div><div><strong>{content.text["explore"]}</strong><a href={content.links.capabilities}>{content.text["commercial_solutions"]}</a><a href={content.links.services}>{content.text["hardware_products"]}</a><a data-residential-link href={residentialPath}>{content.text["residential_services"]}</a></div><div><strong>{content.text["get_in_touch"]}</strong><a href={content.links.quote}>{content.text["request_a_commercial_quote"]}</a><a href={content.links.about}>{content.text["about_carl_zipf"]}</a></div></div><div class="container footer__bottom"><span>© {new Date().getFullYear()} Carl Zipf Lock Shop.</span><span>{content.text["commercial_and_residential_security_solutions"]}</span></div></footer>
</div>
<style>
.official-logo{display:block;width:210px;height:auto}.imagery-note{font-size:12px;color:#e3e9eb;max-width:600px;margin-top:24px}
.commercial{--ink:#142129;--ink-2:#1b2a32;--accent:#b83e17;--accent-hover:#bf4822;--text:#1d252a;--muted:#58636c;--white:#fff;--surface:#f6f7f7;--line:#e5e8e9;--max:1200px;--radius:4px}
*{box-sizing:border-box}
.commercial{margin:0;font-family:Inter,"Segoe UI",Arial,sans-serif;color:var(--text);background:#fff;line-height:1.55}
a{color:inherit;text-decoration:none}
button{font:inherit}
button,a{touch-action:manipulation}
a:focus-visible,button:focus-visible{outline:3px solid #ff9b70;outline-offset:3px}
.container{max-width:var(--max);margin:auto;padding-inline:30px}
.skip-link{position:absolute;left:10px;top:-100px;background:#fff;padding:10px;z-index:10}
.skip-link:focus{top:10px}
.topbar{background:var(--ink);color:#eff3f4;font-size:12px}
.topbar__inner{display:flex;justify-content:space-between;padding-block:8px}
.topbar__right{display:flex;gap:12px}
.site-header{position:relative;background:#fff;z-index:5;box-shadow:0 2px 8px #00000008}
.nav{min-height:87px;display:flex;align-items:center;justify-content:space-between;gap:35px}
.wordmark{display:grid;grid-template-columns:auto auto;grid-template-rows:auto auto;align-items:center;column-gap:13px;white-space:nowrap;line-height:1}




.nav-links{display:flex;align-items:center;gap:21px;font-size:12px;font-weight:700}
.nav-links>a:not(.btn):hover,.footer a:hover{color:var(--accent)}
.btn{display:inline-flex;justify-content:center;align-items:center;gap:19px;min-height:45px;padding:12px 18px;border-radius:var(--radius);border:1px solid transparent;font-size:12px;font-weight:750;transition:background .2s,color .2s,transform .2s;cursor:pointer;text-align:center}
.btn:hover{transform:translateY(-1px)}
.btn--primary{background:var(--accent);color:#fff}
.btn--primary:hover{background:var(--accent-hover)}
.btn--residential{border-color:#59616a;background:#fff;color:var(--ink);white-space:nowrap}
.btn--residential:hover,.btn--outline-dark:hover{background:#e9eef0}
.btn--outline-light{border-color:#c9d0d3;color:white;background:#14212966}
.btn--outline-light:hover{background:#ffffff22}
.btn--outline-dark{border-color:#56606a;color:var(--ink);background:#ffffffd9}
.nav-toggle{display:none}
.hero{isolation:isolate;min-height:540px;background:var(--ink);color:#fff;position:relative;display:flex;align-items:center;overflow:hidden}
.hero__photo{position:absolute;inset:0 0 0 43%;background-image:url('/carlzipf/commercial/hero-commercial.jpg');background-repeat:no-repeat;background-size:cover;background-position:center 45%;z-index:-2}
.hero__shade{position:absolute;inset:0;background:linear-gradient(90deg,#101b22 0%,#101b22 24%,#101b22ef 42%,#101b2299 57%,#101b220d 86%);z-index:-1}
.hero__content{width:100%;padding-top:70px;padding-bottom:55px}
.eyebrow{text-transform:uppercase;color:var(--accent);font-weight:800;letter-spacing:.15em;font-size:11px;margin:0 0 14px}
.eyebrow--white{color:#fff}
.hero h1{max-width:660px;font-size:clamp(39px,5vw,68px);letter-spacing:-.04em;line-height:1.05;margin:0 0 20px;font-weight:850}
.hero h1 em{font-style:normal;color:#f07448}
.hero__lede{font-size:16px;line-height:1.55;max-width:480px;margin:0 0 22px;color:#f1f4f5}
.button-row{display:flex;flex-wrap:wrap;gap:12px}
.hero__proof{display:flex;gap:25px;flex-wrap:wrap;margin-top:45px;font-size:12px;font-weight:650;color:#e6eced}
.feature-strip{background:var(--ink-2);color:#fff}
.feature-strip__grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));padding-block:23px}
.feature-strip__grid a{display:flex;align-items:center;gap:13px;padding:4px 18px;border-right:1px solid #ffffff33;min-width:0}
.feature-strip__grid a:first-child{padding-left:0}
.feature-strip__grid a:last-child{border:0;padding-right:0}
.feature-strip strong{display:block;font-size:13px;line-height:1.3}
.feature-strip small{display:block;font-size:11px;color:#d7dddf;margin-top:6px}
.feature-icon{font-size:36px;flex-shrink:0;line-height:1}
.section{padding-block:72px}
.section-heading{text-align:center;max-width:930px;margin:0 auto 37px}
.section h2,.trust h2,.quote-section h2{font:normal clamp(31px,3.5vw,45px)/1.16 Georgia,serif;letter-spacing:-.025em;margin:0 0 12px}
.section-heading>p:last-child{max-width:730px;margin:auto;color:var(--muted)}
.service-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:26px}
.service-card{background:#fff}
.service-card img{display:block;width:100%;aspect-ratio:1.85;object-fit:cover}
.service-card__body{padding:12px 0}
.service-card h3{font:normal 23px Georgia,serif;margin:0 0 5px}
.service-card p{font-size:14px;color:var(--muted);margin:0 0 10px}
.service-card a{font-size:13px;font-weight:750;color:var(--accent)}
.split-panel{display:grid;grid-template-columns:1.55fr 1fr;min-height:360px}
.split-panel__commercial{background:linear-gradient(90deg,#111e27e8,#111e278a),var(--panel-image) right center/auto 260% no-repeat;color:white;display:flex;align-items:center}
.split-panel__overlay{max-width:630px;padding:54px max(30px,calc((100vw - 1200px)/2 + 30px)) 54px max(30px,calc((100vw - 1200px)/2 + 30px));padding-right:35px}
.split-panel h2{font:normal clamp(29px,3vw,42px)/1.13 Georgia,serif;margin:0 0 15px}
.split-panel p:not(.eyebrow){max-width:460px;font-size:14px;margin:0 0 20px}
.split-panel__residential{background:linear-gradient(90deg,#fffffff2,#ffffffaa),var(--panel-image) right center/auto 260% no-repeat;display:flex;align-items:center}
.split-panel__residential-inner{padding:45px}
.trust{background:var(--surface)}
.trust__grid{display:grid;grid-template-columns:1fr 2fr;gap:40px;align-items:center}
.trust h2{margin:0}
.trust__items{display:grid;grid-template-columns:repeat(3,1fr);gap:15px}
.trust__items div{padding-left:18px;border-left:2px solid var(--accent)}
.trust__items strong,.trust__items span{display:block}
.trust__items strong{font:normal 21px Georgia,serif}
.trust__items span{color:var(--muted);font-size:13px}
.quote-section{background:var(--ink);color:#fff;padding-block:75px}
.quote-section__grid{display:grid;grid-template-columns:1fr 1.2fr;gap:65px}
.quote-section h2{font-size:clamp(34px,3.4vw,46px)}
.quote-section__grid>div>p:not(.eyebrow){color:#d6dfe3}

.footer{background:#f8f9f9;padding-top:45px}
.footer__grid{display:grid;grid-template-columns:2fr 1fr 1fr;gap:35px;padding-bottom:45px}
.footer__grid>div{display:flex;flex-direction:column;align-items:flex-start;gap:10px}
.footer__grid p{font-size:12px;color:var(--muted)}
.footer__grid strong{font-size:13px}
.footer__grid a:not(.wordmark){font-size:13px;color:#4e5960}
.footer__bottom{border-top:1px solid var(--line);padding-block:20px;display:flex;justify-content:space-between;gap:15px;color:#6c757b;font-size:11px}
@media(max-width:1120px){.nav{gap:15px}
.nav-links{gap:11px}

.nav-links{font-size:11px}
.nav-links .btn{padding:10px}
.feature-strip__grid{grid-template-columns:repeat(2,1fr);row-gap:15px}
.feature-strip__grid a:nth-child(2){border:0}
}
@media(max-width:880px){.nav{min-height:74px}
.nav-toggle{display:flex;flex-direction:column;justify-content:center;gap:5px;padding:12px;background:transparent;border:1px solid #ddd;border-radius:4px;cursor:pointer}
.nav-toggle span{display:block;width:22px;height:2px;background:var(--ink)}
.nav-links{display:none;position:absolute;top:100%;left:0;right:0;background:white;box-shadow:0 9px 15px #0002;padding:20px 30px;flex-direction:column;align-items:stretch;gap:15px;font-size:14px}
.nav-links.is-open{display:flex}
.hero__photo{left:27%}
.hero__shade{background:linear-gradient(90deg,#101b22 0%,#101b22f2 48%,#101b2266 100%)}
.hero h1{max-width:520px}
.split-panel{grid-template-columns:1fr 1fr}
.trust__grid,.quote-section__grid{grid-template-columns:1fr}
.split-panel__overlay{padding:35px}
.service-grid{gap:16px}
.service-card h3{font-size:21px}
}
@media(max-width:620px){.container{padding-inline:20px}
.topbar__right{display:none}

.hero{min-height:630px;align-items:flex-end}
.hero__photo{inset:0;background-position:57% center}
.hero__shade{background:linear-gradient(0deg,#101b22 2%,#101b22fa 32%,#101b22aa 74%,#101b2244 100%)}
.hero__content{padding-top:235px;padding-bottom:28px}
.hero h1{font-size:clamp(35px,9vw,48px)}
.hero__lede{font-size:14px}
.hero__proof{margin-top:25px;gap:12px;font-size:11px}
.feature-strip__grid{grid-template-columns:1fr 1fr;gap:0}
.feature-strip__grid a{padding:15px 8px!important;gap:8px;border-bottom:1px solid #ffffff20;font-size:11px}
.feature-strip__grid a:nth-child(odd){border-right:1px solid #ffffff20}
.feature-icon{font-size:26px}
.feature-strip strong{font-size:11px}
.feature-strip small{font-size:10px}
.section{padding-block:50px}
.service-grid{grid-template-columns:1fr}
.service-card{margin-bottom:8px}
.split-panel{grid-template-columns:1fr}
.split-panel__commercial,.split-panel__residential{min-height:310px}
.split-panel__residential-inner{padding:35px 20px}
.trust__items{grid-template-columns:1fr;gap:20px}
.quote-section{padding-block:50px}
.footer__grid{grid-template-columns:1fr 1fr}
.footer__grid>div:first-child{grid-column:1/-1}
.footer__bottom{flex-direction:column}
.button-row .btn{width:100%}
}
@media(prefers-reduced-motion:reduce){*{transition:none!important}
}


</style>