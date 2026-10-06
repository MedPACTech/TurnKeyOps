<script lang="ts">
 import ExternalAdminLayout from '../../../src/lib/components/admin/ExternalAdminLayout.svelte';
 import AdminWorkspace from '../../../src/lib/components/admin/AdminWorkspace.svelte';
 import BdrDashboard from '../../../src/routes/bdr/admin/dashboard/+page.svelte';
 import PinkDashboard from '../../../src/routes/thinkpink/admin/dashboard/+page.svelte';
 import ZipfRequests from '../../../src/routes/carlzipf/admin/requests/+page.svelte';
 import Contacts from '../../../src/lib/components/admin/ContactsManagement.svelte';
 import People from '../../../src/lib/components/admin/PeopleManagement.svelte';
 import type { TenantSlug } from '../../../src/lib/config/tenants';
 import BdrRequests from '../../../src/routes/bdr/admin/requests/+page.svelte';
 import Estimates from '../../../src/routes/bdr/admin/estimates/+page.svelte';
 import Invoices from '../../../src/routes/bdr/admin/invoices/+page.svelte';
 import Jobs from '../../../src/routes/bdr/admin/jobs/+page.svelte';
 import Settings from '../../../src/routes/bdr/admin/settings/+page.svelte';
 import Bob from '../../../src/routes/bdr/admin/bob/+page.svelte';
 import { createQuoteRequestFromForm } from '../../../src/lib/quote-requests';
 const request = createQuoteRequestFromForm({ id:'7909b5b4-0000-4000-8000-000000000001', tenantId:'fixture', companyName:'Test company', contactName:'Test customer', email:'test@example.invalid', phone:'555-0100', siteName:'Test patio', serviceAddress:'123 Test Street, Charlotte, NC 28202', serviceType:'Patio', propertyType:'Residential', requestedTimeline:'Next month', priority:'standard', need:'Patio replacement', attachments:[] });
 const requestsData: any = { requests:[request], employeeContacts:[], scheduleSiteVisitByRequestId:{}, scheduleSiteVisitBaseHref:'/bdr/admin/calendar',metrics:{newCount:1,activeCount:1} };
 const bobData: any = { tenant:{slug:'bdr',shortName:'BDR'},bobHref:'/bdr/admin/bob',estimatesHref:'/bdr/admin/estimates',conversations:[],selectedConversation:{id:'bob-home',mode:'general',title:'Ask Bob',messages:[{id:'test-message',role:'assistant',content:'What would you like to work on today?',suggestedReplies:[],actions:[]}]},estimateProgress:{complete:0,total:6,isComplete:false},estimateFollowups:[],estimateLabels:{dimensions:'Dimensions',depth:'Depth'} };
 const params = new URLSearchParams(location.search);
 const tenantSlug = (params.get('tenant') || 'bdr') as TenantSlug;
 const module = params.get('module') || 'dashboard';
 const restricted = params.has('restricted');
 let drawerOpen = $state(false);
 const session = { role: 'owner' as const, bobVoice: 'practical' as const, adminSession: { email: 'operator@example.invalid' }, ...(restricted ? { modulePermissions: ['requests.read', 'contacts.read'] } : {}) };
 // Synthetic records used only to exercise real rendered components.
 const dashboardData: any = { metrics:{activeJobs:2,pendingEstimates:3,openInvoices:1,collectedThisMonth:1500,openBalance:350,newRequests:2,assessmentReady:1,visitsScheduled:1,activeEstimates:3},requestInbox:[{}],scheduleReadyJobs:[{}],jobs:[{id:'fixture-job',status:'scheduled',siteName:'Test patio',scheduledDate:'2026-10-06',windowStart:'09:00',windowEnd:'11:00',crew:'Crew A'}],invoices:[{invoiceNumber:'TEST-01',customerName:'Test customer',siteName:'Test patio',balanceDue:350}],integrationState:{errors:[],loadedAtUtc:'2026-10-05T16:00:00Z'},requestSource:'fixture',source:'fixture',requests:[] };
</script>
<ExternalAdminLayout data={session} {tenantSlug}>
 {#if module === 'requests'}<BdrRequests data={requestsData} form={null} />
 {:else if module === 'estimates'}<Estimates data={{quoteRequests:[{...request,status:'qualified'}],estimateDefaults:{}} as any} form={null} params={{}} />
 {:else if module === 'invoices'}<Invoices data={{invoices:[],customers:[]} as any} form={null} params={{}} />
 {:else if module === 'jobs'}<Jobs data={{jobs:[],scheduleReadyJobs:[]} as any} form={null} params={{}} />
 {:else if module === 'settings'}<Settings data={{estimateDefaults:{defaultCrewSize:3,concreteCostPerYard:100},billingSettings:{depositPercentRequired:50}} as any} form={null} params={{}} />
 {:else if module === 'bob'}<Bob data={bobData} form={null} params={{}} />
 {:else if module === 'customers'}
  <Contacts contacts={[]} customerLinks={[]} selectedId={null} work={null} canWrite canManagePeople />
 {:else if module === 'users'}
  <People people={[]} canWrite canDelete />
 {:else if module === 'controls'}
  <AdminWorkspace title="Control verification" description="Shared controls and details" {drawerOpen} drawerTitle="Request details" closeDrawer={() => drawerOpen = false}>
   {#snippet work()}
    <div class="card space-y-4">
     <label class="label" for="test-input">Project name</label><input id="test-input" class="input" placeholder="Enter a project name" />
     <div class="flex flex-wrap gap-3"><button class="btn-primary">Save request</button><button class="btn-secondary" onclick={() => drawerOpen = true}>Open details</button><button class="btn-danger">Delete draft</button><button class="btn-ghost">Cancel</button></div>
     <div class="flex flex-wrap gap-3"><span class="badge-green">Complete</span><span class="badge-yellow">Needs review</span><span class="badge-red">Blocked</span><span class="badge-blue">Scheduled</span><span class="badge-gray">Draft</span></div>
    </div>
   {/snippet}
   {#snippet drawer()}<label for="detail-note" class="label">Note</label><input class="input" id="detail-note" /><button class="btn-primary mt-4" onclick={() => drawerOpen = false}>Save note</button>{/snippet}
  </AdminWorkspace>
 {:else if tenantSlug === 'bdr'}<BdrDashboard data={dashboardData} params={{}} />
 {:else if tenantSlug === 'thinkpink'}<PinkDashboard data={dashboardData} />
 {:else}<ZipfRequests data={{requests:[], loadError:null, selectedId:null} as any} form={null} params={{}} />{/if}
</ExternalAdminLayout>
