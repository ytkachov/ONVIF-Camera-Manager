import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  ApiError,
  createCamera,
  deleteCamera,
  updateCamera,
} from '@/api/cameras';
import type { Camera, CreateCameraRequest, UpdateCameraRequest } from '@/api/types';
import { camerasQueryKey } from '@/hooks/useCameras';

function reportError(err: unknown, fallback: string): void {
  if (err instanceof ApiError) {
    if (err.isValidation()) {
      // Field-level errors are surfaced via RHF setError by the caller;
      // skip the toast to avoid duplicating noise.
      return;
    }
    const msg = err.problem.detail ?? err.problem.title ?? fallback;
    toast.error(msg);
    return;
  }
  if (err instanceof Error) {
    toast.error(err.message);
    return;
  }
  toast.error(fallback);
}

export function useCreateCamera() {
  const qc = useQueryClient();
  return useMutation<Camera, unknown, CreateCameraRequest>({
    mutationFn: (req) => createCamera(req),
    onSuccess: (camera) => {
      void qc.invalidateQueries({ queryKey: camerasQueryKey });
      toast.success(`Camera "${camera.name}" added`);
    },
    onError: (err) => reportError(err, 'Failed to create camera'),
  });
}

export function useUpdateCamera() {
  const qc = useQueryClient();
  return useMutation<Camera, unknown, { id: string; data: UpdateCameraRequest }>({
    mutationFn: ({ id, data }) => updateCamera(id, data),
    onSuccess: (camera) => {
      void qc.invalidateQueries({ queryKey: camerasQueryKey });
      void qc.invalidateQueries({ queryKey: ['camera', camera.id] });
      toast.success(`Camera "${camera.name}" updated`);
    },
    onError: (err) => reportError(err, 'Failed to update camera'),
  });
}

export function useDeleteCamera() {
  const qc = useQueryClient();
  return useMutation<void, unknown, { id: string; name: string }>({
    mutationFn: ({ id }) => deleteCamera(id),
    onSuccess: (_void, vars) => {
      void qc.invalidateQueries({ queryKey: camerasQueryKey });
      toast.success(`Camera "${vars.name}" deleted`);
    },
    onError: (err) => reportError(err, 'Failed to delete camera'),
  });
}
