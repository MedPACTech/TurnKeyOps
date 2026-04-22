<script lang="ts">
  import { goto } from '$app/navigation';
  import { startOtp, completeOtp, type OtpChannel, type StartOtpResponse } from '$lib/api/auth';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
  import { auth } from '$stores/auth';
  import { toast } from '$stores/toast';

  let identifier = '';
  let preferredChannel: OtpChannel = 'email';
  let code = '';
  let loading = false;
  let otpState: StartOtpResponse | null = null;
  let step: 'request' | 'verify' = 'request';

  function resolvedChannel(): string {
    if (otpState?.channel && otpState.channel !== 'choose') return otpState.channel;
    return preferredChannel;
  }

  async function requestCode() {
    if (!identifier.trim()) return;
    loading = true;
    try {
      otpState = await startOtp(identifier, preferredChannel);
      step = 'verify';
      code = '';
      const destination = otpState.destinationMasked ? ` at ${otpState.destinationMasked}` : '';
      toast.success(`Verification code sent${destination}.`);
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to send verification code');
    } finally {
      loading = false;
    }
  }

  async function verifyCode() {
    if (!identifier.trim() || !code.trim()) return;
    loading = true;
    try {
      const result = await completeOtp(identifier, code, otpState?.challengeId);
      auth.loginFromResult(result);
      toast.success('Welcome back!');
      goto('/app');
    } catch (err: any) {
      toast.error(err.message ?? 'Verification failed');
    } finally {
      loading = false;
    }
  }

  function goBack() {
    step = 'request';
    code = '';
  }
</script>

<div class="min-h-screen flex items-center justify-center bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.16),transparent_24%),linear-gradient(180deg,#fffdf9_0%,#f2f5f9_100%)] p-4">
  <div class="card w-full max-w-md border-brand-100/70">
    <div class="text-center mb-8">
      <BrandLogo className="items-center" />
      <p class="mt-3 text-sm uppercase tracking-[0.24em] text-ink-500">Secure Contractor Access</p>
      <p class="text-sm text-ink-500 mt-2">Sign in with a one-time verification code</p>
    </div>

    {#if step === 'request'}
      <form on:submit|preventDefault={requestCode} class="space-y-4">
        <div>
          <label class="label" for="identifier">Email or mobile number</label>
          <input
            id="identifier"
            class="input"
            bind:value={identifier}
            placeholder="you@company.com"
            required
          />
        </div>
        <div>
          <label class="label" for="channel">Delivery method</label>
          <select id="channel" class="input" bind:value={preferredChannel}>
            <option value="email">Email</option>
            <option value="sms">Text message</option>
          </select>
        </div>
        <button type="submit" class="btn-primary w-full" disabled={loading}>
          {loading ? 'Sending code...' : 'Send code'}
        </button>
      </form>
    {:else}
      <form on:submit|preventDefault={verifyCode} class="space-y-4">
        <div class="rounded-2xl border border-brand-100 bg-brand-50/80 px-4 py-3 text-sm text-brand-900">
          <p>Enter the code sent via {resolvedChannel()}.</p>
          {#if otpState?.destinationMasked}
            <p class="mt-1 text-brand-700">{otpState.destinationMasked}</p>
          {/if}
          {#if otpState?.devCode}
            <p class="mt-2 font-medium">Dev code: {otpState.devCode}</p>
          {/if}
        </div>
        <div>
          <label class="label" for="code">Verification code</label>
          <input
            id="code"
            class="input"
            bind:value={code}
            inputmode="numeric"
            autocomplete="one-time-code"
            placeholder="123456"
            required
          />
        </div>
        <button type="submit" class="btn-primary w-full" disabled={loading}>
          {loading ? 'Verifying...' : 'Verify and sign in'}
        </button>
        <button type="button" class="btn-secondary w-full" on:click={requestCode} disabled={loading}>
          Resend code
        </button>
        <button type="button" class="w-full text-sm text-ink-500 hover:text-ink-700" on:click={goBack} disabled={loading}>
          Use a different email or phone number
        </button>
      </form>
    {/if}

    <p class="text-center text-sm text-ink-500 mt-6">
      Need an account? <a href="/auth/register" class="text-brand-600 font-medium hover:underline">Request access</a>
    </p>
  </div>
</div>
