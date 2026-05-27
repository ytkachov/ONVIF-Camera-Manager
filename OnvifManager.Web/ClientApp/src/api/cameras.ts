import { request } from './client';
import type {
  Camera,
  CreateCameraRequest,
  UpdateCameraRequest,
} from './types';

export { ApiError } from './client';

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
