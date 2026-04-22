<script lang="ts">
  import type { EstimateDefaultsDto } from '$api/types';

  type DefaultsField = {
    key: keyof EstimateDefaultsDto;
    label: string;
    help?: string;
    prefix?: string;
    suffix?: string;
    step?: string;
    min?: number;
  };

  export let title = '';
  export let subtitle = '';
  export let icon = '⚙️';
  export let fields: DefaultsField[] = [];
  export let values: EstimateDefaultsDto;
  export let onValueChange: (key: keyof EstimateDefaultsDto, value: number) => void;

  function displayValue(key: keyof EstimateDefaultsDto) {
    const value = values[key];
    return Number.isFinite(value) ? String(value) : '';
  }

  function handleInput(key: keyof EstimateDefaultsDto, raw: string) {
    const numeric = raw === '' ? 0 : Number(raw);
    onValueChange(key, Number.isFinite(numeric) ? numeric : 0);
  }
</script>

<section class="card border border-brand-100/70">
  <div class="flex items-start gap-4">
    <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-2xl">
      {icon}
    </div>
    <div>
      <h2 class="text-lg font-semibold text-ink-950">{title}</h2>
      <p class="mt-1 text-sm text-ink-500">{subtitle}</p>
    </div>
  </div>

  <div class="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
    {#each fields as field}
      <label class="block">
        <span class="label mb-2">{field.label}</span>
        <div class="relative">
          {#if field.prefix}
            <span class="pointer-events-none absolute inset-y-0 left-4 flex items-center text-sm font-medium text-ink-500">
              {field.prefix}
            </span>
          {/if}
          <input
            class={`input min-h-[3.1rem] text-sm ${field.prefix ? 'pl-8' : ''} ${field.suffix ? 'pr-14' : ''}`}
            type="number"
            min={field.min ?? 0}
            step={field.step ?? '0.01'}
            value={displayValue(field.key)}
            on:input={(event) => handleInput(field.key, (event.currentTarget as HTMLInputElement).value)}
          />
          {#if field.suffix}
            <span class="pointer-events-none absolute inset-y-0 right-4 flex items-center text-sm font-medium text-ink-500">
              {field.suffix}
            </span>
          {/if}
        </div>
        {#if field.help}
          <span class="mt-2 block text-xs leading-5 text-ink-500">{field.help}</span>
        {/if}
      </label>
    {/each}
  </div>
</section>
