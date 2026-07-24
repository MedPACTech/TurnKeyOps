<script lang="ts">
	/**
	 * Wordmark fallback. Drop the real logo at /static/thinkpink/logo.png and it is used
	 * automatically; otherwise this renders type-only so nothing ever 404s visibly.
	 */
	let { class: className = '', dark = false }: { class?: string; dark?: boolean } = $props();

	let useImage = $state(true);
</script>

{#if useImage}
	<img
		src="/thinkpink/logo.png"
		alt="Think Pink Land Clearing"
		class="block h-14 w-auto mix-blend-multiply sm:h-16 {className}"
		onerror={() => (useImage = false)}
	/>
{:else}
	<span
		class="display text-xl leading-none font-black sm:text-2xl {dark
			? 'text-bone'
			: 'text-ink'} {className}"
	>
		Think <span class="text-pink-bright">Pink</span><br class="hidden sm:inline" />
		<span class={dark ? 'text-dark-body' : 'text-muted'}>Land Clearing</span>
	</span>
{/if}
