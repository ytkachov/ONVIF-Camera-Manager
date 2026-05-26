import createClient from 'openapi-fetch';

// Empty baseUrl => requests are relative; Vite dev server proxies /api to backend,
// in production the bundle is served from the same origin as the backend.
// Typed paths object will plug in once codegen runs (npm run codegen, M3).
export const apiClient = createClient<Record<string, never>>({ baseUrl: '' });

export async function apiGet<T>(path: string): Promise<T> {
  const res = await fetch(path, { headers: { Accept: 'application/json' } });
  if (!res.ok) {
    throw new Error(`GET ${path} failed: ${res.status} ${res.statusText}`);
  }
  return (await res.json()) as T;
}
