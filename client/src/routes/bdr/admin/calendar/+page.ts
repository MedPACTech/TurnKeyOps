import { resolveMvpScaffold } from '$lib/mvp';

export const load = async ({ fetch, data }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);

	return {
		source,
		snapshot,
		scheduleRequestId: data.scheduleRequestId,
		scheduledRequest: data.scheduledRequest
	};
};
