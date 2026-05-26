import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Pencil, Radar, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import type { Camera, CreateCameraRequest, DiscoveredDevice } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useCameras } from '@/hooks/useCameras';
import { CameraFormDialog } from '@/pages/cameras/CameraFormDialog';
import { DeleteCameraDialog } from '@/pages/cameras/DeleteCameraDialog';
import { DiscoveryDialog } from '@/pages/cameras/DiscoveryDialog';

type DialogState =
  | { kind: 'none' }
  | { kind: 'create'; defaults?: Partial<CreateCameraRequest> }
  | { kind: 'edit'; camera: Camera }
  | { kind: 'delete'; camera: Camera }
  | { kind: 'discover' };

export function CamerasPage() {
  const navigate = useNavigate();
  const { data, isLoading, isError, error } = useCameras();
  const [dialog, setDialog] = useState<DialogState>({ kind: 'none' });

  useEffect(() => {
    if (isError) {
      toast.error(error instanceof Error ? error.message : 'Failed to load cameras');
    }
  }, [isError, error]);

  function openCreate(): void {
    setDialog({ kind: 'create' });
  }

  function openDiscover(): void {
    setDialog({ kind: 'discover' });
  }

  function openEdit(camera: Camera): void {
    setDialog({ kind: 'edit', camera });
  }

  function openDelete(camera: Camera): void {
    setDialog({ kind: 'delete', camera });
  }

  function closeDialog(open: boolean): void {
    if (!open) setDialog({ kind: 'none' });
  }

  function handleDiscoverySelect(device: DiscoveredDevice): void {
    setDialog({
      kind: 'create',
      defaults: {
        ip: device.ip,
        port: device.port,
        name: device.name ?? device.ip,
      },
    });
  }

  const createDefaults = dialog.kind === 'create' ? dialog.defaults : undefined;

  return (
    <div className="p-6">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Cameras</h1>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={openDiscover}>
            <Radar className="mr-1 h-4 w-4" />
            Discover
          </Button>
          <Button onClick={openCreate}>Add manually</Button>
        </div>
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading...</p>}

      {!isLoading && data && data.length === 0 && (
        <Card>
          <CardHeader>
            <CardTitle>No cameras yet</CardTitle>
            <CardDescription>Click Discover or Add manually to get started.</CardDescription>
          </CardHeader>
          <CardContent className="flex gap-2">
            <Button variant="outline" onClick={openDiscover}>
              <Radar className="mr-1 h-4 w-4" />
              Discover
            </Button>
            <Button onClick={openCreate}>Add manually</Button>
          </CardContent>
        </Card>
      )}

      {!isLoading && data && data.length > 0 && (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/50 text-left">
                <tr>
                  <th className="px-4 py-2 font-medium">Name</th>
                  <th className="px-4 py-2 font-medium">IP</th>
                  <th className="px-4 py-2 font-medium">Manufacturer</th>
                  <th className="px-4 py-2 font-medium">Model</th>
                  <th className="px-4 py-2 font-medium text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.map((camera) => (
                  <tr
                    key={camera.id}
                    onClick={() => navigate(`/cameras/${camera.id}`)}
                    className="cursor-pointer border-b last:border-0 hover:bg-accent/50"
                  >
                    <td className="px-4 py-2 font-medium text-primary">{camera.name}</td>
                    <td className="px-4 py-2">
                      {camera.ip}
                      {camera.port ? `:${camera.port}` : ''}
                    </td>
                    <td className="px-4 py-2">{camera.manufacturer ?? '-'}</td>
                    <td className="px-4 py-2">{camera.model ?? '-'}</td>
                    <td className="px-4 py-2">
                      <div
                        className="flex items-center justify-end gap-1"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Edit ${camera.name}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            openEdit(camera);
                          }}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Delete ${camera.name}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            openDelete(camera);
                          }}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      <CameraFormDialog
        mode="create"
        defaults={createDefaults}
        open={dialog.kind === 'create'}
        onOpenChange={closeDialog}
      />
      <CameraFormDialog
        mode="edit"
        initial={dialog.kind === 'edit' ? dialog.camera : undefined}
        open={dialog.kind === 'edit'}
        onOpenChange={closeDialog}
      />
      <DeleteCameraDialog
        camera={dialog.kind === 'delete' ? dialog.camera : null}
        open={dialog.kind === 'delete'}
        onOpenChange={closeDialog}
      />
      <DiscoveryDialog
        open={dialog.kind === 'discover'}
        onOpenChange={closeDialog}
        onSelect={handleDiscoverySelect}
      />
    </div>
  );
}
