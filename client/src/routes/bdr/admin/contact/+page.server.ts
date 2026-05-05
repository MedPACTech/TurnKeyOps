import { error, fail } from '@sveltejs/kit';
import { resolveMvpScaffold } from '$lib/mvp';
import {
	isBdrContactAccessRole,
	loadBdrContactAccessRoles,
	saveBdrContactAccessRole
} from '$lib/server/bdr-contact-access';

export const load = async ({ fetch }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);
	const accessOverrides = await loadBdrContactAccessRoles();

	return {
		source,
		estimates: snapshot.estimates,
		invoices: snapshot.invoices,
		customers: snapshot.customers,
		accessOverrides
	};
};

export const actions = {
	updateAccessRole: async ({ locals, request }) => {
		if (!locals.bdrAdminSession) {
			throw error(403, 'Admin access is required to change contact app access.');
		}

		const formData = await request.formData();
		const contactId = String(formData.get('contactId') ?? '').trim();
		const role = String(formData.get('role') ?? '').trim();

		if (!contactId || !isBdrContactAccessRole(role)) {
			return fail(400, { message: 'Choose a valid contact and access role.' });
		}

		if (role === 'owner' && locals.bdrAdminSession.role !== 'owner') {
			throw error(403, 'Only an owner can grant owner access.');
		}

		await saveBdrContactAccessRole(contactId, role);

		return {
			message: 'Contact access saved.',
			updatedContactId: contactId,
			updatedRole: role
		};
	}
};
