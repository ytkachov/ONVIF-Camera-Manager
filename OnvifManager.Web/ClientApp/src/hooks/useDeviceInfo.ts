import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getDeviceInfo } from '@/api/deviceInfo';
import { camerasQueryKey } from '@/hooks/useCameras';

export const deviceInfoQueryKey = (id: string) => ['cameras', id, 'device-info'] as const;

export function useDeviceInfo(id: string | undefined) {
  const qc = useQueryClient();
  return useQuery({
    queryKey: deviceInfoQueryKey(id ?? ''),
    queryFn: async () => {
      const info = await getDeviceInfo(id as string);
      // Backend persists fresher manufacturer/model into the camera record;
      // invalidate the list so navigating back shows the updated values.
      void qc.invalidateQueries({ queryKey: camerasQueryKey });
      return info;
    },
    enabled: Boolean(id),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}
