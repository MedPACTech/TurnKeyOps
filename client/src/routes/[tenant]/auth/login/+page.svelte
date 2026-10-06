<script lang="ts">
import Login from '../../../auth/login/+page.svelte';
let { data, form } = $props();
</script>
<Login {data} {form} />
