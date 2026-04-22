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

	const metrics = $derived([
		{ label: 'Contact records', value: String(contactRecords.length), detail: 'Customers, vendors, and employees presented in one shell model' },
		{
			label: 'Linked estimates',
			value: String(customerViews.filter((customer) => customer.openEstimateCount > 0).length),
			detail: 'Customer-side records with active quote work'
		},
		{ label: 'Source', value: data.source === 'api' ? 'API scaffold' : 'Fallback scaffold', detail: getScaffoldBanner(data.source) }
	]);

	$effect(() => {
		if (selectedContact && selectedContactId !== selectedContact.id) {
			selectedContactId = selectedContact.id;
		}
	});
</script>

<AdminWorkspace
	kicker="Contact"
	title="Shared contact desk for customers, vendors, and employees"
	description="The old customer screen is now a Contact surface. Type selection lives in the context rail, the focus rail lists records for that type, and the work area keeps tabs for practical operator detail."
	{metrics}
	contextLabel="Contact type"
	focusLabel="Contact list"
>
	{#snippet context()}
		<div class="space-y-3">
			{#each [
				{ key: 'customer' as const, label: 'Customer', detail: 'Accounts, properties, and linked revenue work' },
				{ key: 'vendor' as const, label: 'Vendor', detail: 'Suppliers and partner contacts used by the office' },
				{ key: 'employee' as const, label: 'Employee', detail: 'Internal operator and field-facing records' }
			] as option}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${contactType === option.key ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => {
						contactType = option.key;
						selectedContactId = '';
					}}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{option.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{option.detail}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			{#each visibleContacts as contact}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedContact?.id === contact.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedContactId = contact.id)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{contact.displayName}</p>
					<p class="mt-1 text-xs text-[var(--text-muted)]">{contact.title}</p>
					<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{contact.primaryContactEmail ?? 'No email'} · {contact.primaryContactPhone ?? 'No phone'}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedContact}
			<div class="space-y-4">
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

				<div class="flex flex-wrap gap-2 border-b border-[var(--shell-border)] pb-3">
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
				No contact records are available for this type.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
