<script lang="ts">
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const step = $derived(form?.step ?? 'request');
	const returnTo = $derived(form?.returnTo ?? data.returnTo);
	const identifier = $derived(form?.identifier ?? '');
	const surfaceLabel = $derived(form?.label ?? data.label);
	const otpState = $derived(form?.otpState ?? null);
	const destinationLabel = $derived(otpState?.destinationMasked ? ` at ${otpState.destinationMasked}` : '');
	const resolvedChannel = $derived(otpState?.channel === 'sms' ? 'text message' : 'email');
</script>

<svelte:head>
	<title>TurnKeyOps · Admin sign in</title>
</svelte:head>

<main class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.16),transparent_24%),linear-gradient(180deg,#fffdf9_0%,#f2f5f9_100%)] px-4 py-8 text-slate-900">
	<section class="mx-auto flex min-h-[calc(100vh-4rem)] w-full max-w-md flex-col justify-center">
		<div class="rounded-2xl border border-white/70 bg-white/[0.92] p-5 shadow-[0_24px_60px_-36px_rgba(37,44,55,0.45)] backdrop-blur-sm sm:p-6">
			<div class="mb-8 text-center">
				<img src="/turnkeyops-logo.png" alt="TurnKeyOps.ai" class="mx-auto h-auto w-full max-w-[14rem] object-contain sm:max-w-[18rem]" />
				<p class="mt-3 text-sm font-semibold uppercase tracking-[0.24em] text-slate-500">{surfaceLabel}</p>
				<p class="mt-2 text-sm text-slate-500">Sign in with a one-time verification code</p>
			</div>

			{#if form?.message}
				<p class="mb-5 rounded-md bg-rose-50 px-3 py-2 text-sm font-medium text-rose-700">{form.message}</p>
			{/if}

			{#if step === 'request'}
				<form method="POST" action="?/request" class="space-y-4">
					<input type="hidden" name="returnTo" value={returnTo} />
					<div>
						<label class="label" for="identifier">Work email or mobile number</label>
						<input
							id="identifier"
							class="input"
							name="identifier"
							value={identifier}
							placeholder="you@company.com"
							autocomplete="username"
							required
						/>
						<p class="mt-2 text-xs text-slate-500">We’ll email an address or text a mobile number automatically.</p>
					</div>
					<button type="submit" class="btn-primary w-full">Send code</button>
				</form>
			{:else}
				<form method="POST" action="?/verify" class="space-y-4">
					<input type="hidden" name="returnTo" value={returnTo} />
					<input type="hidden" name="identifier" value={identifier} />
					<input type="hidden" name="challengeId" value={otpState?.challengeId ?? ''} />
					<div class="rounded-2xl border border-orange-100 bg-orange-50/80 px-4 py-3 text-sm text-orange-950">
						<p>Enter the code sent by {resolvedChannel}{destinationLabel}.</p>
						{#if otpState?.devCode}
							<p class="mt-2 font-medium">Dev code: {otpState.devCode}</p>
						{/if}
					</div>
					<div>
						<label class="label" for="code">Verification code</label>
						<input
							id="code"
							class="input"
							name="code"
							inputmode="numeric"
							autocomplete="one-time-code"
							placeholder="123456"
							required
						/>
					</div>
					<button type="submit" class="btn-primary w-full">Verify and sign in</button>
				</form>

				<form method="POST" action="?/request" class="mt-3">
					<input type="hidden" name="returnTo" value={returnTo} />
					<input type="hidden" name="identifier" value={identifier} />
					<button type="submit" class="btn-secondary w-full">Resend code</button>
				</form>

				<div class="mt-4 text-center">
					<a href={`/auth/login?returnTo=${encodeURIComponent(returnTo)}`} class="text-sm text-slate-500 hover:text-slate-700">
						Use a different email or phone number
					</a>
				</div>
			{/if}
		</div>
	</section>
</main>
