<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { formatPhone, formatDate, initials } from '$lib/utils/format';
  import { LoadingSpinner, EmptyState, Modal } from '$components';
  import type { CustomerDto } from '$api/types';

  let customers: CustomerDto[] = [];
  let loading = true;
  let searchQuery = '';
  let showForm = false;
  let editingCustomer: Partial<CustomerDto> = {};

  async function loadCustomers() {
    loading = true;
    try {
      const res = await api.paged<CustomerDto>('/customers/paged', 100);
      customers = res.items;
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      loading = false;
    }
  }

  async function saveCustomer() {
    try {
      if (editingCustomer.id) {
        await api.put('/customers', editingCustomer);
        toast.success('Customer updated');
      } else {
        await api.post('/customers', editingCustomer);
        toast.success('Customer added');
      }
      showForm = false;
      editingCustomer = {};
      loadCustomers();
    } catch (err: any) {
      toast.error(err.message);
    }
  }

  function openNew() {
    editingCustomer = {};
    showForm = true;
  }

  function openEdit(c: CustomerDto) {
    editingCustomer = { ...c };
    showForm = true;
  }

  $: filtered = searchQuery
    ? customers.filter(c =>
        `${c.firstName} ${c.lastName} ${c.companyName ?? ''} ${c.email ?? ''}`
          .toLowerCase().includes(searchQuery.toLowerCase()))
    : customers;

  onMount(loadCustomers);
</script>

<div>
  <div class="page-header">
    <h1 class="page-title">Customers</h1>
    <button class="btn-primary" on:click={openNew}>+ Add Customer</button>
  </div>

  <!-- Search -->
  <div class="mb-4">
    <input class="input max-w-md" type="search" placeholder="Search customers..." bind:value={searchQuery} />
  </div>

  {#if loading}
    <LoadingSpinner />
  {:else if filtered.length === 0}
    <EmptyState icon="👥" title="No customers yet" message="Add your first customer to start creating estimates." />
  {:else}
    <div class="card p-0 table-wrapper">
      <table class="table">
        <thead><tr>
          <th>Name</th>
          <th class="hidden sm:table-cell">Company</th>
          <th class="hidden md:table-cell">Phone</th>
          <th class="hidden lg:table-cell">Email</th>
          <th class="hidden lg:table-cell">Added</th>
        </tr></thead>
        <tbody>
          {#each filtered as customer}
            <tr on:click={() => openEdit(customer)}>
              <td>
                <div class="flex items-center gap-2">
                  <span class="w-8 h-8 rounded-full bg-brand-100 text-brand-700 flex items-center justify-center text-xs font-bold flex-shrink-0">
                    {initials(customer.firstName, customer.lastName)}
                  </span>
                  <span class="font-medium">{customer.firstName} {customer.lastName}</span>
                </div>
              </td>
              <td class="hidden sm:table-cell">{customer.companyName ?? '—'}</td>
              <td class="hidden md:table-cell">{formatPhone(customer.phone)}</td>
              <td class="hidden lg:table-cell">{customer.email ?? '—'}</td>
              <td class="hidden lg:table-cell">{formatDate(customer.dateCreated)}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>

<!-- Customer Form Modal -->
<Modal bind:open={showForm} title={editingCustomer.id ? 'Edit Customer' : 'New Customer'}>
  <form on:submit|preventDefault={saveCustomer} class="space-y-4">
    <div class="grid grid-cols-2 gap-3">
      <div>
        <label class="label">First Name</label>
        <input class="input" bind:value={editingCustomer.firstName} required />
      </div>
      <div>
        <label class="label">Last Name</label>
        <input class="input" bind:value={editingCustomer.lastName} required />
      </div>
    </div>
    <div>
      <label class="label">Company</label>
      <input class="input" bind:value={editingCustomer.companyName} />
    </div>
    <div class="grid grid-cols-2 gap-3">
      <div>
        <label class="label">Email</label>
        <input class="input" type="email" bind:value={editingCustomer.email} />
      </div>
      <div>
        <label class="label">Phone</label>
        <input class="input" type="tel" bind:value={editingCustomer.phone} />
      </div>
    </div>
    <div>
      <label class="label">Address</label>
      <input class="input" bind:value={editingCustomer.address} />
    </div>
    <div class="grid grid-cols-3 gap-3">
      <div>
        <label class="label">City</label>
        <input class="input" bind:value={editingCustomer.city} />
      </div>
      <div>
        <label class="label">State</label>
        <input class="input" bind:value={editingCustomer.state} maxlength="2" />
      </div>
      <div>
        <label class="label">Zip</label>
        <input class="input" bind:value={editingCustomer.zip} />
      </div>
    </div>
    <div>
      <label class="label">Notes</label>
      <textarea class="input" rows="2" bind:value={editingCustomer.notes}></textarea>
    </div>
    <div class="flex justify-end gap-3 pt-2">
      <button type="button" class="btn-secondary" on:click={() => showForm = false}>Cancel</button>
      <button type="submit" class="btn-primary">{editingCustomer.id ? 'Update' : 'Add Customer'}</button>
    </div>
  </form>
</Modal>
