<script lang="ts">
import type { PageData, ActionData } from './$types';
let { data, form }: { data: PageData; form: ActionData } = $props();
</script>
<svelte:head><title>Choose workspace · TurnKeyOps</title></svelte:head>
<main class="min-h-screen bg-slate-50 p-8 text-slate-900"><section class="mx-auto max-w-lg rounded-2xl bg-white p-8 shadow-sm">
<h1 class="text-2xl font-semibold">Choose your workspace</h1><p class="mt-2 text-slate-600">Your access is checked separately for each business.</p>
{#if data.message}<p role="alert" class="mt-4 text-red-700">{data.message}</p>{/if}
{#if form?.message}<p role="alert" class="mt-4 text-red-700">{form.message}</p>{/if}
{#each data.workspaces as workspace}<form method="POST" class="mt-4"><input type="hidden" name="tenant" value={workspace.slug}/><input type="hidden" name="returnTo" value={data.returnTo}/><button class="w-full rounded-lg border border-slate-300 p-4 text-left font-semibold hover:bg-slate-50">{workspace.name}</button></form>{:else}<p class="mt-6">No active workspaces are available. Contact your administrator.</p>{/each}
<form method="POST" action="/auth/logout" class="mt-6"><button class="underline">Sign out</button></form>
</section></main>
