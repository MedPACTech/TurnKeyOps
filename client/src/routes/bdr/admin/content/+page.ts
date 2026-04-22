import { redirect } from '@sveltejs/kit';

export const load = ({ url }) => {
	const suffix = url.searchParams.toString();
	throw redirect(307, `/bdr/admin/website${suffix ? `?${suffix}` : ''}`);
};
