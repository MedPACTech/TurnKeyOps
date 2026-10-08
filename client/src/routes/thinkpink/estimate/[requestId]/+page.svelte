<script lang="ts">
import CustomerProposal from '$lib/components/estimates/CustomerProposal.svelte';
import type {EstimatePacket} from '$lib/estimates';
let {data,form}=$props();
const packet=$derived(data.draft as unknown as EstimatePacket);
</script>
{#if packet.document}<CustomerProposal {packet} accessToken={data.accessToken} proposalBase={data.proposalBase} {form}/>
{:else}<p>This historical estimate is available through the office.</p>{/if}
