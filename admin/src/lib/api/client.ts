/**
 * TurnKeyOps API Client — typed fetch wrapper with JWT auth.
 * "Don't Make Me Think" — the client handles tokens, errors, and retries.
 */

const API_BASE = import.meta.env.VITE_API_URL ?? '';

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  errors?: ApiError[];
  traceId?: string;
}

interface ApiPagedResponse<T> extends ApiResponse<T[]> {
  pageSize: number;
  continuationToken?: string | null;
}

interface ApiError {
  code: string;
  field?: string;
  message: string;
}

function isWrappedResponse<T>(value: unknown): value is ApiResponse<T> {
  return typeof value === 'object' && value !== null && 'success' in value;
}

class ApiClient {
  private getToken(): string | null {
    return localStorage.getItem('turnkeyops_token');
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    query?: Record<string, string | number | undefined>
  ): Promise<T> {
    const url = new URL(`${API_BASE}/api${path}`, window.location.origin);
    if (query) {
      Object.entries(query).forEach(([k, v]) => {
        if (v !== undefined && v !== null) url.searchParams.set(k, String(v));
      });
    }

    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    const token = this.getToken();
    if (token && !path.startsWith('/auth')) headers['Authorization'] = `Bearer ${token}`;

    const res = await fetch(url.toString(), {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined
    });

    // Handle 204 No Content
    if (res.status === 204) return undefined as T;

    // Handle 401 — redirect to login
    if (res.status === 401) {
      localStorage.removeItem('turnkeyops_token');
      localStorage.removeItem('turnkeyops_refresh_token');
      window.location.href = '/auth/login';
      throw new Error('Unauthorized');
    }

    const json = await res.json();

    // Some upstream/auth endpoints return raw JSON objects instead of the
    // app's usual { success, data, errors } envelope.
    if (!isWrappedResponse<T>(json)) {
      return json as T;
    }

    if (!json.success) {
      const msg = json.errors?.map(e => e.message).join(', ') ?? 'Request failed';
      throw new Error(msg);
    }

    return json.data as T;
  }

  get<T>(path: string, query?: Record<string, string | number | undefined>) {
    return this.request<T>('GET', path, undefined, query);
  }

  post<T>(path: string, body?: unknown) {
    return this.request<T>('POST', path, body);
  }

  put<T>(path: string, body: unknown) {
    return this.request<T>('PUT', path, body);
  }

  del(path: string) {
    return this.request<void>('DELETE', path);
  }

  /** Paged query returns items + continuation token */
  async paged<T>(path: string, pageSize = 20, continuationToken?: string) {
    const url = new URL(`${API_BASE}/api${path}`, window.location.origin);
    url.searchParams.set('pageSize', String(pageSize));
    if (continuationToken) url.searchParams.set('continuationToken', continuationToken);

    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    const token = this.getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const res = await fetch(url.toString(), { headers });
    const json: ApiPagedResponse<T> = await res.json();
    if (!json.success) throw new Error(json.errors?.[0]?.message ?? 'Failed');
    return { items: json.data ?? [], continuationToken: json.continuationToken ?? null };
  }
}

export const api = new ApiClient();
export type { ApiResponse, ApiPagedResponse, ApiError };
