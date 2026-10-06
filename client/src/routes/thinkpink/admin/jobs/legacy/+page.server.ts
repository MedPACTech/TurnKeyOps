import { loadBdrScheduledJobs } from '$lib/server/bdr-job-scheduling';

export const load = async ({ fetch }) => {
	try {
		return { jobs: await loadBdrScheduledJobs(fetch), error: null, loadedAtUtc: new Date().toISOString() };
	} catch {
		return { jobs: [], error: 'Live production jobs could not be loaded.', loadedAtUtc: new Date().toISOString() };
	}
};
