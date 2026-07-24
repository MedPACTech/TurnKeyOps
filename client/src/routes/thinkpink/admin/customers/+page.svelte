<script lang="ts">
	import { Building2, Mail, MapPin, Phone, UserRound } from 'lucide-svelte';
	import type { PageProps } from './$types';
	let { data }: PageProps = $props();
</script>
<svelte:head><title>Contacts · Think Pink</title></svelte:head>
<div class="mx-auto max-w-6xl space-y-6 pb-10">
	<header><p class="text-xs font-bold uppercase tracking-[.18em] text-[var(--accent-text)]">Relationships</p><h1 class="mt-2 text-3xl font-black">Contacts</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Live customer and property records built from Think Pink requests.</p></header>
	<div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
		{#each data.contacts as contact}
			<article class="rounded-xl bg-white p-5 shadow-[var(--shell-shadow)]">
				<div class="flex items-start gap-3"><div class="flex h-10 w-10 items-center justify-center rounded-full bg-[var(--accent-soft)] text-[var(--accent-text)]"><UserRound class="h-5 w-5" /></div><div><h2 class="font-bold">{contact.name}</h2>{#if contact.company}<p class="mt-1 flex gap-2 text-xs text-[var(--text-muted)]"><Building2 class="h-3.5 w-3.5" />{contact.company}</p>{/if}</div></div>
				<div class="mt-4 space-y-2 text-sm text-[var(--text-muted)]">
					{#if contact.email}<a class="flex gap-2 hover:text-[var(--accent-text)]" href={`mailto:${contact.email}`}><Mail class="h-4 w-4" />{contact.email}</a>{/if}
					{#if contact.phone}<a class="flex gap-2 hover:text-[var(--accent-text)]" href={`tel:${contact.phone}`}><Phone class="h-4 w-4" />{contact.phone}</a>{/if}
					{#each contact.addresses as address}<p class="flex gap-2"><MapPin class="h-4 w-4 shrink-0" />{address}</p>{/each}
				</div>
				<a href={`/thinkpink/admin/requests?request=${encodeURIComponent(contact.requestIds[0])}`} class="mt-5 block text-sm font-bold text-[var(--accent-text)]">{contact.requestIds.length} request{contact.requestIds.length === 1 ? '' : 's'} · Open record →</a>
			</article>
		{:else}<p class="text-sm text-[var(--text-muted)]">No contacts yet.</p>{/each}
	</div>
</div>
