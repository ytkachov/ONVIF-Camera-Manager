import { ApiError } from './cameras';
import type {
  DiscoveryStartResponse,
  ProblemDetails,
  ValidationProblemDetails,
} from './types';

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

export async function startDiscovery(
  timeoutSeconds: number,
  sessionId?: string,
): Promise<DiscoveryStartResponse> {
  const body: Record<string, unknown> = { timeoutSeconds };
  if (sessionId) body.sessionId = sessionId;
  const res = await fetch('/api/discovery/start', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const problem = await parseProblem(res);
    throw new ApiError({ status: res.status, problem });
  }
  return (await res.json()) as DiscoveryStartResponse;
}

export async function cancelDiscovery(sessionId: string): Promise<void> {
  const res = await fetch(`/api/discovery/${encodeURIComponent(sessionId)}/cancel`, {
    method: 'POST',
    headers: { Accept: 'application/json' },
  });
  if (res.status === 404) {
    // Session already gone -> treat as success.
    return;
  }
  if (!res.ok) {
    const problem = await parseProblem(res);
    throw new ApiError({ status: res.status, problem });
  }
}
