<script lang="ts">
  import { toast } from '$stores/toast';

  const icons: Record<string, string> = {
    success: '✅', error: '❌', info: 'ℹ️', warning: '⚠️'
  };
  const colors: Record<string, string> = {
    success: 'bg-green-50 border-green-200 text-green-800',
    error: 'bg-red-50 border-red-200 text-red-800',
    info: 'bg-blue-50 border-blue-200 text-blue-800',
    warning: 'bg-yellow-50 border-yellow-200 text-yellow-800'
  };
</script>

<div class="fixed bottom-4 right-4 z-50 space-y-2 max-w-sm w-full">
  {#each $toast as t (t.id)}
    <div class="flex items-center gap-2 px-4 py-3 rounded-lg border shadow-lg text-sm {colors[t.type]}">
      <span>{icons[t.type]}</span>
      <p class="flex-1">{t.message}</p>
    </div>
  {/each}
</div>
