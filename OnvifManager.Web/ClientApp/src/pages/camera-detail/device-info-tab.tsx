import { useEffect } from 'react';
import { RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/api/client';
import type { DeviceInfo } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useDeviceInfo } from '@/hooks/useDeviceInfo';
import { DefinitionList } from './definition-list';

interface DeviceInfoTabProps {
  cameraId: string;
}

export function DeviceInfoTab({ cameraId }: DeviceInfoTabProps) {
  const query = useDeviceInfo(cameraId);
  const { data, isLoading, isFetching, isError, error, refetch } = query;

  useEffect(() => {
    if (!isError) return;
    if (error instanceof ApiError && error.status === 502) return;
    if (error instanceof Error) toast.error(error.message);
  }, [isError, error]);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <Button
          variant="outline"
          size="sm"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          <RefreshCw className={`mr-1 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {isLoading && (
        <p className="text-sm text-muted-foreground">Loading device info...</p>
      )}

      {isError && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 p-4 text-sm">
          <p className="font-medium text-destructive">Failed to load device info</p>
          <p className="mt-1 text-destructive/80">
            {error instanceof Error ? error.message : 'Unknown error'}
          </p>
          <Button
            variant="outline"
            size="sm"
            className="mt-3"
            onClick={() => void refetch()}
          >
            Retry
          </Button>
        </div>
      )}

      {data && !isError && <DeviceInfoCards info={data} />}
    </div>
  );
}

function DeviceInfoCards({ info }: { info: DeviceInfo }) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Identity</CardTitle>
        </CardHeader>
        <CardContent>
          <DefinitionList
            items={[
              { label: 'Manufacturer', value: info.manufacturer || '-' },
              { label: 'Model', value: info.model || '-' },
              { label: 'Firmware', value: info.firmwareVersion || '-' },
              { label: 'Serial', value: info.serialNumber || '-' },
              { label: 'Hardware ID', value: info.hardwareId || '-' },
              {
                label: 'Endpoint',
                value: info.endpoint ? <span className="font-mono text-xs">{info.endpoint}</span> : '-',
              },
            ]}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Time</CardTitle>
        </CardHeader>
        <CardContent>
          {info.time ? (
            <DefinitionList
              items={[
                { label: 'Time zone', value: info.time.timeZone || '-' },
                { label: 'Sync source', value: info.time.syncSource || '-' },
                { label: 'UTC', value: info.time.utc ?? '-' },
                { label: 'Local', value: info.time.local ?? '-' },
                { label: 'DST', value: info.time.daylightSavings ? 'yes' : 'no' },
              ]}
            />
          ) : (
            <p className="text-sm text-muted-foreground">
              Device did not return system time.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
