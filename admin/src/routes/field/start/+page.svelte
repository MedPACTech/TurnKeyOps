<script lang="ts">
  import { goto } from '$app/navigation';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
  import AppointmentContextCard from '$lib/components/field/AppointmentContextCard.svelte';
  import AppointmentStartCard from '$lib/components/field/AppointmentStartCard.svelte';
  import { mobileApi } from '$lib/api/mobile';
  import type { MobileCurrentAppointmentContextDto } from '$lib/api/types';
  import { fieldEstimate } from '$lib/stores/field-estimate';
  import { onMount } from 'svelte';

  let loading = true;
  let appointment: MobileCurrentAppointmentContextDto | null = null;
  let error: string | null = null;

  async function loadAppointment() {
    loading = true;
    error = null;

    try {
      appointment = await mobileApi.getCurrentAppointment();
    } catch (err: any) {
      error = err?.message ?? 'Unable to load your appointment right now.';
    } finally {
      loading = false;
    }
  }

  function startFromAppointment() {
    if (!appointment) return;
    fieldEstimate.beginFromAppointment(appointment);
    goto('/field/confirm');
  }

  function createNewEstimate() {
    fieldEstimate.beginNewEstimate();
    goto('/field/confirm');
  }

  onMount(loadAppointment);
</script>

<div class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.18),transparent_22%),linear-gradient(180deg,#fffdf9_0%,#f4f7fb_100%)]">
  <div class="mx-auto flex min-h-screen w-full max-w-xl flex-col px-4 pb-8 pt-6 sm:px-6">
    <div class="mb-6 flex items-center justify-center">
      <BrandLogo compact className="items-center" />
    </div>

    <div class="space-y-4">
      <AppointmentStartCard
        {loading}
        hasAppointment={!!appointment}
        {error}
        onAppointment={startFromAppointment}
        onCreateNew={createNewEstimate}
        onRetry={loadAppointment}
      />

      {#if loading}
        <div class="card border border-white/70">
          <div class="animate-pulse space-y-3">
            <div class="h-4 w-32 rounded bg-ink-100"></div>
            <div class="h-5 w-48 rounded bg-ink-100"></div>
            <div class="h-4 w-full rounded bg-ink-100"></div>
            <div class="grid grid-cols-1 gap-2 pt-2">
              <div class="h-16 rounded-2xl bg-ink-100"></div>
              <div class="h-16 rounded-2xl bg-ink-100"></div>
            </div>
          </div>
        </div>
      {:else if appointment}
        <AppointmentContextCard details={appointment} compact />
      {:else if !error}
        <div class="card border border-dashed border-ink-200 text-center">
          <div class="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-ink-50 text-3xl">
            📅
          </div>
          <h2 class="mt-4 text-lg font-semibold text-ink-950">No appointment found</h2>
          <p class="mt-2 text-sm leading-6 text-ink-600">
            Bob couldn’t find a scheduled appointment for you right now. You can still create a new concrete estimate and capture the job from the field.
          </p>
        </div>
      {/if}
    </div>
  </div>
</div>
