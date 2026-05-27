import { useQuery } from '@tanstack/react-query';
import { getProfiles } from '@/api/media';

export const profilesQueryKey = (id: string) => ['cameras', id, 'profiles'] as const;

export function useProfiles(id: string | undefined) {
  return useQuery({
    queryKey: profilesQueryKey(id ?? ''),
    queryFn: () => getProfiles(id as string),
    enabled: Boolean(id),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}
