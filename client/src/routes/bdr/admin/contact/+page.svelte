<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import {
		bdrEmployeeContacts,
		bdrEmployeePermissionMeta,
		bdrEmployeeSkillMeta,
		type BdrEmployeeContact,
		type BdrEmployeePermissionKey,
		type BdrEmployeeSkillKey
	} from '$lib/bdr-team';
	import { buildCustomerViews, getScaffoldBanner } from '$lib/mvp-display';
	import { CheckCircle2 } from 'lucide-svelte';
	import type { PageProps } from './$types';

	type ContactType = 'customer' | 'vendor' | 'employee';
	type AdminAccessRole = 'none' | 'field' | 'office-admin' | 'owner';
	type PendingAccessChange = {
		contactId: string;
		contactName: string;
		fromRole: AdminAccessRole;
		toRole: AdminAccessRole;
	};

	type ContactRecord = ReturnType<typeof buildCustomerViews>[number] & {
		contactType: ContactType;
		title: string;
		team: string;
		employee?: BdrEmployeeContact;
	};
	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};

	let { data }: PageProps = $props();
	let contactType = $state<ContactType>('customer');
	let activeTab = $state<'overview' | 'activity' | 'files' | 'linked'>('overview');
	let selectedContactId = $state('');
	let accessOverrides = $state<Record<string, AdminAccessRole>>({});
	let pendingAccessChange = $state<PendingAccessChange | null>(null);
	let addedContacts = $state<ContactRecord[]>([]);
	let contactEdits = $state<Record<string, ContactRecord>>({});
	let contactDrawerOpen = $state(false);
	let editingContactId = $state<string | null>(null);
	let newContactType = $state<ContactType>('customer');
	let newContactSegment = $state('Residential property');
	let newDisplayName = $state('');
	let newPrimaryContactName = $state('');
	let newPrimaryContactEmail = $state('');
	let newPrimaryContactPhone = $state('');
	let newProperty = $state('');
	let newTitle = $state('');
	let newStatus = $state('Active');
	let newLifecycleStage = $state('Customer');
	let newEmployeeSkills = $state<BdrEmployeeSkillKey[]>([]);
	let newEmployeePermissions = $state<BdrEmployeePermissionKey[]>([]);

	const customerViews = $derived(buildCustomerViews(data.customers, data.estimates, data.invoices));
	const canManageEmployeeCapabilities = $derived(data.role === 'owner' || data.role === 'office-admin');
	const employeeSkillOptions = Object.keys(bdrEmployeeSkillMeta) as BdrEmployeeSkillKey[];
	const employeePermissionOptions = Object.keys(bdrEmployeePermissionMeta) as BdrEmployeePermissionKey[];
	const baseContactRecords = $derived<ContactRecord[]>([
		...customerViews.map((customer, index) => ({
			...customer,
			contactType: 'customer' as const,
			segment: index % 2 === 0 ? 'Residential property' : 'Commercial property',
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
			segment: index === 0 ? 'Material supplier' : 'Trade partner',
			title: index === 0 ? 'Material supplier' : 'Trade partner',
			team: 'Vendor network'
		})),
		...bdrEmployeeContacts.map((employee) => ({
			id: employee.id,
			displayName: employee.displayName,
			primaryContactName: employee.displayName,
			primaryContactEmail: employee.email,
			primaryContactPhone: employee.phone,
			status: employee.availability,
			lifecycleStage: 'Employee',
			property: `${employee.team} · ${employee.employmentType}`,
			segment: employee.employmentType,
			lastTouch: 'Capability profile ready for quote workflow assignment',
			nextStep: `Eligible for ${employee.skills.map((skill) => bdrEmployeeSkillMeta[skill].label).join(', ')}`,
			files: [],
			risk: employee.permissions.includes('manage-admin-access') ? 'Access admin privileges enabled' : 'Standard workflow access',
			openEstimateCount: 0,
			openInvoiceCount: 0,
			contactType: 'employee' as const,
			title: employee.title,
			team: employee.team,
			employee
		})),
		...addedContacts
	]);
	const contactRecords = $derived<ContactRecord[]>(baseContactRecords.map((contact) => contactEdits[contact.id] ?? contact));

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
	const accessRoleOptions: Array<{ key: AdminAccessRole; label: string; detail: string }> = [
		{ key: 'none', label: 'No app access', detail: 'Contact record only' },
		{ key: 'field', label: 'Field user', detail: 'Mobile field workflow' },
		{ key: 'office-admin', label: 'Office admin', detail: 'Can manage day-to-day admin work' },
		{ key: 'owner', label: 'Owner', detail: 'Full owner-level access' }
	];
	const contactClassificationOptions: Record<ContactType, string[]> = {
		customer: ['Residential property', 'Commercial property'],
		vendor: ['Material supplier', 'Equipment rental', 'Trade partner', 'Subcontractor', 'Service provider'],
		employee: ['Full time', 'Part time', 'Contractor', 'Seasonal']
	};

	const defaultAccessRole = (contact: ContactRecord): AdminAccessRole => {
		if (contact.contactType !== 'employee') return 'none';
		if (contact.employee) return contact.employee.accessRole;
		if (contact.title.toLowerCase().includes('office admin')) return 'office-admin';
		return 'field';
	};
	const getAccessRole = (contact: ContactRecord) => accessOverrides[contact.id] ?? defaultAccessRole(contact);
	const isAdminRole = (role: AdminAccessRole) => role === 'office-admin' || role === 'owner';
	const getAccessRoleLabel = (role: AdminAccessRole) =>
		accessRoleOptions.find((option) => option.key === role)?.label ?? 'No app access';
	const isCommercialContact = (contact: ContactRecord) => {
		const contactText = [contact.displayName, contact.property, contact.segment, contact.title, contact.team].join(' ').toLowerCase();
		return contactText.includes('commercial') || contactText.includes('retail') || contactText.includes('facility') || contactText.includes('hoa');
	};
	const getContactAvatar = (contact: ContactRecord) => {
		if (contact.contactType === 'vendor') return '🚚';
		if (contact.contactType === 'employee') return '👤';
		return isCommercialContact(contact) ? '🏢' : '🏠';
	};
	const getContactTypeLabel = (contact: ContactRecord) => {
		if (contact.contactType === 'vendor') return contact.segment || 'Vendor';
		if (contact.contactType === 'employee') return contact.segment || 'Employee';
		return isCommercialContact(contact) ? 'Commercial property' : 'Residential property';
	};
	const getContactAccent = (contact: ContactRecord) => {
		const statusText = [contact.status, contact.lifecycleStage, contact.risk].join(' ').toLowerCase();
		if (statusText.includes('collection') || statusText.includes('blocked') || statusText.includes('overdue')) return '#dc2626';
		if (statusText.includes('pending') || statusText.includes('approval') || statusText.includes('review')) return '#f59e0b';
		if (statusText.includes('active') || statusText.includes('production') || statusText.includes('ready') || statusText.includes('won')) return '#16a34a';
		return '#64748b';
	};
	const getContactAccentSoft = (contact: ContactRecord) => `${getContactAccent(contact)}1f`;
	const violetSelectionCardClass = 'border-transparent bg-violet-50 shadow-sm ring-1 ring-violet-200';
	const violetSelectionChipClass = 'border-transparent bg-violet-50 text-violet-700 shadow-sm ring-1 ring-violet-200';
	const violetPrimaryButtonClass =
		'rounded-md bg-violet-600 px-3 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-violet-500';
	const violetPrimaryButtonLargeClass =
		'rounded-md bg-violet-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50';
	const violetFieldClass =
		'h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none transition focus:border-violet-400 focus:ring-2 focus:ring-violet-100';
	const violetTextareaClass =
		'rounded-lg border border-[var(--shell-border)] bg-white px-3 py-3 text-sm text-[var(--text-strong)] outline-none transition focus:border-violet-400 focus:ring-2 focus:ring-violet-100';
	const resetContactDrawer = (type: ContactType = contactType) => {
		editingContactId = null;
		newContactType = type;
		newContactSegment = contactClassificationOptions[type][0];
		newDisplayName = '';
		newPrimaryContactName = '';
		newPrimaryContactEmail = '';
		newPrimaryContactPhone = '';
		newProperty = '';
		newTitle = type === 'employee' ? 'Team member' : type === 'vendor' ? 'Vendor contact' : 'Property owner';
		newStatus = type === 'employee' ? 'Active' : type === 'vendor' ? 'Pending approval' : 'Active';
		newLifecycleStage = type === 'employee' ? 'Employee' : type === 'vendor' ? 'Vendor' : 'Customer';
		newEmployeeSkills = type === 'employee' ? ['intake-review'] : [];
		newEmployeePermissions = type === 'employee' ? ['manage-quotes'] : [];
	};
	const setDrawerContactType = (type: ContactType) => {
		newContactType = type;
		newContactSegment = contactClassificationOptions[type][0];
		if (!newTitle.trim()) {
			newTitle = type === 'employee' ? 'Team member' : type === 'vendor' ? 'Vendor contact' : 'Property owner';
		}
		newLifecycleStage = type === 'employee' ? 'Employee' : type === 'vendor' ? 'Vendor' : 'Customer';
		if (type === 'employee') {
			newEmployeeSkills = newEmployeeSkills.length ? newEmployeeSkills : ['intake-review'];
			newEmployeePermissions = newEmployeePermissions.length ? newEmployeePermissions : ['manage-quotes'];
		} else {
			newEmployeeSkills = [];
			newEmployeePermissions = [];
		}
	};
	const openContactDrawer = (type: ContactType = contactType) => {
		resetContactDrawer(type);
		contactDrawerOpen = true;
	};
	const openEditContactDrawer = (contact: ContactRecord) => {
		editingContactId = contact.id;
		newContactType = contact.contactType;
		newContactSegment = contactClassificationOptions[contact.contactType].includes(contact.segment)
			? contact.segment
			: contactClassificationOptions[contact.contactType][0];
		newDisplayName = contact.displayName;
		newPrimaryContactName = contact.primaryContactName ?? '';
		newPrimaryContactEmail = contact.primaryContactEmail ?? '';
		newPrimaryContactPhone = contact.primaryContactPhone ?? '';
		newProperty = contact.property;
		newTitle = contact.title;
		newStatus = contact.status;
		newLifecycleStage = contact.lifecycleStage;
		newEmployeeSkills = contact.employee?.skills ?? [];
		newEmployeePermissions = contact.employee?.permissions ?? [];
		contactDrawerOpen = true;
	};
	const closeContactDrawer = () => {
		contactDrawerOpen = false;
		editingContactId = null;
	};
	const toggleEmployeeSkill = (skill: BdrEmployeeSkillKey) => {
		if (!canManageEmployeeCapabilities) return;
		newEmployeeSkills = newEmployeeSkills.includes(skill)
			? newEmployeeSkills.filter((entry) => entry !== skill)
			: [...newEmployeeSkills, skill];
	};
	const toggleEmployeePermission = (permission: BdrEmployeePermissionKey) => {
		if (!canManageEmployeeCapabilities) return;
		newEmployeePermissions = newEmployeePermissions.includes(permission)
			? newEmployeePermissions.filter((entry) => entry !== permission)
			: [...newEmployeePermissions, permission];
	};
	const saveContact = () => {
		const displayName = newDisplayName.trim();
		if (!displayName) return;

		const primaryContactName = newPrimaryContactName.trim() || displayName;
		const existingContact = editingContactId ? contactRecords.find((contact) => contact.id === editingContactId) : null;
		const id = existingContact?.id ?? `manual-${Date.now()}`;
		const title = newTitle.trim() || (newContactType === 'employee' ? 'Team member' : newContactType === 'vendor' ? 'Vendor contact' : 'Property owner');
		const team = newContactType === 'employee' ? existingContact?.team ?? 'BDR team' : newContactType === 'vendor' ? 'Vendor network' : 'Client account';
		const employeeAccessRole =
			existingContact?.employee?.accessRole ??
			(title.toLowerCase().includes('owner') ? 'owner' : title.toLowerCase().includes('office admin') ? 'office-admin' : 'field');
		const employee =
			newContactType === 'employee'
				? ({
						id,
						displayName,
						title,
						team,
						employmentType: newContactSegment as BdrEmployeeContact['employmentType'],
						email: newPrimaryContactEmail.trim(),
						phone: newPrimaryContactPhone.trim(),
						accessRole: employeeAccessRole,
						skills: [...new Set(newEmployeeSkills)],
						permissions: [...new Set(newEmployeePermissions)],
						availability: newStatus.trim() || 'Available',
						workload: existingContact?.employee?.workload ?? 2
					} satisfies BdrEmployeeContact)
				: undefined;
		const contact: ContactRecord = {
			...(existingContact ?? {}),
			id,
			displayName,
			primaryContactName,
			primaryContactEmail: newPrimaryContactEmail.trim() || null,
			primaryContactPhone: newPrimaryContactPhone.trim() || null,
			status: newStatus.trim() || 'Active',
			lifecycleStage: newLifecycleStage.trim() || (newContactType === 'employee' ? 'Employee' : newContactType === 'vendor' ? 'Vendor' : 'Customer'),
			property: newProperty.trim() || (newContactType === 'employee' ? 'BDR team' : 'No address on file'),
			segment: newContactSegment,
			lastTouch: existingContact?.lastTouch ?? 'New contact added',
			nextStep:
				newContactType === 'employee'
					? employee?.skills.length
						? `Eligible for ${employee.skills.map((skill) => bdrEmployeeSkillMeta[skill].label).join(', ')}`
						: 'No workflow skills selected'
					: existingContact?.nextStep ?? 'Complete contact profile and next follow-up',
			files: existingContact?.files ?? [],
			risk:
				newContactType === 'employee'
					? employee?.permissions.includes('manage-admin-access')
						? 'Access admin privileges enabled'
						: 'Standard workflow access'
					: existingContact?.risk ?? 'New contact needs review',
			openEstimateCount: existingContact?.openEstimateCount ?? 0,
			openInvoiceCount: existingContact?.openInvoiceCount ?? 0,
			contactType: newContactType,
			title,
			team,
			employee
		};

		if (existingContact) {
			contactEdits = { ...contactEdits, [id]: contact };
		} else {
			addedContacts = [contact, ...addedContacts];
		}
		contactType = newContactType;
		selectedContactId = id;
		activeTab = 'overview';
		contactDrawerOpen = false;
		editingContactId = null;
	};
	const requestAccessChange = (contact: ContactRecord, toRole: AdminAccessRole) => {
		const fromRole = getAccessRole(contact);
		if (fromRole === toRole) return;
		if (isAdminRole(toRole)) {
			pendingAccessChange = {
				contactId: contact.id,
				contactName: contact.primaryContactName ?? contact.displayName,
				fromRole,
				toRole
			};
			return;
		}
	};

	const metrics = $derived([
		{ label: 'Contact records', value: String(contactRecords.length), detail: 'Customers, vendors, and employees presented in one shell model' },
		{
			label: 'Admin users',
			value: String(contactRecords.filter((contact) => isAdminRole(getAccessRole(contact))).length),
			detail: 'Contacts with office admin or owner access'
		},
		{
			label: 'Linked estimates',
			value: String(customerViews.filter((customer) => customer.openEstimateCount > 0).length),
			detail: 'Customer-side records with active quote work'
		},
		{ label: 'Source', value: data.source === 'api' ? 'API scaffold' : 'Fallback scaffold', detail: getScaffoldBanner(data.source) }
	]);
	const selectedContactHeaderMetrics = $derived.by(() => {
		if (!selectedContact) return [];

		return [
			{
				label: 'Relationship type',
				value: getContactTypeLabel(selectedContact),
				detail: selectedContact.segment
			},
			{
				label: 'Work surface',
				value: selectedContact.lifecycleStage,
				detail: selectedContact.team
			},
			{
				label: 'Open estimates',
				value: String(selectedContact.openEstimateCount),
				detail: 'Linked quote work'
			},
			{
				label: 'Open invoices',
				value: String(selectedContact.openInvoiceCount),
				detail: 'Linked billing work'
			}
		];
	});
	const bobMoves = $derived.by(() => {
		if (!selectedContact) {
			return [
				{
					label: 'Review relationship desk',
					detail: `${visibleContacts.length} record${visibleContacts.length === 1 ? '' : 's'} in view`,
					href: '/bdr/admin/contact'
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

	$effect(() => {
		accessOverrides = { ...data.accessOverrides };
	});
</script>

<AdminWorkspace
	kicker="External Admin / Contacts"
	title="Contacts"
	description="Keep people, property context, and the next follow-up visible without turning the page into a dense CRM screen."
	{metrics}
	contextLabel="Blades"
	focusLabel="Filtered contacts"
	drawerOpen={contactDrawerOpen}
	drawerTitle={editingContactId ? 'Edit Contact' : 'Add Contact'}
	closeDrawer={closeContactDrawer}
>
	{#snippet context()}
		<div class="space-y-3">
			{#each contactTypeOptions as option}
				<button
					type="button"
					class={`w-full rounded-lg border px-3 py-3 text-left transition ${contactType === option.key ? violetSelectionCardClass : 'border-transparent bg-white/80 shadow-sm hover:bg-violet-50/70'}`}
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

			<div class="rounded-lg bg-white/80 p-3 shadow-sm">
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
							class="block rounded-lg bg-[var(--shell-panel-strong)] px-3 py-2.5 shadow-sm transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]"
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
			<div class="flex items-center justify-between gap-3">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
					{visibleContacts.length} records
				</p>
				<button
					type="button"
					class={violetPrimaryButtonClass}
					onclick={() => openContactDrawer(contactType)}
				>
					+ Add Contact
				</button>
			</div>
			{#each visibleContacts as contact}
				<button
					type="button"
					style={`--contact-accent: ${getContactAccent(contact)}; --contact-accent-soft: ${getContactAccentSoft(contact)}; border-top-color: var(--contact-accent);`}
					class={`group w-full overflow-hidden rounded-lg border border-t-[3px] border-x-transparent border-b-transparent p-0 text-left transition ${selectedContact?.id === contact.id ? violetSelectionCardClass : 'bg-white/85 shadow-sm hover:bg-violet-50/70'}`}
					onclick={() => (selectedContactId = contact.id)}
				>
					<div class="flex min-h-[7.25rem] items-stretch">
						<div class={`flex w-16 shrink-0 items-center justify-center border-r border-[var(--shell-border)] ${selectedContact?.id === contact.id ? 'bg-white/55' : 'bg-[var(--shell-panel-strong)]/80 group-hover:bg-white/70'}`}>
							<span class="flex h-10 w-10 items-center justify-center rounded-full bg-white text-xl shadow-sm">
								{getContactAvatar(contact)}
							</span>
						</div>
						<div class="min-w-0 flex-1 px-3 py-3">
							<div class="flex items-start justify-between gap-2">
								<div class="min-w-0">
									<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{contact.displayName}</p>
									<p class="mt-1 truncate text-xs text-[var(--text-muted)]">{getContactTypeLabel(contact)} · {contact.title}</p>
								</div>
							</div>
							<div class="mt-3 space-y-1">
								<p class="truncate text-xs font-medium text-[var(--text-base)]">{contact.primaryContactName ?? contact.displayName}</p>
								{#if contact.primaryContactEmail}
									<p class="truncate text-xs text-[var(--text-muted)]">{contact.primaryContactEmail}</p>
								{/if}
								{#if contact.primaryContactPhone}
									<p class="truncate text-xs text-[var(--text-muted)]">{contact.primaryContactPhone}</p>
								{/if}
							</div>
							{#if contact.property}
								<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{contact.property}</p>
							{/if}
						</div>
					</div>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedContact}
			<div id="relationship-record" class="space-y-4">
				<div class="rounded-[1.4rem] border border-violet-100 bg-gradient-to-r from-violet-50 via-white to-white p-5 shadow-[var(--shell-shadow)]">
					<div class="flex flex-wrap items-start justify-between gap-4">
						<div class="min-w-0">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.22em] text-violet-700">{contactType} blade</p>
							<h4 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{selectedContact.displayName}</h4>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedContact.title} · {selectedContact.team}</p>
						</div>
						<div
							class="flex flex-col items-end gap-2"
							style={`--contact-accent: ${getContactAccent(selectedContact)}; --contact-accent-soft: ${getContactAccentSoft(selectedContact)};`}
						>
							<button type="button" class={violetPrimaryButtonClass} onclick={() => openEditContactDrawer(selectedContact)}>
								Edit Contact
							</button>
							<span
								class="rounded-md border px-3 py-1.5 text-[0.65rem] font-semibold uppercase tracking-[0.16em]"
								style="border-color: var(--contact-accent); background: var(--contact-accent-soft); color: var(--contact-accent);"
							>
								{selectedContact.status}
							</span>
						</div>
					</div>

					<div class="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
						{#each selectedContactHeaderMetrics as metric}
							<div class="rounded-2xl border border-violet-100/80 bg-white/90 p-4 shadow-sm">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">{metric.label}</p>
								<p class="mt-3 text-xl font-semibold text-[var(--text-strong)]">{metric.value}</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">{metric.detail}</p>
							</div>
						{/each}
					</div>
				</div>

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Property / job</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.property}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Last touch</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.lastTouch}</p>
					</div>
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
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
							class={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${activeTab === tab.key ? violetSelectionChipClass : 'border-transparent bg-white/80 text-[var(--text-base)] shadow-sm hover:bg-violet-50/70'}`}
							onclick={() => (activeTab = tab.key)}
						>
							{tab.label}
						</button>
					{/each}
				</div>

				{#if activeTab === 'overview'}
					<div class="grid gap-3 md:grid-cols-2">
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Primary contact</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.primaryContactName ?? selectedContact.displayName}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedContact.primaryContactEmail ?? 'No email on file'}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedContact.primaryContactPhone ?? 'No phone on file'}</p>
						</div>
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Property / team context</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.property}</p>
							<p class="mt-2 text-sm text-[var(--text-muted)]">{selectedContact.segment}</p>
						</div>
					</div>
					{#if selectedContact.contactType === 'employee'}
						<div class="grid gap-3 md:grid-cols-2">
							<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Workflow skills</p>
								<div class="mt-3 flex flex-wrap gap-2">
									{#each selectedContact.employee?.skills ?? [] as skill}
										<span class="rounded-full bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-700">
											{bdrEmployeeSkillMeta[skill].label}
										</span>
									{/each}
								</div>
								<p class="mt-3 text-xs leading-5 text-[var(--text-muted)]">
									Skills drive automatic quote task assignment before an admin overrides the owner.
								</p>
							</div>
							<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
								<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Workflow permissions</p>
								<div class="mt-3 flex flex-wrap gap-2">
									{#each selectedContact.employee?.permissions ?? [] as permission}
										<span class="rounded-full bg-[var(--accent-soft)] px-3 py-1.5 text-xs font-semibold text-[var(--accent-text)]">
											{bdrEmployeePermissionMeta[permission].label}
										</span>
									{/each}
								</div>
								<p class="mt-3 text-xs leading-5 text-[var(--text-muted)]">
									Permissions control which workflow buttons can choose this employee automatically.
								</p>
							</div>
						</div>
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<div class="flex flex-wrap items-start justify-between gap-3">
								<div>
									<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Admin access</p>
									<p class="mt-2 text-sm text-[var(--text-base)]">
										Current access: <span class="font-semibold text-[var(--text-strong)]">{getAccessRoleLabel(getAccessRole(selectedContact))}</span>
									</p>
								</div>
								<span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-semibold text-[var(--accent-text)]">
									Internal user
								</span>
							</div>
							<div class="mt-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
								{#each accessRoleOptions as option}
									<form method="POST" action="?/updateAccessRole">
										<input type="hidden" name="contactId" value={selectedContact.id} />
										<input type="hidden" name="role" value={option.key} />
										<button
											type="submit"
											class={`h-full w-full rounded-lg px-3 py-3 text-left shadow-sm transition ${getAccessRole(selectedContact) === option.key ? 'bg-violet-50 ring-1 ring-violet-200' : 'bg-[var(--shell-panel-strong)] hover:bg-violet-50/70'}`}
											onclick={(event) => {
												if (isAdminRole(option.key) && getAccessRole(selectedContact) !== option.key) {
													event.preventDefault();
													requestAccessChange(selectedContact, option.key);
												}
											}}
										>
											<p class="text-sm font-semibold text-[var(--text-strong)]">{option.label}</p>
											<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{option.detail}</p>
										</button>
									</form>
								{/each}
							</div>
						</div>
					{/if}
				{:else if activeTab === 'activity'}
					<div class="grid gap-3 md:grid-cols-2">
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Last touch</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{selectedContact.lastTouch}</p>
						</div>
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Current next step</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{selectedContact.nextStep}</p>
							<p class="mt-2 text-sm text-[var(--text-muted)]">{selectedContact.risk}</p>
						</div>
					</div>
				{:else if activeTab === 'files'}
					<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Files on record</p>
						<div class="mt-3 flex flex-wrap gap-2">
							{#each selectedContact.files as file}
								<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1.5 text-xs text-[var(--text-base)]">{file}</span>
							{/each}
						</div>
					</div>
				{:else}
					<div class="grid gap-3 md:grid-cols-2">
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
							<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Estimate links</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{selectedContact.openEstimateCount} active estimate link(s)</p>
						</div>
						<div class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
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

	{#snippet drawer()}
		<div class="space-y-4">
			<div class="grid gap-2">
				<span class="text-sm font-semibold text-[var(--text-base)]">Contact type</span>
				<div class="grid grid-cols-3 gap-2">
					{#each contactTypeOptions as option}
						<button
							type="button"
							class={`rounded-lg px-3 py-3 text-sm font-semibold shadow-sm transition ${newContactType === option.key ? 'bg-violet-50 text-violet-700 ring-1 ring-violet-200' : 'bg-white text-[var(--text-base)] hover:bg-violet-50/70'}`}
							onclick={() => setDrawerContactType(option.key)}
						>
							{option.label.replace(/s$/, '')}
						</button>
					{/each}
				</div>
			</div>

			<label class="grid gap-2">
				<span class="text-sm font-semibold text-[var(--text-base)]">
					{newContactType === 'customer' ? 'Customer type' : newContactType === 'vendor' ? 'Vendor type' : 'Employee type'}
				</span>
				<select bind:value={newContactSegment} class={violetFieldClass}>
					{#each contactClassificationOptions[newContactType] as option}
						<option value={option}>{option}</option>
					{/each}
				</select>
			</label>

			<label class="grid gap-2">
				<span class="text-sm font-semibold text-[var(--text-base)]">
					{newContactType === 'employee' ? 'Employee name' : newContactType === 'vendor' ? 'Vendor name' : 'Customer / property name'}
				</span>
				<input bind:value={newDisplayName} class={violetFieldClass} placeholder="Name" />
			</label>

			<div class="grid gap-4 sm:grid-cols-2">
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Primary contact</span>
					<input bind:value={newPrimaryContactName} class={violetFieldClass} placeholder="Contact name" />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Role / title</span>
					<input bind:value={newTitle} class={violetFieldClass} />
				</label>
			</div>

			<div class="grid gap-4 sm:grid-cols-2">
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Email</span>
					<input bind:value={newPrimaryContactEmail} type="email" class={violetFieldClass} placeholder="name@example.com" />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Phone</span>
					<input bind:value={newPrimaryContactPhone} type="tel" class={violetFieldClass} placeholder="704-555-0100" />
				</label>
			</div>

			<label class="grid gap-2">
				<span class="text-sm font-semibold text-[var(--text-base)]">{newContactType === 'employee' ? 'Team / context' : 'Address / context'}</span>
				<textarea bind:value={newProperty} rows="3" class={violetTextareaClass} placeholder={newContactType === 'employee' ? 'Office, field, dispatch...' : 'Street, city, notes...'}></textarea>
			</label>

			<div class="grid gap-4 sm:grid-cols-2">
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Status</span>
					<input bind:value={newStatus} class={violetFieldClass} />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Work surface label</span>
					<input bind:value={newLifecycleStage} class={violetFieldClass} />
				</label>
			</div>

			{#if newContactType === 'employee'}
				<div class="space-y-4 rounded-lg bg-white/80 p-4 shadow-sm">
					<div class="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
						<div>
							<p class="text-sm font-semibold text-[var(--text-strong)]">Workflow skills</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Skills decide which quote task this employee can be auto-picked for.</p>
						</div>
						<span class="w-fit rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-semibold text-[var(--accent-text)]">
							{canManageEmployeeCapabilities ? 'Editable' : 'Owner/admin only'}
						</span>
					</div>
					<div class="grid gap-2 sm:grid-cols-2">
						{#each employeeSkillOptions as skill}
							{@const isSelected = newEmployeeSkills.includes(skill)}
							<button
								type="button"
								class={`min-h-16 rounded-lg px-3 py-3 text-left shadow-sm transition disabled:cursor-not-allowed disabled:opacity-60 ${isSelected ? 'bg-violet-50 text-violet-700 ring-1 ring-violet-200' : 'bg-[var(--shell-panel-strong)] text-[var(--text-base)] hover:bg-violet-50/70'}`}
								aria-pressed={isSelected}
								disabled={!canManageEmployeeCapabilities}
								onclick={() => toggleEmployeeSkill(skill)}
							>
								<span class="flex items-center gap-2 text-sm font-semibold">
									{#if isSelected}<CheckCircle2 size={15} />{/if}
									{bdrEmployeeSkillMeta[skill].label}
								</span>
								<span class="mt-1 block text-xs leading-5 opacity-80">{bdrEmployeeSkillMeta[skill].detail}</span>
							</button>
						{/each}
					</div>
				</div>

				<div class="space-y-4 rounded-lg bg-white/80 p-4 shadow-sm">
					<div>
						<p class="text-sm font-semibold text-[var(--text-strong)]">Workflow permissions</p>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Permissions control which workflow actions can assign work to this employee.</p>
					</div>
					<div class="grid gap-2 sm:grid-cols-2">
						{#each employeePermissionOptions as permission}
							{@const isSelected = newEmployeePermissions.includes(permission)}
							<button
								type="button"
								class={`min-h-16 rounded-lg px-3 py-3 text-left shadow-sm transition disabled:cursor-not-allowed disabled:opacity-60 ${isSelected ? 'bg-violet-50 text-violet-700 ring-1 ring-violet-200' : 'bg-[var(--shell-panel-strong)] text-[var(--text-base)] hover:bg-violet-50/70'}`}
								aria-pressed={isSelected}
								disabled={!canManageEmployeeCapabilities}
								onclick={() => toggleEmployeePermission(permission)}
							>
								<span class="flex items-center gap-2 text-sm font-semibold">
									{#if isSelected}<CheckCircle2 size={15} />{/if}
									{bdrEmployeePermissionMeta[permission].label}
								</span>
								<span class="mt-1 block text-xs leading-5 opacity-80">{bdrEmployeePermissionMeta[permission].detail}</span>
							</button>
						{/each}
					</div>
				</div>
			{/if}

			<div class="flex flex-col gap-2 sm:flex-row">
				<button
					type="button"
					class={violetPrimaryButtonLargeClass}
					disabled={!newDisplayName.trim()}
					onclick={saveContact}
				>
					{editingContactId ? 'Save Contact' : 'Add Contact'}
				</button>
				<button
					type="button"
					class="rounded-md bg-white px-5 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]"
					onclick={closeContactDrawer}
				>
					Cancel
				</button>
			</div>
		</div>
	{/snippet}
</AdminWorkspace>

{#if pendingAccessChange}
	<div class="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 px-4 py-6">
		<section class="w-full max-w-md rounded-lg bg-white p-5 shadow-[0_24px_72px_rgba(15,23,42,0.22)]">
			<p class="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent-text)]">Confirm admin access</p>
			<h2 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">
				Make {pendingAccessChange.contactName} an {getAccessRoleLabel(pendingAccessChange.toRole)}?
			</h2>
			<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">
				This changes app permissions from {getAccessRoleLabel(pendingAccessChange.fromRole)} to {getAccessRoleLabel(pendingAccessChange.toRole)}. Admin users can see and manage operating surfaces, so this needs an explicit confirmation.
			</p>
			<div class="mt-5 flex flex-col gap-2 sm:flex-row sm:justify-end">
				<button
					type="button"
					class="rounded-lg bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm ring-1 ring-[var(--shell-border)]"
					onclick={() => (pendingAccessChange = null)}
				>
					Cancel
				</button>
				<form method="POST" action="?/updateAccessRole">
					<input type="hidden" name="contactId" value={pendingAccessChange.contactId} />
					<input type="hidden" name="role" value={pendingAccessChange.toRole} />
					<button
						type="submit"
						class="w-full rounded-lg bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm"
					>
						Confirm access
					</button>
				</form>
			</div>
		</section>
	</div>
{/if}
