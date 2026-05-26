import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import type { Camera } from '@/api/types';
import { useDeleteCamera } from '@/hooks/useCameraMutations';

interface DeleteCameraDialogProps {
  camera: Camera | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DeleteCameraDialog({ camera, open, onOpenChange }: DeleteCameraDialogProps) {
  const del = useDeleteCamera();

  async function handleConfirm(event: React.MouseEvent<HTMLButtonElement>): Promise<void> {
    if (!camera) return;
    // Prevent Radix from auto-closing before mutation finishes, so we can keep
    // the dialog open on failure and the toast lands in context.
    event.preventDefault();
    try {
      await del.mutateAsync({ id: camera.id, name: camera.name });
      onOpenChange(false);
    } catch {
      // Toast surfaced inside the hook; dialog stays open for retry.
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete camera?</AlertDialogTitle>
          <AlertDialogDescription>
            {camera
              ? `"${camera.name}" will be removed. This cannot be undone.`
              : 'This cannot be undone.'}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter className="gap-2">
          <AlertDialogCancel asChild>
            <Button variant="outline" disabled={del.isPending}>
              Cancel
            </Button>
          </AlertDialogCancel>
          <AlertDialogAction asChild>
            <Button variant="destructive" disabled={del.isPending} onClick={handleConfirm}>
              {del.isPending ? 'Deleting...' : 'Delete'}
            </Button>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
