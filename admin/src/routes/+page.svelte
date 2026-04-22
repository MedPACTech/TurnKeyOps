<script lang="ts">
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
</script>

<div class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.22),transparent_18%),linear-gradient(160deg,#fffdfa_0%,#f3f6fa_48%,#eef2f7_100%)] flex flex-col items-center justify-center text-ink-950 p-6">
  <div class="max-w-4xl text-center">
    <BrandLogo prominent className="items-center" />
    <p class="mt-4 text-xs font-semibold uppercase tracking-[0.36em] text-brand-600 sm:text-sm">
      Residential and Commercial Back Office
    </p>
    <p class="mt-5 text-xl text-ink-600 text-center max-w-2xl mx-auto leading-8 sm:text-2xl sm:leading-9">
      AI-powered operations for construction teams, from estimates and scheduling to invoicing and customer communication.
    </p>
  </div>
  <div class="mt-10 flex gap-4">
    <a href="/auth/login" class="btn-primary px-6 py-3 text-base">
      Sign In
    </a>
    <a href="/auth/register" class="btn-secondary px-6 py-3 text-base">
      Get Started Free
    </a>
  </div>
  <p class="mt-12 text-sm uppercase tracking-[0.22em] text-ink-400">Concrete, Framing, Residential and Commercial</p>
</div>
