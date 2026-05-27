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

export interface SystemDateTime {
  timeZone: string;
  syncSource: string;
  utc: string | null;
  local: string | null;
  daylightSavings: boolean;
}

export interface DeviceInfo {
  manufacturer: string;
  model: string;
  firmwareVersion: string;
  serialNumber: string;
  hardwareId: string;
  endpoint: string;
  time: SystemDateTime | null;
}

export interface CameraProfile {
  token: string;
  name: string;
  fixed: boolean;
  videoSourceToken: string;
  videoEncoderToken: string;
}

export type VideoQuality = 'ConstantBitrate' | 'VariableBitrate' | 'ConstantQuality';

export interface VideoEncoderConfig {
  token: string;
  name: string;
  encoding: string;
  useCount: number;
  width: number;
  height: number;
  frameRateLimit: number;
  bitrateLimit: number;
  encodingInterval: number;
  govLength: string;
  h264Profile: string;
  quality: VideoQuality;
}

export type MediaApiVersion = 'media1' | 'media2';

export interface VideoEncoderConfigsResponse {
  mediaVersion: MediaApiVersion;
  configurations: VideoEncoderConfig[];
}
