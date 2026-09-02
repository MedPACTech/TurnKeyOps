<script lang="ts">
	import ExternalAdminLayout from '$lib/components/admin/ExternalAdminLayout.svelte';
	import type { Snippet } from 'svelte';
	import type { BobVoiceId } from '$lib/bob-voice';
	import type { BdrAdminRole } from '$lib/config/platform';
	import { setStoredAuthToken } from '$lib/api/client';

	let {
		children,
		data
	}: {
		children: Snippet;
		data: {
			role: BdrAdminRole;
			bobVoice: BobVoiceId;
			apiAccessToken?: string | null;
			adminSession?: { email?: string };
		};
	} = $props();

	$effect(() => {
		if (data.apiAccessToken) setStoredAuthToken(data.apiAccessToken);
	});
</script>

<ExternalAdminLayout tenantSlug="bdr" {data}>
	{@render children()}
</ExternalAdminLayout>
