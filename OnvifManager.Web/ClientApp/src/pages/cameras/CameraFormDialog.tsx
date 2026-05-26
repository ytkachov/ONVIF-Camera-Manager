import { useEffect } from 'react';
import { useForm, type Path, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ApiError } from '@/api/cameras';
import type { Camera, CreateCameraRequest, UpdateCameraRequest } from '@/api/types';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useCreateCamera, useUpdateCamera } from '@/hooks/useCameraMutations';
import {
  createCameraSchema,
  updateCameraSchema,
  type CreateCameraForm,
  type UpdateCameraForm,
} from '@/lib/schemas';

type FormValues = CreateCameraForm | UpdateCameraForm;

interface CameraFormDialogProps {
  mode: 'create' | 'edit';
  initial?: Camera;
  defaults?: Partial<CreateCameraRequest>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const FIELD_NAMES: ReadonlyArray<keyof CreateCameraForm> = [
  'name',
  'ip',
  'port',
  'username',
  'password',
];

function defaultValues(
  mode: 'create' | 'edit',
  initial?: Camera,
  defaults?: Partial<CreateCameraRequest>,
): FormValues {
  if (mode === 'edit' && initial) {
    return {
      name: initial.name,
      ip: initial.ip,
      port: initial.port,
      username: initial.username,
      password: '',
    };
  }
  return {
    name: defaults?.name ?? '',
    ip: defaults?.ip ?? '',
    port: defaults?.port ?? 80,
    username: defaults?.username ?? '',
    password: defaults?.password ?? '',
  };
}

export function CameraFormDialog({
  mode,
  initial,
  defaults,
  open,
  onOpenChange,
}: CameraFormDialogProps) {
  const create = useCreateCamera();
  const update = useUpdateCamera();
  const schema = mode === 'create' ? createCameraSchema : updateCameraSchema;

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as Resolver<FormValues>,
    defaultValues: defaultValues(mode, initial, defaults),
    mode: 'onSubmit',
  });

  useEffect(() => {
    if (open) {
      form.reset(defaultValues(mode, initial, defaults));
    }
  }, [open, mode, initial, defaults, form]);

  const isSubmitting = create.isPending || update.isPending;

  function applyServerErrors(err: unknown): void {
    if (!(err instanceof ApiError) || !err.isValidation()) return;
    const errors = err.problem.errors ?? {};
    for (const [rawKey, msgs] of Object.entries(errors)) {
      const normalized = rawKey.charAt(0).toLowerCase() + rawKey.slice(1);
      const field = FIELD_NAMES.find((f) => f === normalized || f === rawKey);
      if (!field) continue;
      form.setError(field as Path<FormValues>, { type: 'server', message: msgs.join(', ') });
    }
  }

  async function onSubmit(values: FormValues): Promise<void> {
    if (mode === 'create') {
      const req: CreateCameraRequest = {
        name: values.name,
        ip: values.ip,
        port: values.port,
        username: values.username,
        password: (values as CreateCameraForm).password,
      };
      try {
        await create.mutateAsync(req);
        onOpenChange(false);
      } catch (err) {
        applyServerErrors(err);
      }
      return;
    }

    if (!initial) return;
    const pwd = (values as UpdateCameraForm).password ?? '';
    const req: UpdateCameraRequest = {
      name: values.name,
      ip: values.ip,
      port: values.port,
      username: values.username,
      ...(pwd ? { password: pwd } : {}),
    };
    try {
      await update.mutateAsync({ id: initial.id, data: req });
      onOpenChange(false);
    } catch (err) {
      applyServerErrors(err);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'Add camera' : 'Edit camera'}</DialogTitle>
          <DialogDescription>
            {mode === 'create'
              ? 'Connect a camera by entering its address and credentials.'
              : 'Update camera connection details.'}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <Field
            id="cam-name"
            label="Name"
            error={form.formState.errors.name?.message}
            input={
              <Input
                id="cam-name"
                autoFocus
                autoComplete="off"
                {...form.register('name')}
              />
            }
          />

          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <Field
                id="cam-ip"
                label="IP address"
                error={form.formState.errors.ip?.message}
                input={
                  <Input
                    id="cam-ip"
                    autoComplete="off"
                    placeholder="192.168.1.10"
                    {...form.register('ip')}
                  />
                }
              />
            </div>
            <Field
              id="cam-port"
              label="Port"
              error={form.formState.errors.port?.message}
              input={
                <Input
                  id="cam-port"
                  type="number"
                  min={1}
                  max={65535}
                  {...form.register('port')}
                />
              }
            />
          </div>

          <Field
            id="cam-user"
            label="Username"
            error={form.formState.errors.username?.message}
            input={
              <Input
                id="cam-user"
                autoComplete="off"
                {...form.register('username')}
              />
            }
          />

          <Field
            id="cam-pass"
            label="Password"
            error={form.formState.errors.password?.message}
            input={
              <Input
                id="cam-pass"
                type="password"
                autoComplete="new-password"
                placeholder={mode === 'edit' ? 'Leave blank to keep current' : undefined}
                {...form.register('password')}
              />
            }
          />

          <DialogFooter className="gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

interface FieldProps {
  id: string;
  label: string;
  input: React.ReactNode;
  error?: string;
}

function Field({ id, label, input, error }: FieldProps) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      {input}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
