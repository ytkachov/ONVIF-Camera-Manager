// Hand-written mirrors of backend DTOs.
// Replaced with openapi-typescript generated schema in M3 once backend stabilises.

export interface Camera {
  id: string;
  name: string;
  ip: string;
  port: number;
  username: string;
  manufacturer?: string;
  model?: string;
  firmwareVersion?: string;
}

export interface Health {
  status: string;
  version: string;
  runtime: string;
  uptime: string;
}

export interface CreateCameraRequest {
  name: string;
  ip: string;
  port: number;
  username: string;
  password: string;
}

export interface UpdateCameraRequest {
  name: string;
  ip: string;
  port: number;
  username: string;
  password?: string;
}

// RFC 7807 ProblemDetails as emitted by ASP.NET Core.
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
}

export interface ValidationProblemDetails extends ProblemDetails {
  errors?: Record<string, string[]>;
}

export interface DiscoveredDevice {
  id: string;
  endpoint: string;
  ip: string;
  port: number;
  scopes: string[];
  hardware?: string;
  name?: string;
  types: string[];
}

export interface DiscoveryProgress {
  found: number;
  elapsedSeconds: number;
}

export interface DiscoveryCompleted {
  found: number;
  durationSeconds: number;
  cancelled: boolean;
}

export interface DiscoveryStartResponse {
  sessionId: string;
  timeoutSeconds: number;
}
