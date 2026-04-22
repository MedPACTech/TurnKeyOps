/**
 * Auth store — manages JWT token, user profile, and tenant context.
 * Writable Svelte 5 runes-compatible store.
 */
import { browser } from '$app/environment';
import { writable, derived } from 'svelte/store';

interface AuthUser {
  userId: string;
  email: string;
  tenantId: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

interface AuthResultLike {
  accessToken?: string;
  token?: string | {
    accessToken?: string;
    refreshToken?: string;
    [key: string]: unknown;
  };
  refreshToken?: string;
  auth?: {
    accessToken?: string;
    token?: string;
    refreshToken?: string;
  };
}

function parseJwt(token: string): Record<string, unknown> {
  try {
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(base64));
  } catch {
    return {};
  }
}

function createAuthStore() {
  const stored = browser && typeof localStorage?.getItem === 'function'
    ? localStorage.getItem('turnkeyops_token')
    : null;

  const { subscribe, set, update } = writable<{ token: string | null; user: AuthUser | null }>({
    token: stored,
    user: stored ? extractUser(stored) : null
  });

  function extractUser(token: string): AuthUser | null {
    const claims = parseJwt(token);
    if (!claims.sub) return null;
    return {
      userId: claims.sub as string,
      email: (claims.email ?? claims.unique_name ?? '') as string,
      tenantId: (claims.tenant_id ?? claims.tenant ?? claims.tid ?? '') as string,
      firstName: (claims.given_name ?? '') as string,
      lastName: (claims.family_name ?? '') as string,
      roles: Array.isArray(claims.role)
        ? claims.role as string[]
        : (claims.role ? [claims.role as string] : [])
    };
  }

  function extractToken(result: AuthResultLike): string | null {
    if (typeof result.token === 'object' && result.token !== null) {
      return (result.token.accessToken as string | undefined) ?? null;
    }

    return result.accessToken
      ?? result.token
      ?? result.auth?.accessToken
      ?? result.auth?.token
      ?? null;
  }

  function extractRefreshToken(result: AuthResultLike): string | null {
    if (typeof result.token === 'object' && result.token !== null) {
      return (result.token.refreshToken as string | undefined) ?? null;
    }

    return result.refreshToken
      ?? result.auth?.refreshToken
      ?? null;
  }

  return {
    subscribe,
    login(token: string) {
      if (browser && typeof localStorage?.setItem === 'function') {
        localStorage.setItem('turnkeyops_token', token);
      }
      set({ token, user: extractUser(token) });
    },
    loginFromResult(result: AuthResultLike) {
      const token = extractToken(result);
      if (!token) {
        throw new Error('Authentication completed but no access token was returned.');
      }

      const refreshToken = extractRefreshToken(result);
      if (refreshToken && browser && typeof localStorage?.setItem === 'function') {
        localStorage.setItem(
          'turnkeyops_refresh_token',
          refreshToken
        );
      }

      if (browser && typeof localStorage?.setItem === 'function') {
        localStorage.setItem('turnkeyops_token', token);
      }
      set({ token, user: extractUser(token) });
    },
    logout() {
      if (browser && typeof localStorage?.removeItem === 'function') {
        localStorage.removeItem('turnkeyops_token');
        localStorage.removeItem('turnkeyops_refresh_token');
      }
      set({ token: null, user: null });
      if (browser) {
        window.location.href = '/auth/login';
      }
    },
    isExpired(): boolean {
      const state = getState();
      if (!state.token) return true;
      const claims = parseJwt(state.token);
      const exp = claims.exp as number;
      return !exp || Date.now() / 1000 > exp;
    }
  };

  function getState() {
    let s = { token: null as string | null, user: null as AuthUser | null };
    subscribe(v => { s = v; })();
    return s;
  }
}

export const auth = createAuthStore();
export const isAuthenticated = derived(auth, $a => !!$a.token && !auth.isExpired());
export const currentUser = derived(auth, $a => $a.user);
export type { AuthUser };
