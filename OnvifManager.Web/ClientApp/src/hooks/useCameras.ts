import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { Camera } from '@/api/types';

export const camerasQueryKey = ['cameras'] as const;

export function useCameras() {
  return useQuery({
    queryKey: camerasQueryKey,
    queryFn: () => apiGet<Camera[]>('/api/cameras'),
  });
}
