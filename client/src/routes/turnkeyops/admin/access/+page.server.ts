import { fail } from '@sveltejs/kit';
import { authTokenCookie } from '$lib/server/auth-session';
import {
	createPlatformCustomerAdminInvite,
	listPlatformManagedTenants
} from '$lib/server/user-administration';

export const load = async ({ fetch, cookies }) => ({
	managedTenants: await listPlatformManagedTenants(fetch, cookies.get(authTokenCookie))
});

export const actions = {
	inviteCustomerAdmin: async ({ request, fetch, cookies, url }) => {
		const formData = await request.formData();
		const tenantKey = String(formData.get('tenantKey') ?? '').trim();
		const email = String(formData.get('email') ?? '').trim();
		const phone = String(formData.get('phone') ?? '').trim();
		if (!tenantKey || (!email && !phone)) {
			return fail(400, { inviteError: 'Choose a tenant and enter an email address or mobile number.' });
		}

		try {
			const result = await createPlatformCustomerAdminInvite(
				fetch,
				cookies.get(authTokenCookie),
				tenantKey,
				{ email: email || undefined, phone: phone || undefined }
			);
			const token = result.invite.inviteToken;
			const invitePath = token
				? `/auth/invite/${result.invite.id}?token=${encodeURIComponent(token)}`
				: '';
			return {
				inviteSuccess: `Customer Admin invite created for ${result.tenantDisplayName}.`,
				inviteUrl: invitePath ? new URL(invitePath, url.origin).toString() : ''
			};
		} catch (cause) {
			return fail(400, {
				inviteError: cause instanceof Error ? cause.message : 'Could not create the Customer Admin invite.'
			});
		}
	}
};
