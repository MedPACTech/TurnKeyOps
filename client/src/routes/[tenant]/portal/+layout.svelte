<script lang="ts">
 import {page} from '$app/state';
 import {House,FolderOpen,MessageCircle,FileText,UserRound} from 'lucide-svelte';
 let {data,children}=$props();
 let branding=$derived(page.data.home?.configuration);
 let base=$derived(`/${data.tenant.slug}/portal`);
</script>
<svelte:head><meta name="robots" content="noindex,nofollow"/><meta name="theme-color" content="#0f766e"/><link rel="manifest" href={`${base}/manifest.webmanifest`}/><title>My work · {data.tenant.name}</title></svelte:head>
<div data-accent={branding?.accent??'teal'} class="portal min-h-screen bg-[var(--surface)] text-[var(--text)]">
 <a class="skip" href="#portal-main">Skip to content</a>
 <header class="mx-auto flex max-w-4xl items-center justify-between gap-4 px-5 py-5"><a class="brand" href={base}>{#if branding?.logoPath}<img class="mb-2 max-h-12 max-w-32 object-contain" src={branding.logoPath} alt=""/>{/if}{branding?.companyName??data.tenant.name}</a><span class="text-sm text-[var(--text-muted)]">Customer portal</span></header>
 {#if !page.url.pathname.endsWith('/login')}
 <nav aria-label="Customer portal" class="mx-auto flex max-w-4xl gap-1 overflow-x-auto border-b border-[var(--border)] px-3">
 {#each [{label:'Home',icon:House,key:'home'},{label:'My work',icon:FolderOpen,key:'work'},{label:'Messages',icon:MessageCircle,key:'messages'},{label:'Documents',icon:FileText,key:'documents'},{label:'Account',icon:UserRound,key:'account'}] as item}
 <a href={`${base}?view=${item.key}`} aria-current={(page.url.searchParams.get('view')??'home')===item.key?'page':undefined} class="navlink"><item.icon size={18}/><span>{item.label}</span></a>
 {/each}{#if branding?.financeEnabled}<a class="navlink" href={`${base}/finance`} aria-current={page.url.pathname.endsWith('/finance')?'page':undefined}><FileText size={18}/><span>Invoices</span></a>{/if}</nav>{/if}
 <main id="portal-main" tabindex="-1" class="mx-auto max-w-4xl px-5 py-8">{@render children()}</main>
 <footer class="mx-auto max-w-4xl px-5 py-8 text-sm text-[var(--text-muted)]">Powered by TurnKeyOps</footer>
</div>
<style>
 .portal{--surface:#fafaf9;--surface-raised:#fff;--text:#1c2927;--text-muted:#53635e;--border:#d4ded9;--accent:#0f766e;line-height:1.6} :global(.dark) .portal{--surface:#13201d;--surface-raised:#1b2b27;--text:#f0f7f3;--text-muted:#b7c9c0;--border:#3c5148;--accent:#7cddd1}
 .portal[data-accent=blue]{--accent:#1d4ed8;--button:#1d4ed8}.portal[data-accent=violet]{--accent:#6d28d9;--button:#6d28d9}:global(.dark) .portal[data-accent=blue]{--accent:#93c5fd}:global(.dark) .portal[data-accent=violet]{--accent:#c4b5fd}
 .brand{font-weight:700;text-decoration:none;font-size:1.05rem}.navlink{display:flex;align-items:center;gap:.4rem;padding:.7rem .6rem;min-height:48px;font-size:.85rem;white-space:nowrap;text-decoration:none}.navlink[aria-current=page]{border-bottom:3px solid var(--accent);font-weight:700}
 .skip{position:absolute;left:-9999px}.skip:focus{left:1rem;top:1rem;background:var(--surface-raised);padding:1rem;z-index:100}
 :global(.portal h1){font-size:clamp(1.7rem,5vw,2.2rem);font-weight:700;line-height:1.2;margin:0 0 1rem}:global(.portal h2){font-size:1.2rem;font-weight:650;margin:2rem 0 .75rem}:global(.portal h3){font-size:1rem;font-weight:650;margin:1rem 0 .5rem}
 :global(.portal a){text-underline-offset:4px}:global(.portal :is(button,input,textarea,select,a):focus-visible){outline:3px solid var(--accent);outline-offset:3px}:global(.portal :is(input:not([type=checkbox]),textarea,select)){display:block;width:100%;min-height:48px;padding:.65rem;border:1px solid var(--border);border-radius:6px;background:var(--surface-raised);color:var(--text)}
 :global(.portal label){display:block;font-weight:550;margin:1rem 0 .3rem}:global(.portal input[type=checkbox]){height:22px;width:22px;vertical-align:middle;margin-right:.5rem}:global(.portal button){min-height:48px;padding:.65rem 1rem;border:1px solid var(--border);border-radius:6px;background:var(--surface-raised);font-weight:600;cursor:pointer}:global(.portal button.primary){background:var(--button,#0f766e);color:#fff;border-color:var(--button,#0f766e)}:global(.portal button:disabled){opacity:.6;cursor:wait}:global(.portal .row){padding:1.2rem 0;border-bottom:1px solid var(--border)}:global(.portal .muted){color:var(--text-muted)}:global(.portal .actions){display:flex;flex-wrap:wrap;gap:.75rem;margin-top:1rem}:global(.portal .notice){padding:1rem;background:var(--surface-raised);border-left:4px solid var(--accent);margin-bottom:1.5rem}:global(.portal .prose){white-space:pre-wrap;overflow-wrap:anywhere}
 @media(max-width:440px){.navlink{flex-direction:column;font-size:.75rem;flex:1;gap:.2rem;padding:.6rem .3rem}nav{gap:0}}
</style>
