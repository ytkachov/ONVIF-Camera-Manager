import { Link, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useCameras } from '@/hooks/useCameras';

export function CameraDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, isLoading } = useCameras();
  const camera = data?.find((c) => c.id === id);

  if (isLoading) {
    return <p className="p-6 text-sm text-muted-foreground">Loading...</p>;
  }

  if (!camera) {
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
        <h1 className="text-2xl font-semibold">{camera.name}</h1>
        <Link to="/cameras" className="text-sm text-primary hover:underline">
          Back to list
        </Link>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Raw camera data</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="mb-2 text-sm text-muted-foreground">
            Detailed views (info / video / PTZ / network / events) land in later milestones.
          </p>
          <pre className="overflow-auto rounded-md bg-muted p-3 text-xs">
            {JSON.stringify(camera, null, 2)}
          </pre>
        </CardContent>
      </Card>
    </div>
  );
}
