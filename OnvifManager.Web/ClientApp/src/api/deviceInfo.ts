import { apiGet } from './client';
import type { DeviceInfo } from './types';

export function getDeviceInfo(id: string): Promise<DeviceInfo> {
  return apiGet<DeviceInfo>(`/api/cameras/${encodeURIComponent(id)}/device-info`);
}
