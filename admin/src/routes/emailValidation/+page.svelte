<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { completeEmailPasswordRegistration } from '$lib/api/auth';
  import { auth } from '$stores/auth';
  import { toast } from '$stores/toast';

  const challengeId = $derived(page.url.searchParams.get('challengeId') ?? page.url.searchParams.get('challenge'));
  const token = $derived(page.url.searchParams.get('token') ?? page.url.searchParams.get('code'));
  const email = $derived(page.url.searchParams.get('email') ?? '');
  const displayName = $derived(page.url.searchParams.get('displayName') ?? page.url.searchParams.get('name') ?? '');

  let password = $state('');
  let confirmPassword = $state('');
  let acceptedTerms = $state(true);
  let loading = $state(false);

  async function completeRegistration() {
    if (!challengeId || !token) {
      toast.error('This registration link is missing required verification details.');
      return;
    }

    if (!password || password.length < 8) {
      toast.error('Choose a password with at least 8 characters.');
      return;
    }

    if (password !== confirmPassword) {
      toast.error('Passwords do not match.');
      return;
    }

    loading = true;
    try {
      const result = await completeEmailPasswordRegistration({
        challengeId,
        token,
        email: email || undefined,
        displayName: displayName || undefined,
        password,
        newPassword: password,
        accepted: acceptedTerms,
        version: 'current',
        terms: {
          accepted: acceptedTerms,
          version: 'current'
        }
      });

      auth.loginFromResult(result);
      toast.success('Account setup complete.');
      goto('/app');
    } catch (err: any) {
      toast.error(err.message ?? 'Unable to complete registration');
    } finally {
      loading = false;
    }
  }
</script>

<div class="min-h-screen flex items-center justify-center bg-gray-50 p-4">
  <div class="card w-full max-w-sm">
    <div class="text-center mb-6">
      <h1 class="text-2xl font-bold">Finish Account Setup</h1>
      <p class="text-sm text-gray-500 mt-1">Create your password to activate your TurnKeyOps account.</p>
    </div>

    <form onsubmit={(event) => { event.preventDefault(); completeRegistration(); }} class="space-y-4">
      <div>
        <label class="label" for="email">Email</label>
        <input id="email" class="input" value={email} readonly />
      </div>
      <div>
        <label class="label" for="password">Password</label>
        <input id="password" type="password" class="input" bind:value={password} minlength="8" required />
      </div>
      <div>
        <label class="label" for="confirmPassword">Confirm Password</label>
        <input id="confirmPassword" type="password" class="input" bind:value={confirmPassword} minlength="8" required />
      </div>
      <label class="flex items-start gap-3 text-sm text-gray-600">
        <input type="checkbox" class="mt-1" bind:checked={acceptedTerms} />
        <span>I agree to the current terms and account setup requirements.</span>
      </label>
      <button type="submit" class="btn-primary w-full" disabled={loading}>
        {loading ? 'Completing setup...' : 'Complete account setup'}
      </button>
    </form>
  </div>
</div>
