import type {
  Camera,
  CreateCameraRequest,
  ProblemDetails,
  UpdateCameraRequest,
  ValidationProblemDetails,
} from './types';

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

async function parseProblem(res: Response): Promise<ProblemDetails | ValidationProblemDetails> {
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

async function request<T>(input: RequestInfo | URL, init?: RequestInit): Promise<T> {
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

const jsonHeaders: HeadersInit = {
  'Content-Type': 'application/json',
  Accept: 'application/json',
};

export function getCamera(id: string): Promise<Camera> {
  return request<Camera>(`/api/cameras/${encodeURIComponent(id)}`, {
    headers: { Accept: 'application/json' },
  });
}

export function createCamera(req: CreateCameraRequest): Promise<Camera> {
  return request<Camera>('/api/cameras', {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify(req),
  });
}

export function updateCamera(id: string, req: UpdateCameraRequest): Promise<Camera> {
  // Drop empty password so the backend keeps the stored value.
  const payload: UpdateCameraRequest = {
    name: req.name,
    ip: req.ip,
    port: req.port,
    username: req.username,
    ...(req.password ? { password: req.password } : {}),
  };
  return request<Camera>(`/api/cameras/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: jsonHeaders,
    body: JSON.stringify(payload),
  });
}

export function deleteCamera(id: string): Promise<void> {
  return request<void>(`/api/cameras/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json' },
  });
}
