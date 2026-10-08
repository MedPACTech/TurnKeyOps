// Test-only routing state. This fixture is not a SvelteKit route or an auth bypass.
const params = new URLSearchParams(location.search);
export const page = { url: new URL(`/${params.get('tenant') || 'bdr'}/admin/${params.get('module') || 'dashboard'}${params.has('new') ? '?new=1' : ''}`, location.origin) };
