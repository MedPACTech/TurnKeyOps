<script lang="ts">
 import ContactsManagement from '$lib/components/admin/ContactsManagement.svelte';
 import type { PageProps } from './$types';
 let {data,form}:PageProps=$props();
</script>
<ContactsManagement contacts={data.contacts} customerLinks={data.customerLinks} selectedId={data.selectedId} work={data.work} contactSaved={data.contactSaved} {form} canWrite={data.modulePermissions.includes('contacts.write')} canManagePeople={data.modulePermissions.includes('users.read')}/>
