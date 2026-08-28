import { fail } from '@sveltejs/kit';
import { authTokenCookie } from '$lib/server/auth-session';
import {
	cancelCurrentTenantInvite,
	createCurrentTenantInvite,
	listCurrentTenantUsers,
	removeCurrentTenantUser,
	updateCurrentTenantUserRole
} from '$lib/server/user-administration';

const allowedRoles = new Set(['admin', 'staff', 'member']);
const value = (data: FormData, key: string) => String(data.get(key) ?? '').trim();

export const load = async ({ fetch, cookies }) => ({
	tenantName: 'Pink Axe',
	users: await listCurrentTenantUsers(fetch, cookies.get(authTokenCookie))
});

export const actions = {
	invite: async ({ request, fetch, cookies, url }) => {
		const data = await request.formData();
		const email = value(data, 'email');
		const phone = value(data, 'phone');
		const role = value(data, 'role');
		if ((!email && !phone) || !allowedRoles.has(role)) return fail(400, { error: 'Enter a contact and choose a valid role.' });
		try {
			const invite = await createCurrentTenantInvite(fetch, cookies.get(authTokenCookie), { email: email || undefined, phone: phone || undefined, role });
			const path = invite.inviteToken ? `/auth/invite/${invite.id}?token=${encodeURIComponent(invite.inviteToken)}` : '';
			return { message: 'User invite created.', inviteUrl: path ? new URL(path, url.origin).toString() : '' };
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Could not create the invite.' });
		}
	},
	updateRole: async ({ request, fetch, cookies }) => {
		const data = await request.formData();
		const membershipId = value(data, 'membershipId');
		const role = value(data, 'role');
		if (!membershipId || !allowedRoles.has(role)) return fail(400, { error: 'Choose a valid user and role.' });
		try {
			await updateCurrentTenantUserRole(fetch, cookies.get(authTokenCookie), membershipId, role);
			return { message: 'User role updated.' };
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Could not update the role.' });
		}
	},
	remove: async ({ request, fetch, cookies }) => {
		const membershipId = value(await request.formData(), 'membershipId');
		if (!membershipId) return fail(400, { error: 'Choose a valid user.' });
		try {
			await removeCurrentTenantUser(fetch, cookies.get(authTokenCookie), membershipId);
			return { message: 'User access removed.' };
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Could not remove access.' });
		}
	},
	cancelInvite: async ({ request, fetch, cookies }) => {
		const inviteId = value(await request.formData(), 'inviteId');
		if (!inviteId) return fail(400, { error: 'Choose a valid invite.' });
		try {
			await cancelCurrentTenantInvite(fetch, cookies.get(authTokenCookie), inviteId);
			return { message: 'Invite cancelled.' };
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Could not cancel the invite.' });
		}
	}
};
