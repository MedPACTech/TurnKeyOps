<script lang="ts">
import CustomerProposal from '$lib/components/estimates/CustomerProposal.svelte';
import type {EstimatePacket} from '$lib/estimates';
import LegacyProposal from './LegacyProposal.svelte';
let {data,form}=$props();
const packet=$derived(data.draft as unknown as EstimatePacket);
</script>
{#if packet.document}<CustomerProposal {packet} accessToken={data.accessToken} proposalBase={data.proposalBase} {form}/>
{:else}<LegacyProposal {data} {form}/>{/if}
