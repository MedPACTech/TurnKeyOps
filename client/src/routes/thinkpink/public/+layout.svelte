<script lang="ts">
	import './thinkpink.css';
	import type { Snippet } from 'svelte';
	let { children }: { children: Snippet } = $props();
</script>

<svelte:head>
	<link rel="icon" href="/thinkpink/favicon.png?v=2" sizes="any" type="image/png" />
	<link rel="icon" href="/thinkpink/favicon-32.png?v=2" sizes="32x32" type="image/png" />
	<link rel="apple-touch-icon" href="/thinkpink/apple-touch-icon.png?v=2" />
	<meta name="theme-color" content="#ee4f91" />
</svelte:head>

{@render children()}
