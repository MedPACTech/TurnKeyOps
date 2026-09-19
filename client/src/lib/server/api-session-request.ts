/** Attach a validated admin session only to requests inside the configured API base. */
export const withApiSession = (request: Request, apiBaseUrl: string, token?: string | null): Request => {
	if (!token) return request;
	const target = new URL(request.url);
	const base = new URL(apiBaseUrl);
	const apiPath = `${base.pathname.replace(/\/$/, '')}/api`;
	if (target.origin !== base.origin || (target.pathname !== apiPath && !target.pathname.startsWith(`${apiPath}/`))) {
		return request;
	}
	const headers = new Headers(request.headers);
	// The caller's tenant-scoped session takes precedence over legacy shared credentials.
	headers.set('Authorization', `Bearer ${token}`);
	return new Request(request, { headers, redirect: 'error' });
};
