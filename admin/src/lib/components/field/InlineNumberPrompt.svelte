<script lang="ts">
  type NumberField = {
    id: string;
    label: string;
    suffix?: string;
    step?: string;
    min?: string;
    placeholder?: string;
    value: string;
  };

  export let title = '';
  export let fields: NumberField[] = [];
  export let submitLabel = 'Save';
  export let onSubmit: (values: Record<string, number>) => void;

  let touched = false;

  function submit() {
    touched = true;
    const parsed = Object.fromEntries(
      fields.map((field) => [field.id, Number(field.value)])
    );

    const invalid = Object.values(parsed).some((value) => Number.isNaN(value) || value <= 0);
    if (invalid) return;

    onSubmit(parsed);
  }
</script>

<div class="space-y-4">
  {#if title}
    <p class="text-sm font-medium text-ink-700">{title}</p>
  {/if}

  <div class={`grid gap-3 ${fields.length > 1 ? 'grid-cols-2' : 'grid-cols-1'}`}>
    {#each fields as field}
      <label class="block">
        <span class="label mb-2">{field.label}</span>
        <div class="relative">
          <input
            class="input min-h-[3.5rem] pr-12 text-base"
            type="number"
            bind:value={field.value}
            step={field.step ?? '1'}
            min={field.min ?? '0'}
            placeholder={field.placeholder ?? '0'}
            inputmode="decimal"
          />
          {#if field.suffix}
            <span class="pointer-events-none absolute inset-y-0 right-4 flex items-center text-sm font-medium text-ink-500">
              {field.suffix}
            </span>
          {/if}
        </div>
      </label>
    {/each}
  </div>

  {#if touched && fields.some((field) => Number(field.value) <= 0 || Number.isNaN(Number(field.value)))}
    <p class="text-sm text-red-600">Enter a value greater than zero for each field.</p>
  {/if}

  <button class="btn-primary w-full min-h-[3.5rem] rounded-2xl text-base" on:click={submit}>
    {submitLabel}
  </button>
</div>
