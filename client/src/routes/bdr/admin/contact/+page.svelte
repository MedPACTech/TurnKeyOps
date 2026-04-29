<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildCustomerViews, getScaffoldBanner } from '$lib/mvp-display';
	import type { PageProps } from './$types';

	type ContactType = 'customer' | 'vendor' | 'employee';

	type ContactRecord = ReturnType<typeof buildCustomerViews>[number] & {
		contactType: ContactType;
		title: string;
		team: string;
	};
	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};

	let { data }: PageProps = $props();

	const customerViews = $derived(buildCustomerViews(data.customers, data.estimates, data.invoices));
	const contactRecords = $derived<ContactRecord[]>([
		...customerViews.map((customer, index) => ({
			...customer,
			contactType: 'customer' as const,
			title: index % 2 === 0 ? 'Property owner' : 'Facility manager',
			team: 'Client account'
		})),
		...customerViews.slice(0, 3).map((customer, index) => ({
			...customer,
			id: `${customer.id}-vendor`,
			displayName: `${customer.displayName.split(' ')[0]} Supply Co.`,
			primaryContactName: ['Mason Reed', 'Tara Quinn', 'Jules Park'][index],
			primaryContactEmail: [`vendor${index + 1}@bdr-demo.local`][0],
			primaryContactPhone: '704-555-0130',
			contactType: 'vendor' as const,
			title: index === 0 ? 'Material supplier' : 'Trade partner',
			team: 'Vendor network'
		})),
		...customerViews.slice(0, 3).map((customer, index) => ({
			...customer,
			id: `${customer.id}-employee`,
			displayName: ['Jordan Ellis', 'Casey Morgan', 'Riley Stone'][index],
			primaryContactName: ['Jordan Ellis', 'Casey Morgan', 'Riley Stone'][index],
			primaryContactEmail: [`team${index + 1}@bdr-demo.local`][0],
			primaryContactPhone: '704-555-0188',
			contactType: 'employee' as const,
			title: index === 0 ? 'Office admin' : 'Estimator / field lead',
			team: 'BDR team'
		}))
	]);

	let contactType = $state<ContactType>('customer');
	let activeTab = $state<'overview' | 'activity' | 'files' | 'linked'>('overview');
	let selectedContactId = $state('');

	const visibleContacts = $derived(contactRecords.filter((contact) => contact.contactType === contactType));
	const selectedContact = $derived.by(() => {
		const current = visibleContacts.find((contact) => contact.id === selectedContactId);
		return current ?? visibleContacts[0] ?? null;
	});
	const contactTypeOptions = $derived([
		{
			key: 'customer' as const,
			label: 'Customers',
			count: contactRecords.filter((contact) => contact.contactType === 'customer').length,
			detail: 'Accounts and properties'
		},
		{
			key: 'vendor' as const,
			label: 'Vendors',
			count: contactRecords.filter((contact) => contact.contactType === 'vendor').length,
			detail: 'Suppliers and trade partners'
		},
		{
			key: 'employee' as const,
			label: 'Employees',
			count: contactRecords.filter((contact) => contact.contactType === 'employee').length,
			detail: 'Office and field operators'
		}
	]);

	const metrics = $derived([
		{ label: 'Contact records', value: String(contactRecords.length), detail: 'Customers, vendors, and employees presented in one shell model' },
		{
			label: 'Linked estimates',
			value: String(customerViews.filter((customer) => customer.openEstimateCount > 0).length),
			detail: 'Customer-side records with active quote work'
		},
		{ label: 'Source', value: data.source === 'api' ? 'API scaffold' : 'Fallback scaffold', detail: getScaffoldBanner(data.source) }
	]);
	const bobMoves = $derived.by(() => {
		if (!selectedContact) {
			return [
				{
					label: 'Review relationship desk',
					detail: `${visibleContacts.length} record${visibleContacts.length === 1 ? '' : 's'} in view`,
					href: '/bdr/admin/contact?role=office-admin'
				}
			] satisfies BobMove[];
		}

		return [
			{
				label: 'Prep follow-up',
				detail: selectedContact.nextStep,
				href: '#relationship-record'
			},
			{
				label: 'Summarize relationship',
				detail: `${selectedContact.property} · ${selectedContact.segment}`,
				href: '#relationship-record'
			},
			{
				label: 'Check linked work',
				detail: `${selectedContact.openEstimateCount} estimate link(s) · ${selectedContact.openInvoiceCount} invoice link(s)`,
				href: '#relationship-tabs'
			}
		] satisfies BobMove[];
	});

	$effect(() => {
		if (selectedContact && selectedContactId !== selectedContact.id) {
			selectedContactId = selectedContact.id;
		}
	});
</script>

<AdminWorkspace
	kicker="External Admin / Contacts"
	title="Relationship desk for customers, vendors, and team follow-up"
	description="Keep people, property context, and the next follow-up visible without turning the page into a dense CRM screen."
	{metrics}
	contextLabel="Contact type"
	focusLabel="Contact list"
>
	{#snippet context()}
		<div class="space-y-3">
			{#each contactTypeOptions as option}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${contactType === option.key ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => {
						contactType = option.key;
						selectedContactId = '';
					}}
				>
					<div class="flex items-center justify-between gap-3">
						<p class="text-sm font-semibold text-[var(--text-strong)]">{option.label}</p>
						<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--text-base)]">
							{option.count}
						</span>
					</div>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{option.detail}</p>
				</button>
			{/each}

			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<div class="flex items-start justify-between gap-3">
					<div>
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Bob relationship assist</p>
						<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{selectedContact?.displayName ?? 'Relationship queue'}</p>
					</div>
					<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm">
						✨
					</span>
				</div>
				<div class="mt-3 space-y-2">
					{#each bobMoves as move}
						<a
							href={move.href}
							class="block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5 transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]"
						>
							<p class="text-sm font-semibold text-[var(--text-strong)]">{move.label}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{move.detail}</p>
						</a>
					{/each}
				</div>
			</div>
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
				{visibleContacts.length} records
			</p>
			{#each visibleContacts as contact}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedContact?.id === contact.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedContactId = contact.id)}
				>
					<div class="flex items-start justify-between gap-3">
						<div class="min-w-0">
							<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{contact.displayName}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">{contact.title} · {contact.team}</p>
						</div>
						<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--text-base)]">
							{contact.status}
						</span>
					</div>
					<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{contact.property}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{contact.nextStep}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedContact}
			<div id="relationship-record" class="space-y-4">
				<div class="flex flex-wrap items-start justify-between gap-3">
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">{contactType}</p>
						<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedContact.displayName}</h4>
						<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedContact.title} · {selectedContact.team}</p>
					</div>
					<div class="text-right text-xs uppercase tracking-[0.18em] text-[var(--muted)]">
						<p>{selectedContact.status}</p>
						<p class="mt-1">{selectedContact.lifecycleStage}</p>
					</div>
				</div>

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Property / job</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.property}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Last touch</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.lastTouch}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next action</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.nextStep}</p>
					</div>
				</div>

				<div id="relationship-tabs" class="flex flex-wrap gap-2 border-b border-[var(--shell-border)] pb-3">
					{#each [
						{ key: 'overview' as const, label: 'Overview' },
						{ key: 'activity' as const, label: 'Activity' },
						{ key: 'files' as const, label: 'Files' },
						{ key: 'linked' as const, label: 'Linked Work' }
					] as tab}
						<button
							type="button"
							class={`rounded-full border px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] transition ${activeTab === tab.key ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] text-[var(--text-base)] hover:bg-[var(--shell-panel-strong)]'}`}
							onclick={() => (activeTab = tab.key)}
						>
							{tab.label}
						</button>
					{/each}
				</div>

				{#if activeTab === 'overview'}
					<div class="grid gap-3 md:grid-cols-2">
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Primary contact</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.primaryContactName ?? selectedContact.displayName}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedContact.primaryContactEmail ?? 'No email on file'}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedContact.primaryContactPhone ?? 'No phone on file'}</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Property / team context</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.property}</p>
							<p class="mt-2 text-sm text-[var(--text-muted)]">{selectedContact.segment}</p>
						</div>
					</div>
				{:else if activeTab === 'activity'}
					<div class="grid gap-3 md:grid-cols-2">
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Last touch</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{selectedContact.lastTouch}</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Current next step</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{selectedContact.nextStep}</p>
							<p class="mt-2 text-sm text-[var(--text-muted)]">{selectedContact.risk}</p>
						</div>
					</div>
				{:else if activeTab === 'files'}
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Files on record</p>
						<div class="mt-3 flex flex-wrap gap-2">
							{#each selectedContact.files as file}
								<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1.5 text-xs text-[var(--text-base)]">{file}</span>
							{/each}
						</div>
					</div>
				{:else}
					<div class="grid gap-3 md:grid-cols-2">
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Estimate links</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.openEstimateCount} active estimate link(s)</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Invoice links</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.openInvoiceCount} active invoice link(s)</p>
						</div>
					</div>
				{/if}
			</div>
		{:else}
			<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
				No relationship records are available in this view.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
