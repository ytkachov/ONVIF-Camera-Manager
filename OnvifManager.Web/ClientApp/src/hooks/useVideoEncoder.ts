import { useQuery } from '@tanstack/react-query';
import { getVideoEncoder } from '@/api/media';

export const videoEncoderQueryKey = (id: string) => ['cameras', id, 'video-encoder'] as const;

export function useVideoEncoder(id: string | undefined) {
  return useQuery({
    queryKey: videoEncoderQueryKey(id ?? ''),
    queryFn: () => getVideoEncoder(id as string),
    enabled: Boolean(id),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}
