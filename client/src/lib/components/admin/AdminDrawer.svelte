<script lang="ts">
  import { onMount, type Snippet } from 'svelte';
  import { X } from 'lucide-svelte';
  let { title, close, children, side = 'right', width = '36rem' }: { title: string; close: () => void; children: Snippet; side?: 'left' | 'right'; width?: string } = $props();
  let dialog: HTMLDialogElement;
  onMount(() => {
    const previous = document.activeElement as HTMLElement | null;
    dialog.showModal();
    return () => { dialog.close(); if (previous?.isConnected) previous.focus(); };
  });
</script>
<dialog bind:this={dialog} aria-label={title} oncancel={(event) => { event.preventDefault(); close(); }} onclick={(event) => { if(event.target === dialog) { const rect=dialog.getBoundingClientRect(); if(event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) close(); } }} class:left={side === 'left'} style:width={`min(100%, ${width})`}>
  <div class="flex shrink-0 items-center justify-between gap-4 border-b border-[var(--border)] px-5 py-4">
    <h2 class="text-lg font-semibold text-[var(--text-strong)]">{title}</h2>
    <button type="button" class="btn-icon" aria-label={`Close ${title.toLowerCase()}`} onclick={close}><X size={20} aria-hidden="true" /></button>
  </div>
  <div class="min-h-0 flex-1 overflow-y-auto p-5">{@render children()}</div>
</dialog>
<style>
  dialog { position:fixed; inset:0 0 0 auto; margin:0; max-height:none; max-width:100%; height:100dvh; padding:0; border:0; border-left:1px solid var(--border); }
  dialog[open] { display:flex; flex-direction:column; }
  dialog.left { inset:0 auto 0 0; border-left:0; border-right:1px solid var(--border); }
</style>
