<script lang="ts">
	import ExternalAdminLayout from '$lib/components/admin/ExternalAdminLayout.svelte';
	import type { Snippet } from 'svelte';
	import type { BobVoiceId } from '$lib/bob-voice';
	import type { BdrAdminRole } from '$lib/config/platform';

	let {
		children,
		data
	}: {
		children: Snippet;
		data: {
			role: BdrAdminRole;
			bobVoice: BobVoiceId;
			adminSession?: { email?: string };
		};
	} = $props();
</script>

<ExternalAdminLayout tenantSlug="bdr" {data}>
	{@render children()}
</ExternalAdminLayout>
