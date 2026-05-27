import { useCallback } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useCameras } from '@/hooks/useCameras';
import { DeviceInfoTab } from './camera-detail/device-info-tab';
import { OverviewTab } from './camera-detail/overview-tab';
import { VideoTab } from './camera-detail/video-tab';

type TabKey = 'overview' | 'device' | 'video' | 'ptz' | 'network' | 'events';

const TAB_KEYS: ReadonlySet<TabKey> = new Set<TabKey>([
  'overview',
  'device',
  'video',
  'ptz',
  'network',
  'events',
]);

function isTabKey(value: string | null): value is TabKey {
  return value != null && (TAB_KEYS as Set<string>).has(value);
}

export function CameraDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const { data, isLoading } = useCameras();
  const camera = data?.find((c) => c.id === id);

  const tabParam = searchParams.get('tab');
  const activeTab: TabKey = isTabKey(tabParam) ? tabParam : 'overview';

  const handleTabChange = useCallback(
    (value: string): void => {
      const next = isTabKey(value) ? value : 'overview';
      const nextParams = new URLSearchParams(searchParams);
      if (next === 'overview') {
        nextParams.delete('tab');
      } else {
        nextParams.set('tab', next);
      }
      setSearchParams(nextParams, { replace: true });
    },
    [searchParams, setSearchParams],
  );

  if (isLoading) {
    return <p className="p-6 text-sm text-muted-foreground">Loading...</p>;
  }

  if (!camera || !id) {
    return (
      <div className="p-6">
        <Card>
          <CardHeader>
            <CardTitle>Camera not found</CardTitle>
          </CardHeader>
          <CardContent>
            <Link to="/cameras">
              <Button>Back to list</Button>
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-4 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{camera.name}</h1>
          <p className="text-sm text-muted-foreground">
            {camera.ip}
            {camera.port ? `:${camera.port}` : ''}
            {camera.manufacturer ? ` - ${camera.manufacturer}` : ''}
            {camera.model ? ` ${camera.model}` : ''}
          </p>
        </div>
        <Link to="/cameras" className="text-sm text-primary hover:underline">
          Back to list
        </Link>
      </div>

      <Tabs value={activeTab} onValueChange={handleTabChange}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="device">Device Info</TabsTrigger>
          <TabsTrigger value="video">Video</TabsTrigger>
          <TabsTrigger value="ptz" disabled title="Coming in M5">
            PTZ
          </TabsTrigger>
          <TabsTrigger value="network" disabled title="Coming in M5">
            Network
          </TabsTrigger>
          <TabsTrigger value="events" disabled title="Coming in M6">
            Events
          </TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <OverviewTab camera={camera} />
        </TabsContent>
        <TabsContent value="device">
          <DeviceInfoTab cameraId={id} />
        </TabsContent>
        <TabsContent value="video">
          <VideoTab cameraId={id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
