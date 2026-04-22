<script lang="ts">
  import { goto } from '$app/navigation';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import type { ConcreteCalculatorRequest, ConcreteCalculatorResult } from '$api/types';
  import { formatCurrency } from '$lib/utils/format';

  let tradeType: 'Concrete' | 'Framing' = 'Concrete';
  let customerName = '';

  // Concrete calculator
  let calc: ConcreteCalculatorRequest = {
    lengthFeet: 0, widthFeet: 0, depthInches: 4, wastePercent: 10, numberOfPours: 1
  };
  let result: ConcreteCalculatorResult | null = null;
  let calculating = false;

  async function calculate() {
    if (calc.lengthFeet <= 0 || calc.widthFeet <= 0) {
      toast.warning('Enter length and width');
      return;
    }
    calculating = true;
    try {
      result = await api.post<ConcreteCalculatorResult>('/estimates/calculate/concrete', calc);
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      calculating = false;
    }
  }
</script>

<div class="max-w-2xl mx-auto">
  <div class="page-header">
    <h1 class="page-title">New Estimate</h1>
  </div>

  <!-- Trade Toggle -->
  <div class="flex gap-3 mb-6">
    <button class="flex-1 card text-center py-4 transition-all
      {tradeType === 'Concrete' ? 'ring-2 ring-brand-600 bg-brand-50' : 'hover:bg-gray-50'}"
      on:click={() => tradeType = 'Concrete'}>
      <span class="text-3xl block mb-1">🏗️</span>
      <span class="font-medium">Concrete</span>
    </button>
    <button class="flex-1 card text-center py-4 transition-all
      {tradeType === 'Framing' ? 'ring-2 ring-brand-600 bg-brand-50' : 'hover:bg-gray-50'}"
      on:click={() => tradeType = 'Framing'}>
      <span class="text-3xl block mb-1">🪵</span>
      <span class="font-medium">Framing</span>
    </button>
  </div>

  {#if tradeType === 'Concrete'}
    <!-- Concrete Calculator -->
    <div class="card mb-6">
      <h2 class="font-semibold mb-4">🧮 Concrete Calculator</h2>
      <div class="grid grid-cols-2 gap-4">
        <div>
          <label class="label">Length (ft)</label>
          <input class="input" type="number" step="0.1" bind:value={calc.lengthFeet} />
        </div>
        <div>
          <label class="label">Width (ft)</label>
          <input class="input" type="number" step="0.1" bind:value={calc.widthFeet} />
        </div>
        <div>
          <label class="label">Depth (inches)</label>
          <input class="input" type="number" step="0.5" bind:value={calc.depthInches} />
        </div>
        <div>
          <label class="label">Waste %</label>
          <input class="input" type="number" bind:value={calc.wastePercent} />
        </div>
        <div>
          <label class="label"># of Pours</label>
          <input class="input" type="number" min="1" bind:value={calc.numberOfPours} />
        </div>
      </div>
      <button class="btn-primary mt-4 w-full" on:click={calculate} disabled={calculating}>
        {calculating ? 'Calculating...' : 'Calculate'}
      </button>

      {#if result}
        <div class="mt-6 p-4 bg-green-50 rounded-lg border border-green-200">
          <h3 class="font-semibold text-green-900 mb-3">Results</h3>
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div><span class="text-gray-600">Area:</span> <strong>{result.sqft.toFixed(0)} sqft</strong></div>
            <div><span class="text-gray-600">Cubic Yards:</span> <strong>{result.cubicYards.toFixed(1)} CY</strong></div>
            <div><span class="text-gray-600">CY/Pour:</span> <strong>{result.cubicYardsPerPour.toFixed(1)}</strong></div>
            <div><span class="text-gray-600">Rebar:</span> <strong>{result.rebarLinearFeet.toFixed(0)} LF</strong></div>
            <div><span class="text-gray-600">Forms:</span> <strong>{result.formBoardLinearFeet.toFixed(0)} LF</strong></div>
            <div><span class="text-gray-600">Materials:</span> <strong>{formatCurrency(result.estimatedMaterialCost)}</strong></div>
            <div><span class="text-gray-600">Labor:</span> <strong>{formatCurrency(result.estimatedLaborCost)}</strong></div>
            <div class="col-span-2 pt-2 border-t border-green-300">
              <span class="text-gray-600">Estimated Total:</span>
              <strong class="text-lg text-green-800 ml-2">{formatCurrency(result.estimatedTotal)}</strong>
            </div>
          </div>
        </div>
      {/if}
    </div>
  {:else}
    <div class="card mb-6">
      <h2 class="font-semibold mb-2">🪵 Framing Estimate</h2>
      <p class="text-sm text-gray-500">Select a framing template to get started with pre-filled line items.</p>
      <!-- TODO: Template selection UI -->
    </div>
  {/if}
</div>
