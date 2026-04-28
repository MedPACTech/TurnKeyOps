import { env } from '$env/dynamic/public';
import { error } from '@sveltejs/kit';
import { fallbackMvpSnapshot } from '$lib/mvp-data';
import type { ApiEnvelope, MvpScaffoldSnapshot, MvpScaffoldSource } from '$lib/types/mvp';

const defaultApiBaseUrl = 'http://localhost:5178';

export const getPublicApiBaseUrl = () =>
	(env.PUBLIC_TKO_API_BASE_URL || defaultApiBaseUrl).replace(/\/$/, '');

const hasConfiguredApiBaseUrl = () => Boolean(env.PUBLIC_TKO_API_BASE_URL?.trim());

export const loadMvpScaffold = async (fetch: typeof globalThis.fetch): Promise<MvpScaffoldSnapshot> => {
	const response = await fetch(`${getPublicApiBaseUrl()}/api/mvp/scaffold`);

	if (!response.ok) {
		throw error(response.status, 'Unable to load TurnKeyOps scaffold data');
	}

	const payload = (await response.json()) as ApiEnvelope<MvpScaffoldSnapshot>;

	if (!payload.success) {
		throw error(502, 'TurnKeyOps scaffold response was not successful');
	}

	return payload.data;
};

export const resolveMvpScaffold = async (
	fetch: typeof globalThis.fetch
): Promise<{ snapshot: MvpScaffoldSnapshot; source: MvpScaffoldSource }> => {
	if (!hasConfiguredApiBaseUrl()) {
		return { snapshot: fallbackMvpSnapshot, source: 'fallback' };
	}

	try {
		const snapshot = await loadMvpScaffold(fetch);
		return { snapshot, source: 'api' };
	} catch (cause) {
		console.warn('Falling back to local MVP scaffold snapshot.', cause);
		return { snapshot: fallbackMvpSnapshot, source: 'fallback' };
	}
};
