<script lang="ts">
	import type { ManagedTenantUser } from '$lib/server/user-administration';

	let {
		tenantName,
		users,
		form
	}: {
		tenantName: string;
		users: ManagedTenantUser[];
		form?: {
			message?: string;
			error?: string;
			inviteUrl?: string;
		} | null;
	} = $props();

	const roles = [
		{ key: 'admin', label: 'Customer Admin', detail: 'Can invite users and manage tenant settings.' },
		{ key: 'staff', label: 'Staff', detail: 'Can run customer, estimate, schedule, and job workflows.' },
		{ key: 'member', label: 'Member', detail: 'Standard tenant access without user administration.' }
	];
</script>

<svelte:head><title>Users · {tenantName}</title></svelte:head>

<div class="mx-auto max-w-7xl space-y-5 pb-10">
	<header>
		<p class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--accent-text)]">Identity & access</p>
		<h1 class="mt-2 text-3xl font-black tracking-tight text-[var(--text-strong)]">Users</h1>
		<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">
			Invite people to {tenantName}, choose what they can do, and remove access when responsibilities change. Users can access only this tenant.
		</p>
	</header>

	{#if form?.message}
		<p class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-800">{form.message}</p>
	{/if}
	{#if form?.error}
		<p class="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-semibold text-rose-800">{form.error}</p>
	{/if}
	{#if form?.inviteUrl}
		<div class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
			<p class="font-semibold">Share this one-time activation link securely:</p>
			<a href={form.inviteUrl} class="mt-1 block break-all underline">{form.inviteUrl}</a>
		</div>
	{/if}

	<div class="grid gap-5 xl:grid-cols-[0.72fr_1.28fr]">
		<form method="POST" action="?/invite" class="h-fit rounded-xl bg-white p-5 shadow-[var(--shell-shadow)]">
			<h2 class="text-lg font-bold text-[var(--text-strong)]">Invite a user</h2>
			<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">The invite must match the email address or mobile number the person verifies with iBeam.</p>
			<div class="mt-5 grid gap-4">
				<label class="grid gap-2 text-sm font-semibold text-[var(--text-strong)]">
					Work email
					<input name="email" type="email" autocomplete="email" placeholder="person@company.com" class="min-h-11 rounded-lg border border-[var(--shell-border)] px-3 font-normal outline-none focus:border-[var(--accent-border)]" />
				</label>
				<label class="grid gap-2 text-sm font-semibold text-[var(--text-strong)]">
					Mobile number <span class="font-normal text-[var(--text-muted)]">(optional)</span>
					<input name="phone" type="tel" autocomplete="tel" placeholder="+1 704 555 0100" class="min-h-11 rounded-lg border border-[var(--shell-border)] px-3 font-normal outline-none focus:border-[var(--accent-border)]" />
				</label>
				<label class="grid gap-2 text-sm font-semibold text-[var(--text-strong)]">
					Role
					<select name="role" class="min-h-11 rounded-lg border border-[var(--shell-border)] bg-white px-3 font-normal outline-none focus:border-[var(--accent-border)]">
						{#each roles as role}
							<option value={role.key}>{role.label} — {role.detail}</option>
						{/each}
					</select>
				</label>
				<button type="submit" class="min-h-11 rounded-lg bg-[var(--accent-text)] px-5 text-sm font-semibold text-white hover:opacity-90">Create invite</button>
			</div>
		</form>

		<section class="overflow-hidden rounded-xl bg-white shadow-[var(--shell-shadow)]">
			<div class="border-b border-[var(--shell-border)] px-5 py-4">
				<h2 class="text-lg font-bold text-[var(--text-strong)]">Tenant access</h2>
				<p class="mt-1 text-sm text-[var(--text-muted)]">{users.length} user record{users.length === 1 ? '' : 's'}</p>
			</div>
			{#if users.length}
				<div class="divide-y divide-[var(--shell-border)]">
					{#each users as user}
						<div class="grid gap-3 px-5 py-4 lg:grid-cols-[1fr_auto] lg:items-center">
							<div>
								<p class="font-semibold text-[var(--text-strong)]">{user.email || user.phone || 'Identity pending'}</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">{user.userId ? 'Verified iBeam user' : 'Awaiting invite acceptance'} · {user.status}</p>
							</div>
							{#if user.role === 'owner'}
								<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1.5 text-xs font-semibold text-[var(--accent-text)]">Owner</span>
							{:else}
								<div class="flex flex-wrap gap-2">
									<form method="POST" action="?/updateRole" class="flex gap-2">
										<input type="hidden" name="membershipId" value={user.membershipId} />
										<select name="role" value={user.role} class="min-h-9 rounded-md border border-[var(--shell-border)] bg-white px-2 text-xs">
											{#each roles as role}<option value={role.key}>{role.label}</option>{/each}
										</select>
										<button type="submit" class="min-h-9 rounded-md border border-[var(--shell-border)] px-3 text-xs font-semibold">Save role</button>
									</form>
									<form method="POST" action={user.inviteId && !user.userId ? '?/cancelInvite' : '?/remove'}>
										<input type="hidden" name="membershipId" value={user.membershipId} />
										<input type="hidden" name="inviteId" value={user.inviteId ?? ''} />
										<button type="submit" class="min-h-9 rounded-md border border-rose-200 px-3 text-xs font-semibold text-rose-700">
											{user.inviteId && !user.userId ? 'Cancel invite' : 'Remove access'}
										</button>
									</form>
								</div>
							{/if}
						</div>
					{/each}
				</div>
			{:else}
				<p class="px-5 py-10 text-center text-sm text-[var(--text-muted)]">No users have been invited yet.</p>
			{/if}
		</section>
	</div>
</div>
