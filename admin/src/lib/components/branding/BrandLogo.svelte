<script lang="ts">
  export let compact = false;
  export let dark = false;
  export let showTagline = false;
  export let prominent = false;
  export let className = '';
</script>

<div class={`flex flex-col ${compact ? 'gap-2' : 'gap-4'} ${className}`}>
  <img
    src={dark ? '/turnkeyops-logo.png' : '/TransparentLogo.png'}
    alt="TurnKeyOps.ai"
    class={`object-contain ${
      compact
        ? 'h-14 w-auto'
        : prominent
          ? 'h-auto w-full max-w-[22rem] sm:max-w-[28rem] lg:max-w-[34rem]'
          : 'h-auto w-full max-w-[14rem] sm:max-w-[18rem]'
    }`}
  />

  {#if showTagline}
    <p class={`text-sm uppercase tracking-[0.24em] ${dark ? 'text-white/70' : 'text-ink-500'}`}>
      Back Office Software For Construction
    </p>
  {/if}
</div>
