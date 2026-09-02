import { env } from '$env/dynamic/private';
import { error } from '@sveltejs/kit';
import type { ApiEnvelope } from '$lib/types/mvp';

const defaultApiBaseUrl = 'http://localhost:5178';

export const getTurnKeyApiBaseUrl = () =>
	(env.PUBLIC_TKO_API_BASE_URL || env.TKO_API_BASE_URL || defaultApiBaseUrl).replace(/\/$/, '');

export const getTurnKeyApiHeaders = (json = true) => {
	const headers: Record<string, string> = { Accept: 'application/json' };
	if (json) headers['Content-Type'] = 'application/json';

	const bearerToken = env.TKO_API_BEARER_TOKEN || env.TKO_API_TOKEN || env.TKO_API_AUTH_TOKEN;
	if (bearerToken) headers.Authorization = `Bearer ${bearerToken}`;
	return headers;
};

export const unwrapTurnKeyApiEnvelope = async <T>(response: Response, operation = 'TurnKey API call') => {
	if (!response.ok) throw error(response.status, `${operation} failed with ${response.status}`);

	const payload = (await response.json()) as ApiEnvelope<T> | T;
	if (payload && typeof payload === 'object' && 'success' in payload && 'data' in payload) {
		if (!payload.success) throw error(502, `${operation} was not successful`);
		return payload.data;
	}

	return payload as T;
};
