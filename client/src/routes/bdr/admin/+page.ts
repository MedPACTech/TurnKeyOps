import { redirect } from '@sveltejs/kit';

export const load = () => {
	throw redirect(307, '/bdr/admin/bob');
};
