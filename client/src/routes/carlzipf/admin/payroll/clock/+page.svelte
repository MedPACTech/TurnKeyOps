<script lang="ts">import FieldClock from '$lib/components/payroll/FieldClock.svelte';let {data}=$props();</script>
<svelte:head><title>My time · TurnKeyOps</title><link rel="manifest" href={`${data.basePath}/payroll/manifest.webmanifest`}/></svelte:head>
<h1 class="mb-6 text-2xl font-semibold">My time</h1><FieldClock workspace={data.workspace} tenant={data.tenant}/><p class="mt-6"><a class="underline" href={`${data.basePath}/payroll`}>Review and submit recorded time</a></p>
