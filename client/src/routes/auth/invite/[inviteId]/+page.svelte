<script lang="ts">
	import type { PageProps } from './$types';

	let { data, form }: PageProps = $props();
	const returnTo = $derived(`/auth/invite/${data.context?.inviteId ?? ''}?token=${encodeURIComponent(data.inviteToken ?? '')}`);
</script>

<svelte:head><title>Activate access · TurnKeyOps</title></svelte:head>

<main class="min-h-screen bg-slate-50 px-4 py-10 text-slate-900">
	<section class="mx-auto max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-xl shadow-slate-200/50">
		<img src="/turnkeyops-logo.png" alt="TurnKeyOps" class="mx-auto h-auto w-full max-w-[16rem]" />
		{#if data.invalid}
			<div class="mt-8 rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
				<h1 class="font-bold">Activation link unavailable</h1>
				<p class="mt-1">{data.message}</p>
			</div>
		{:else if form?.accepted}
			<div class="mt-8 rounded-lg border border-emerald-200 bg-emerald-50 p-5 text-center text-emerald-900">
				<h1 class="text-xl font-bold">Access activated</h1>
				<p class="mt-2 text-sm">{form.message}</p>
				<a href={form.signInUrl} class="mt-5 inline-flex min-h-11 items-center rounded-lg bg-emerald-700 px-5 text-sm font-semibold text-white">Sign in to your workspace</a>
			</div>
		{:else if data.context}
			<div class="mt-8">
				<p class="text-xs font-bold uppercase tracking-[0.18em] text-orange-600">Tenant invitation</p>
				<h1 class="mt-2 text-2xl font-black">Join {data.context.tenantName}</h1>
				<p class="mt-2 text-sm leading-6 text-slate-600">
					You were invited as <strong>{data.context.role.replaceAll('_', ' ')}</strong>
					{#if data.context.invitedEmailMasked} using {data.context.invitedEmailMasked}{/if}{#if data.context.invitedPhoneMasked} using {data.context.invitedPhoneMasked}{/if}.
				</p>
				{#if form?.error}<p class="mt-4 rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{form.error}</p>{/if}
				{#if data.context.requiresAuthentication}
					<a href={`/auth/login?returnTo=${encodeURIComponent(returnTo)}`} class="mt-6 inline-flex min-h-11 w-full items-center justify-center rounded-lg bg-orange-600 px-5 text-sm font-semibold text-white">Verify identity to continue</a>
				{:else if data.context.canRedeem}
					<form method="POST" action="?/redeem" class="mt-6">
						<input type="hidden" name="inviteToken" value={data.inviteToken} />
						<button type="submit" class="min-h-11 w-full rounded-lg bg-orange-600 px-5 text-sm font-semibold text-white">Accept invitation</button>
					</form>
				{:else}
					<p class="mt-5 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">{data.context.nextStep}</p>
				{/if}
				<p class="mt-5 text-xs text-slate-500">Expires {new Date(data.context.expiresAtUtc).toLocaleString()}.</p>
			</div>
		{/if}
	</section>
</main>
