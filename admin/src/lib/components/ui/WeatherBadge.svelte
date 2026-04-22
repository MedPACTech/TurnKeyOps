<script lang="ts">
  import type { WeatherForecastDto } from '$api/types';
  export let weather: WeatherForecastDto | undefined = undefined;
  export let compact = false;
</script>

{#if weather}
  <div class="inline-flex items-center gap-1.5 px-2 py-1 rounded-md bg-sky-50 text-sky-700 text-xs font-medium"
       title={weather.summary ?? 'Weather'}>
    {#if weather.icon}
      <img src={weather.icon} alt="" class="w-4 h-4" />
    {:else}
      🌤️
    {/if}
    {#if !compact}
      {#if weather.tempHigh}
        <span>{weather.tempHigh}°</span>
      {/if}
      {#if weather.precipChance && weather.precipChance > 0}
        <span class="text-sky-500">💧{weather.precipChance}%</span>
      {/if}
    {/if}
  </div>
{/if}
