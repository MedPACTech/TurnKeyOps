<script lang="ts">
	import { onMount } from 'svelte';
	type InstallPrompt = Event & { prompt(): Promise<void>; userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }> };
	let prompt = $state<InstallPrompt | null>(null);
	let notice = $state('');
	let installed = $state(false);
	onMount(() => {
		installed = window.matchMedia('(display-mode: standalone)').matches;
		const capturePrompt = (event: Event) => { event.preventDefault(); prompt = event as InstallPrompt; };
		const markInstalled = () => { installed = true; prompt = null; notice = 'Field workspace installed.'; };
		window.addEventListener('beforeinstallprompt', capturePrompt);
		window.addEventListener('appinstalled', markInstalled);
		if ('serviceWorker' in navigator && window.isSecureContext) {
			void navigator.serviceWorker.register('/carlzipf-field-service-worker.js', { scope: '/carlzipf/tech' }).catch(() => { notice = 'Installation support could not load. Device draft saving is still available.'; });
		}
		return () => { window.removeEventListener('beforeinstallprompt', capturePrompt); window.removeEventListener('appinstalled', markInstalled); };
	});
	async function install() {
		if (!prompt) return;
		try { await prompt.prompt(); const choice = await prompt.userChoice; notice = choice.outcome === 'accepted' ? 'Installation requested.' : 'You can install later from the browser menu.'; }
		catch { notice = 'Use your browser’s install or Add to Home Screen option.'; }
		finally { prompt = null; }
	}
</script>

<svelte:head>
	<link rel="manifest" href="/carlzipf/tech/pwa/manifest.webmanifest" />
	<link rel="apple-touch-icon" href="/carlzipf/tech/pwa/icon-192.png" />
	<meta name="theme-color" content="#92400e" />
	<meta name="apple-mobile-web-app-capable" content="yes" />
</svelte:head>

<div class="mb-5 flex flex-wrap items-center justify-between gap-3 text-xs text-gray-600">
	<p>{installed ? 'Installed field workspace' : 'Install this workspace from your browser’s menu or Add to Home Screen.'} Open while connected before visiting a site without service.</p>
	{#if prompt}<button class="btn-secondary" onclick={install}>Install field app</button>{/if}
	{#if notice}<p role="status">{notice}</p>{/if}
</div>
