<script lang="ts">
  import { browser } from '$app/environment';
  import { goto } from '$app/navigation';
  import BrandLogo from '$lib/components/branding/BrandLogo.svelte';
  import AppointmentContextCard from '$lib/components/field/AppointmentContextCard.svelte';
  import BobMessageBubble from '$lib/components/field/BobMessageBubble.svelte';
  import InlineNumberPrompt from '$lib/components/field/InlineNumberPrompt.svelte';
  import MobileActionBar from '$lib/components/field/MobileActionBar.svelte';
  import NaturalLanguageReplyInput from '$lib/components/field/NaturalLanguageReplyInput.svelte';
  import QuickReplyChips from '$lib/components/field/QuickReplyChips.svelte';
  import StructuredEstimateSummaryCard from '$lib/components/field/StructuredEstimateSummaryCard.svelte';
  import UserReplyBubble from '$lib/components/field/UserReplyBubble.svelte';
  import { fieldEstimate } from '$lib/stores/field-estimate';
  import { fieldIntake, type FinishTypeOption, type ProjectTypeOption, type ReinforcementTypeOption } from '$lib/stores/field-intake';

  const projectTypeOptions: { label: string; value: ProjectTypeOption }[] = [
    { label: 'Driveway', value: 'Driveway' },
    { label: 'Patio', value: 'Patio' },
    { label: 'Sidewalk', value: 'Sidewalk' },
    { label: 'Slab', value: 'Slab' },
    { label: 'Steps', value: 'Steps' },
    { label: 'Other', value: 'Other' }
  ];

  const booleanOptions = [
    { label: 'Yes', value: 'yes' },
    { label: 'No', value: 'no' }
  ];

  const reinforcementOptions: { label: string; value: ReinforcementTypeOption }[] = [
    { label: 'Rebar', value: 'Rebar' },
    { label: 'Wire Mesh', value: 'Wire Mesh' },
    { label: 'Fiber Mesh', value: 'Fiber Mesh' },
    { label: 'None', value: 'None' }
  ];

  const finishOptions: { label: string; value: FinishTypeOption }[] = [
    { label: 'Broom', value: 'Broom' },
    { label: 'Smooth', value: 'Smooth' },
    { label: 'Stamped', value: 'Stamped' },
    { label: 'Exposed Aggregate', value: 'Exposed Aggregate' },
    { label: 'Other', value: 'Other' }
  ];

  $: context = $fieldEstimate;
  $: intake = $fieldIntake;

  $: if (browser && !context) {
    goto('/field/start');
  }

  function answerJobType(value: string) {
    fieldIntake.answer('jobType', { projectType: value as ProjectTypeOption }, value);
  }

  function answerDimensions(values: Record<string, number>) {
    fieldIntake.answer(
      'dimensions',
      { lengthFt: values.lengthFt, widthFt: values.widthFt },
      `${values.lengthFt} ft by ${values.widthFt} ft`
    );
  }

  function answerDepth(values: Record<string, number>) {
    fieldIntake.answer('depth', { depthIn: values.depthIn }, `${values.depthIn}" thick`);
  }

  function answerPourCount(values: Record<string, number>) {
    fieldIntake.answer('pourCount', { pourCount: values.pourCount }, `${values.pourCount} pours`);
  }

  function answerBool(step: 'demoRequired' | 'excavationRequired' | 'pumpRequired', value: string) {
    const boolValue = value === 'yes';
    const label = boolValue ? 'Yes' : 'No';
    fieldIntake.answer(step, { [step]: boolValue }, label);
  }

  function answerReinforcement(value: string) {
    fieldIntake.answer('reinforcementType', { reinforcementType: value as ReinforcementTypeOption }, value);
  }

  function answerFinish(value: string) {
    fieldIntake.answer('finishType', { finishType: value as FinishTypeOption }, value);
  }

  const naturalLanguageHints: Record<string, string> = {
    jobType: 'Try “25 by 20 driveway, 4 inches thick” or “25x20 patio”.',
    dimensions: 'Try “25 by 20” or include the job type if you want Bob to capture both.',
    depth: 'Try “4 inches thick”.',
    pourCount: 'Try “Two pours, pump needed”.',
    demoRequired: 'Try “No demo, but yes excavation”.',
    excavationRequired: 'Try “No demo, but yes excavation”.',
    pumpRequired: 'Try “Pump needed”.',
    reinforcementType: 'Try “Stamped finish with rebar”.',
    finishType: 'Try “Stamped finish with rebar”.',
    complete: 'You can go back and refine the confirmed details anytime.'
  };

  function handleNaturalLanguageReply(value: string) {
    fieldIntake.interpretReply(value);
  }
</script>

{#if context}
  <div class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(249,115,22,0.18),transparent_22%),linear-gradient(180deg,#fffdf9_0%,#f4f7fb_100%)]">
    <div class="mx-auto flex min-h-screen w-full max-w-2xl flex-col px-4 pb-36 pt-6 sm:px-6 lg:grid lg:grid-cols-[minmax(0,1fr)_20rem] lg:gap-6 lg:pb-8">
      <div class="lg:col-span-2">
        <div class="mb-6 flex items-center justify-center">
          <BrandLogo compact className="items-center" />
        </div>
      </div>

      <div class="min-w-0">
        <div class="card">
          <div class="flex items-start gap-4">
            <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-3xl">
              👷‍♂️
            </div>
            <div>
              <p class="text-[11px] font-semibold uppercase tracking-[0.26em] text-brand-600">Bob</p>
              <h1 class="mt-2 text-2xl font-bold text-ink-950">Estimate Intake</h1>
              <p class="mt-3 text-sm leading-6 text-ink-600">
                I’ll walk you through the job details one step at a time and keep the structured estimate updated as we go.
              </p>
            </div>
          </div>
        </div>

        <div class="mt-4">
          <AppointmentContextCard details={context.details} compact />
        </div>

        <div class="mt-4 space-y-4">
          {#each intake.messages as message}
            {#if message.role === 'bob'}
              <BobMessageBubble text={message.text} />
            {:else}
              <UserReplyBubble text={message.text} />
            {/if}
          {/each}
        </div>
      </div>

      <div class="mt-4 lg:mt-0">
        <div class="lg:sticky lg:top-6">
          <StructuredEstimateSummaryCard values={intake.values} />
        </div>
      </div>

      <div class="lg:col-span-2">
        <MobileActionBar>
          {#if intake.currentStep === 'jobType'}
            <div class="space-y-4">
              <QuickReplyChips options={projectTypeOptions} onSelect={answerJobType} />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: 25 by 20 driveway, 4 inches thick"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'dimensions'}
            <div class="space-y-4">
              <InlineNumberPrompt
                title="Enter the overall slab dimensions."
                fields={[
                  { id: 'lengthFt', label: 'Length', suffix: 'ft', step: '0.5', min: '0.5', value: intake.values.lengthFt?.toString() ?? '' },
                  { id: 'widthFt', label: 'Width', suffix: 'ft', step: '0.5', min: '0.5', value: intake.values.widthFt?.toString() ?? '' }
                ]}
                submitLabel="Save dimensions"
                onSubmit={answerDimensions}
              />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: 25x20 patio"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'depth'}
            <div class="space-y-4">
              <InlineNumberPrompt
                title="Enter the slab depth in inches."
                fields={[
                  { id: 'depthIn', label: 'Depth', suffix: 'in', step: '0.5', min: '0.5', value: intake.values.depthIn?.toString() ?? '4' }
                ]}
                submitLabel="Save depth"
                onSubmit={answerDepth}
              />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder='Example: 4 inches thick'
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'pourCount'}
            <div class="space-y-4">
              <InlineNumberPrompt
                title="How many pours should I plan for?"
                fields={[
                  { id: 'pourCount', label: 'Pours', step: '1', min: '1', value: intake.values.pourCount?.toString() ?? '1' }
                ]}
                submitLabel="Save pours"
                onSubmit={answerPourCount}
              />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: Two pours, pump needed"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'demoRequired'}
            <div class="space-y-4">
              <QuickReplyChips options={booleanOptions} onSelect={(value) => answerBool('demoRequired', value)} />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: No demo, but yes excavation"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'excavationRequired'}
            <div class="space-y-4">
              <QuickReplyChips options={booleanOptions} onSelect={(value) => answerBool('excavationRequired', value)} />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: No demo, but yes excavation"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'pumpRequired'}
            <div class="space-y-4">
              <QuickReplyChips options={booleanOptions} onSelect={(value) => answerBool('pumpRequired', value)} />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: Two pours, pump needed"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'reinforcementType'}
            <div class="space-y-4">
              <QuickReplyChips options={reinforcementOptions} onSelect={answerReinforcement} />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: Stamped finish with rebar"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else if intake.currentStep === 'finishType'}
            <div class="space-y-4">
              <QuickReplyChips options={finishOptions} onSelect={answerFinish} />
              <NaturalLanguageReplyInput
                hint={naturalLanguageHints[intake.currentStep]}
                placeholder="Example: Stamped finish with rebar"
                submitLabel="Tell Bob"
                onSubmit={handleNaturalLanguageReply}
              />
            </div>
          {:else}
            <div class="space-y-3">
              <div class="rounded-2xl border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-800">
                Bob has captured the initial estimate details. Review the summary and we can build the next step from here.
              </div>
              <button class="btn-primary w-full min-h-[3.25rem] rounded-2xl text-base" on:click={() => goto('/field/review')}>
                Review estimate
              </button>
              <button class="btn-secondary w-full min-h-[3.25rem] rounded-2xl text-base" on:click={() => goto('/field/confirm')}>
                Back to confirmed details
              </button>
            </div>
          {/if}
        </MobileActionBar>
      </div>
    </div>
  </div>
{/if}
