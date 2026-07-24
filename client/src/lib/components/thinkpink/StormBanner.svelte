<script lang="ts">
	import { site } from '$lib/tenants/thinkpink/content';
</script>

<div
	class="bg-ink text-bone flex flex-wrap items-center justify-center gap-x-3 gap-y-1 px-6 py-2.5 text-center text-sm font-semibold tracking-[0.02em]"
>
	<span class="text-pink-bright text-xs font-bold tracking-[0.1em] uppercase">Storm response</span>
	<span>
		Crews available now for storm cleanup across {site.region} — call
		<a href={site.phoneHref} class="text-pink-bright hover:underline">{site.phone}</a>
	</span>
</div>

