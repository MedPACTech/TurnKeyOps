<script lang="ts">
 let {name, identity = '', assigned = true}: {name:string; identity?:string; assigned?:boolean} = $props();
 const initials = $derived(assigned ? name.trim().split(/\s+/).filter(Boolean).slice(0,2).map(part=>part[0]).join('').toUpperCase() || '?' : '—');
 const colors = ['#155e75','#5b21b6','#9a3412','#166534','#9d174d','#1e40af'];
 const color = $derived(assigned ? colors[Array.from(identity || name).reduce((hash,char)=>(hash*31+char.charCodeAt(0))>>>0,0)%colors.length] : '#475569');
</script>
<span class="avatar" style:background={color} aria-hidden="true">{initials}</span>
<style>
 .avatar{display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;width:2rem;height:2rem;border-radius:50%;color:#fff;font-size:.7rem;font-weight:750;letter-spacing:.025em;line-height:1;vertical-align:middle}
</style>
