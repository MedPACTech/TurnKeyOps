<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { formatCurrency, formatDate, statusColor } from '$lib/utils/format';
  import { LoadingSpinner, EmptyState } from '$components';
  import type { InvoiceDto } from '$api/types';

  let invoices: InvoiceDto[] = [];
  let loading = true;

  onMount(async () => {
    try {
      const res = await api.paged<InvoiceDto>('/invoices/paged', 50);
      invoices = res.items;
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      loading = false;
    }
  });
</script>

<div>
  <div class="page-header">
    <h1 class="page-title">Invoices</h1>
    <button class="btn-primary">+ New Invoice</button>
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if invoices.length === 0}
    <EmptyState icon="💰" title="No invoices yet" message="Create an invoice from an accepted estimate." />
  {:else}
    <div class="card p-0 table-wrapper">
      <table class="table">
        <thead><tr>
          <th>Number</th>
          <th>Customer</th>
          <th>Status</th>
          <th class="text-right">Total</th>
          <th class="text-right hidden sm:table-cell">Balance</th>
          <th class="hidden md:table-cell">Due</th>
        </tr></thead>
        <tbody>
          {#each invoices as inv}
            <tr>
              <td class="font-medium">{inv.invoiceNumber ?? '—'}</td>
              <td>{inv.customerName ?? '—'}</td>
              <td><span class={statusColor(inv.status)}>{inv.status}</span></td>
              <td class="text-right">{formatCurrency(inv.total)}</td>
              <td class="text-right hidden sm:table-cell font-medium
                {inv.balanceDue > 0 ? 'text-red-600' : 'text-green-600'}">
                {formatCurrency(inv.balanceDue)}
              </td>
              <td class="hidden md:table-cell">{formatDate(inv.dueDate)}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
