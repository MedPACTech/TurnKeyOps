<script lang="ts">
  export let text = '';
</script>

<div class="flex items-start gap-3">
  <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-2xl shadow-sm">
    👷‍♂️
  </div>
  <div class="max-w-[85%] rounded-[1.4rem] rounded-tl-md bg-white px-4 py-3 shadow-sm ring-1 ring-black/5">
    <p class="text-[11px] font-semibold uppercase tracking-[0.24em] text-brand-600">Bob</p>
    <p class="mt-2 text-sm leading-6 text-ink-800">{text}</p>
  </div>
</div>
