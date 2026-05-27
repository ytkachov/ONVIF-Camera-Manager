import { apiGet } from './client';
import type { CameraProfile, VideoEncoderConfigsResponse } from './types';

export function getProfiles(id: string): Promise<CameraProfile[]> {
  return apiGet<CameraProfile[]>(`/api/cameras/${encodeURIComponent(id)}/profiles`);
}

export function getVideoEncoder(id: string): Promise<VideoEncoderConfigsResponse> {
  return apiGet<VideoEncoderConfigsResponse>(
    `/api/cameras/${encodeURIComponent(id)}/video-encoder`,
  );
}
