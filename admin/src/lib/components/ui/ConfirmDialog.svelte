<script lang="ts">
  import Modal from './Modal.svelte';

  export let open = false;
  export let title = 'Are you sure?';
  export let message = 'This action cannot be undone.';
  export let confirmLabel = 'Delete';
  export let danger = true;
  export let onConfirm: () => void = () => {};
</script>

<Modal bind:open {title} size="sm">
  <p class="text-sm text-gray-600 mb-6">{message}</p>
  <div class="flex justify-end gap-3">
    <button class="btn-secondary" on:click={() => open = false}>Cancel</button>
    <button
      class={danger ? 'btn-danger' : 'btn-primary'}
      on:click={() => { onConfirm(); open = false; }}
    >
      {confirmLabel}
    </button>
  </div>
</Modal>
