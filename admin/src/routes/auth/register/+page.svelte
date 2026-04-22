<script lang="ts">
  import { startEmailPasswordRegistration } from '$lib/api/auth';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
  import { toast } from '$stores/toast';

  let firstName = '';
  let lastName = '';
  let email = '';
  let companyName = '';
  let loading = false;
  let submitted = false;

  async function handleRegister() {
    if (!email.trim() || !firstName.trim()) return;

    loading = true;
    try {
      const displayName = [firstName, lastName].filter(Boolean).join(' ').trim();
      const resetUrlBase = `${window.location.origin}/emailValidation`;

      await startEmailPasswordRegistration(email.trim(), displayName || companyName.trim() || email.trim(), resetUrlBase);
      submitted = true;
      toast.success('Registration email sent. Check your inbox to finish setting your password.');
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to start registration');
    } finally {
      loading = false;
    }
  }
</script>

<div class="min-h-screen flex items-center justify-center bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.16),transparent_24%),linear-gradient(180deg,#fffdf9_0%,#f2f5f9_100%)] p-4">
  <div class="card w-full max-w-md border-brand-100/70">
    <div class="text-center mb-8">
      <BrandLogo className="items-center" />
      <p class="mt-3 text-sm uppercase tracking-[0.24em] text-ink-500">Start Your Workspace</p>
      <p class="text-sm text-ink-500 mt-2">We’ll email you a secure link to finish creating your account.</p>
    </div>

    {#if submitted}
      <div class="space-y-4 text-sm text-ink-600">
        <p>
          We sent a registration email to <span class="font-medium text-ink-950">{email}</span>.
        </p>
        <p>
          Open that link to set your password, then come back here and sign in with the OTP flow.
        </p>
        <button class="btn-primary w-full" on:click={() => (submitted = false)}>
          Send another email
        </button>
      </div>
    {:else}
      <form on:submit|preventDefault={handleRegister} class="space-y-4">
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="label" for="firstName">First Name</label>
            <input id="firstName" class="input" bind:value={firstName} required />
          </div>
          <div>
            <label class="label" for="lastName">Last Name</label>
            <input id="lastName" class="input" bind:value={lastName} />
          </div>
        </div>
        <div>
          <label class="label" for="company">Company Name</label>
          <input id="company" class="input" bind:value={companyName} placeholder="Your contracting company" />
        </div>
        <div>
          <label class="label" for="regEmail">Email</label>
          <input id="regEmail" type="email" class="input" bind:value={email} required />
        </div>
        <button type="submit" class="btn-primary w-full" disabled={loading}>
          {loading ? 'Sending email...' : 'Send setup email'}
        </button>
      </form>
    {/if}

    <p class="text-center text-sm text-ink-500 mt-6">
      Already have an account? <a href="/auth/login" class="text-brand-600 font-medium hover:underline">Sign in</a>
    </p>
  </div>
</div>
