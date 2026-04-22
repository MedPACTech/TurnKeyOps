import { redirect } from '@sveltejs/kit';

export const load = ({ url }) => {
	const role = url.searchParams.get('role');
	const suffix = role ? `?role=${role}` : '';

	throw redirect(307, `/bdr/admin/dashboard${suffix}`);
};
