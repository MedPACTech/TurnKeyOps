<script lang="ts">
  import { browser } from '$app/environment';
  import { goto } from '$app/navigation';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
  import AppointmentContextCard from '$lib/components/field/AppointmentContextCard.svelte';
  import type { FieldEstimateContextDetails } from '$lib/api/types';
  import { fieldEstimate } from '$lib/stores/field-estimate';

  $: context = $fieldEstimate;

  let editMode = false;
  let form: FieldEstimateContextDetails = {
    customerName: '',
    customerCompany: '',
    projectAddress: '',
    estimatorName: '',
    projectName: ''
  };

  $: if (context) {
    form = { ...context.details };
  }

  $: if (browser && !context) {
    goto('/field/start');
  }

  function saveEdits() {
    fieldEstimate.setDetails({
      ...form,
      customerName: form.customerName.trim(),
      customerCompany: form.customerCompany?.trim(),
      projectAddress: form.projectAddress.trim(),
      estimatorName: form.estimatorName.trim(),
      projectName: form.projectName?.trim()
    });
    editMode = false;
  }

  function continueToIntake() {
    if (editMode) {
      saveEdits();
    }

    goto('/field/intake');
  }
</script>

{#if context}
  <div class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.18),transparent_22%),linear-gradient(180deg,#fffdf9_0%,#f4f7fb_100%)]">
    <div class="mx-auto flex min-h-screen w-full max-w-xl flex-col px-4 pb-8 pt-6 sm:px-6">
      <div class="mb-6 flex items-center justify-center">
        <BrandLogo compact className="items-center" />
      </div>

      <div class="card">
        <div class="flex items-start gap-4">
          <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-3xl">
            👷‍♂️
          </div>
          <div>
            <p class="text-[11px] font-semibold uppercase tracking-[0.26em] text-brand-600">Bob</p>
            <h1 class="mt-2 text-2xl font-bold text-ink-950">Confirm Estimate</h1>
            <p class="mt-3 text-sm leading-6 text-ink-600">
              Here&apos;s what I&apos;ve got. Is this the right estimate?
            </p>
          </div>
        </div>
      </div>

      <div class="mt-4">
        <AppointmentContextCard details={editMode ? form : context.details} />
      </div>

      {#if editMode}
        <div class="card mt-4 space-y-4">
          <div>
            <label class="label" for="customerName">Customer Name</label>
            <input id="customerName" class="input min-h-[3rem]" bind:value={form.customerName} />
          </div>

          <div>
            <label class="label" for="customerCompany">Customer Company</label>
            <input id="customerCompany" class="input min-h-[3rem]" bind:value={form.customerCompany} />
          </div>

          <div>
            <label class="label" for="projectAddress">Project / Site Address</label>
            <textarea id="projectAddress" class="input min-h-[6rem]" bind:value={form.projectAddress}></textarea>
          </div>

          <div>
            <label class="label" for="projectName">Project Name</label>
            <input id="projectName" class="input min-h-[3rem]" bind:value={form.projectName} />
          </div>

          <button class="btn-secondary w-full min-h-[3.25rem] rounded-2xl text-base" on:click={saveEdits}>
            Save details
          </button>
        </div>
      {/if}

      <div class="mt-6 space-y-3">
        <button class="btn-primary w-full min-h-[3.5rem] rounded-2xl text-base" on:click={continueToIntake}>
          Yes, continue
        </button>
        <button class="btn-secondary w-full min-h-[3.5rem] rounded-2xl text-base" on:click={() => (editMode = !editMode)}>
          {editMode ? 'Done editing' : 'Edit details'}
        </button>
      </div>
    </div>
  </div>
{/if}
