/** Attach the validated browser session only to this application's API. */
export const withApiSession = (request: Request, apiBaseUrl: string, token: string | undefined) => {
	if (!token) return request;
	const target = new URL(request.url);
	const api = new URL(apiBaseUrl);
	const apiPath = `${api.pathname.replace(/\/$/, '')}/api/`;
	if (target.origin !== api.origin || !target.pathname.startsWith(apiPath)) return request;
	const headers = new Headers(request.headers);
	headers.set('Authorization', `Bearer ${token}`);
	return new Request(request, { headers });
};
