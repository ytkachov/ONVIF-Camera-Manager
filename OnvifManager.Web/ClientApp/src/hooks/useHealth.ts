import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { Health } from '@/api/types';

export const healthQueryKey = ['health'] as const;

export function useHealth() {
  return useQuery({
    queryKey: healthQueryKey,
    queryFn: () => apiGet<Health>('/api/health'),
  });
}
