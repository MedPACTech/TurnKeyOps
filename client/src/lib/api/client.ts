import { browser } from '$app/environment';
import { env } from '$env/dynamic/public';

const defaultApiBaseUrl = 'http://localhost:5178';
export const authTokenStorageKey = 'auth.token';

type ApiErrorPayload = {
	message?: string;
	title?: string;
	error?: string;
	errors?: Array<{ message?: string }>;
};

type ApiEnvelope<T> = ApiErrorPayload & {
	success: boolean;
	data?: T;
};

export class ApiError extends Error {
	constructor(
		message: string,
		public readonly status: number,
		public readonly payload: unknown
	) {
		super(message);
		this.name = 'ApiError';
	}
}

export const getApiBaseUrl = () =>
	(env.PUBLIC_TKO_API_BASE_URL || defaultApiBaseUrl).replace(/\/+$/, '');

export const getStoredAuthToken = () =>
	browser ? window.localStorage.getItem(authTokenStorageKey) : null;

export const setStoredAuthToken = (token: string | null | undefined) => {
	if (!browser) return;
	if (token) {
		window.localStorage.setItem(authTokenStorageKey, token);
	} else {
		window.localStorage.removeItem(authTokenStorageKey);
	}
};

const isEnvelope = <T>(payload: unknown): payload is ApiEnvelope<T> =>
	typeof payload === 'object' && payload !== null && 'success' in payload;

const errorMessage = (status: number, payload: unknown) => {
	if (typeof payload === 'object' && payload !== null) {
		const body = payload as ApiErrorPayload;
		const errors = body.errors?.map((item) => item.message).filter(Boolean).join(', ');
		const message = errors || body.message || body.title || body.error;
		if (message) return message;
	}

	if (status === 401) return 'Your session has expired. Please sign in again.';
	if (status === 403) return 'You do not have access to perform this action.';
	if (status >= 500) return 'The TurnKeyOps API is temporarily unavailable.';
	return `The request failed with status ${status}.`;
};

export const apiRequest = async <T>(
	path: string,
	init: RequestInit = {},
	fetcher: typeof globalThis.fetch = globalThis.fetch,
	accessToken?: string | null
): Promise<T> => {
	const headers = new Headers(init.headers);
	const token = accessToken ?? getStoredAuthToken();

	if (token && !headers.has('Authorization')) {
		headers.set('Authorization', `Bearer ${token}`);
	}
	if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
		headers.set('Content-Type', 'application/json');
	}
	if (!headers.has('Accept')) headers.set('Accept', 'application/json');
	if (browser && !headers.has('X-Time-Zone')) {
		headers.set('X-Time-Zone', Intl.DateTimeFormat().resolvedOptions().timeZone);
	}

	const response = await fetcher(`${getApiBaseUrl()}${path.startsWith('/') ? path : `/${path}`}`, {
		...init,
		headers
	});

	let payload: unknown = null;
	const responseText = await response.text();
	if (responseText) {
		try {
			payload = JSON.parse(responseText);
		} catch {
			payload = responseText;
		}
	}

	if (!response.ok) {
		if (response.status === 401) setStoredAuthToken(null);
		throw new ApiError(errorMessage(response.status, payload), response.status, payload);
	}

	if (isEnvelope<T>(payload)) {
		if (!payload.success) {
			throw new ApiError(errorMessage(response.status, payload), response.status, payload);
		}
		return payload.data as T;
	}

	return payload as T;
};
