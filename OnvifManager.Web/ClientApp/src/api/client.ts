import createClient from 'openapi-fetch';
import type { ProblemDetails, ValidationProblemDetails } from './types';

// Empty baseUrl => requests are relative; Vite dev server proxies /api to backend,
// in production the bundle is served from the same origin as the backend.
// Typed paths object will plug in once codegen runs (npm run codegen, M3).
export const apiClient = createClient<Record<string, never>>({ baseUrl: '' });

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | ValidationProblemDetails;

  constructor(args: { status: number; problem: ProblemDetails | ValidationProblemDetails }) {
    super(args.problem.title ?? args.problem.detail ?? `Request failed (${args.status})`);
    this.name = 'ApiError';
    this.status = args.status;
    this.problem = args.problem;
  }

  isValidation(): this is ApiError & { problem: ValidationProblemDetails } {
    return (
      this.status === 400 &&
      typeof (this.problem as ValidationProblemDetails).errors === 'object' &&
      (this.problem as ValidationProblemDetails).errors !== undefined
    );
  }
}

export async function parseProblem(
  res: Response,
): Promise<ProblemDetails | ValidationProblemDetails> {
  try {
    const text = await res.text();
    if (!text) {
      return { status: res.status, title: res.statusText };
    }
    return JSON.parse(text) as ProblemDetails | ValidationProblemDetails;
  } catch {
    return { status: res.status, title: res.statusText };
  }
}

export async function request<T>(input: RequestInfo | URL, init?: RequestInit): Promise<T> {
  const res = await fetch(input, init);
  if (!res.ok) {
    const problem = await parseProblem(res);
    throw new ApiError({ status: res.status, problem });
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return (await res.json()) as T;
}

export async function apiGet<T>(path: string): Promise<T> {
  return request<T>(path, { headers: { Accept: 'application/json' } });
}
