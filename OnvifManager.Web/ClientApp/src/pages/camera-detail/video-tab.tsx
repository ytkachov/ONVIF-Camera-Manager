import { useEffect } from 'react';
import { RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/api/client';
import type {
  CameraProfile,
  VideoEncoderConfig,
  VideoEncoderConfigsResponse,
} from '@/api/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { useProfiles } from '@/hooks/useProfiles';
import { useVideoEncoder } from '@/hooks/useVideoEncoder';

interface VideoTabProps {
  cameraId: string;
}

export function VideoTab({ cameraId }: VideoTabProps) {
  const profilesQ = useProfiles(cameraId);
  const encoderQ = useVideoEncoder(cameraId);

  const refetchAll = (): void => {
    void profilesQ.refetch();
    void encoderQ.refetch();
  };
  const isFetching = profilesQ.isFetching || encoderQ.isFetching;

  useEffect(() => {
    [profilesQ.error, encoderQ.error].forEach((e) => {
      if (e instanceof ApiError && e.status === 502) return;
      if (e instanceof Error) toast.error(e.message);
    });
  }, [profilesQ.error, encoderQ.error]);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <Button variant="outline" size="sm" onClick={refetchAll} disabled={isFetching}>
          <RefreshCw className={`mr-1 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ProfilesCard
          isLoading={profilesQ.isLoading}
          isError={profilesQ.isError}
          error={profilesQ.error}
          data={profilesQ.data}
          onRetry={() => void profilesQ.refetch()}
        />
        <EncoderCard
          isLoading={encoderQ.isLoading}
          isError={encoderQ.isError}
          error={encoderQ.error}
          data={encoderQ.data}
          onRetry={() => void encoderQ.refetch()}
        />
      </div>
    </div>
  );
}

interface CardStateProps<T> {
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  data: T | undefined;
  onRetry: () => void;
}

function ProfilesCard({
  isLoading,
  isError,
  error,
  data,
  onRetry,
}: CardStateProps<CameraProfile[]>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Profiles</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading && <p className="text-sm text-muted-foreground">Loading profiles...</p>}
        {isError && <ErrorBanner error={error} onRetry={onRetry} />}
        {data && data.length === 0 && (
          <p className="text-sm text-muted-foreground">No media profiles reported.</p>
        )}
        {data && data.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/50 text-left">
                <tr>
                  <th className="px-3 py-2 font-medium">Name</th>
                  <th className="px-3 py-2 font-medium">Token</th>
                  <th className="px-3 py-2 font-medium">Video encoder token</th>
                  <th className="px-3 py-2 font-medium">Fixed</th>
                </tr>
              </thead>
              <tbody>
                {data.map((p) => (
                  <tr key={p.token} className="border-b last:border-0">
                    <td className="px-3 py-2 font-medium">{p.name}</td>
                    <td className="px-3 py-2 font-mono text-xs">{p.token}</td>
                    <td className="px-3 py-2 font-mono text-xs">{p.videoEncoderToken || '-'}</td>
                    <td className="px-3 py-2">{p.fixed ? 'yes' : 'no'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function EncoderCard({
  isLoading,
  isError,
  error,
  data,
  onRetry,
}: CardStateProps<VideoEncoderConfigsResponse>) {
  const sourceLabel =
    data?.mediaVersion === 'media2'
      ? 'Source: ONVIF Media2'
      : 'Source: ONVIF Media1 (H265 may be misreported as H264)';

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Video encoders</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading && <p className="text-sm text-muted-foreground">Loading encoders...</p>}
        {isError && <ErrorBanner error={error} onRetry={onRetry} />}
        {data && (
          <>
            <p className="mb-2 text-xs text-muted-foreground">{sourceLabel}</p>
            {data.configurations.length === 0 ? (
              <p className="text-sm text-muted-foreground">No encoder configurations.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="border-b bg-muted/50 text-left">
                    <tr>
                      <th className="px-3 py-2 font-medium">Name</th>
                      <th className="px-3 py-2 font-medium">Encoding</th>
                      <th className="px-3 py-2 font-medium">Resolution</th>
                      <th className="px-3 py-2 font-medium">FPS</th>
                      <th className="px-3 py-2 font-medium">Bitrate (kbps)</th>
                      <th className="px-3 py-2 font-medium">GOP</th>
                      <th className="px-3 py-2 font-medium">Profile</th>
                      <th className="px-3 py-2 font-medium">Quality</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.configurations.map((c) => (
                      <EncoderRow key={c.token} cfg={c} />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function EncoderRow({ cfg }: { cfg: VideoEncoderConfig }) {
  return (
    <tr className="border-b last:border-0">
      <td className="px-3 py-2 font-medium">{cfg.name}</td>
      <td className="px-3 py-2">
        <EncodingBadge encoding={cfg.encoding} />
      </td>
      <td className="px-3 py-2">{cfg.width}x{cfg.height}</td>
      <td className="px-3 py-2">{cfg.frameRateLimit}</td>
      <td className="px-3 py-2">{cfg.bitrateLimit}</td>
      <td className="px-3 py-2">{cfg.govLength || '-'}</td>
      <td className="px-3 py-2">{cfg.h264Profile || '-'}</td>
      <td className="px-3 py-2">{cfg.quality}</td>
    </tr>
  );
}

function EncodingBadge({ encoding }: { encoding: string }) {
  const upper = encoding.toUpperCase();
  const tone =
    upper === 'H265'
      ? 'bg-purple-100 text-purple-800'
      : upper === 'H264'
      ? 'bg-blue-100 text-blue-800'
      : 'bg-muted text-muted-foreground';
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium',
        tone,
      )}
    >
      {upper || '?'}
    </span>
  );
}

function ErrorBanner({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return (
    <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm">
      <p className="font-medium text-destructive">Failed to load</p>
      <p className="mt-1 text-destructive/80">
        {error instanceof Error ? error.message : 'Unknown error'}
      </p>
      <Button variant="outline" size="sm" className="mt-2" onClick={onRetry}>
        Retry
      </Button>
    </div>
  );
}
