import { useCallback, useEffect, useRef, useState } from 'react';
import { Radar } from 'lucide-react';
import { toast } from 'sonner';
import type { DiscoveredDevice, DiscoveryProgress } from '@/api/types';
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
import { runDiscovery, type DiscoveryHandle } from '@/lib/signalr';

type DiscoveryState = 'idle' | 'running' | 'completed' | 'error';

interface DiscoveryDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSelect: (device: DiscoveredDevice) => void;
}

const MIN_TIMEOUT = 1;
const MAX_TIMEOUT = 60;
const DEFAULT_TIMEOUT = 10;

export function DiscoveryDialog({ open, onOpenChange, onSelect }: DiscoveryDialogProps) {
  const [timeoutSeconds, setTimeoutSeconds] = useState<number>(DEFAULT_TIMEOUT);
  const [state, setState] = useState<DiscoveryState>('idle');
  const [devices, setDevices] = useState<DiscoveredDevice[]>([]);
  const [progress, setProgress] = useState<DiscoveryProgress | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleRef = useRef<DiscoveryHandle | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const safeSet = useCallback(<T,>(setter: (v: T) => void, value: T): void => {
    if (mountedRef.current) setter(value);
  }, []);

  const teardown = useCallback(async (): Promise<void> => {
    const handle = handleRef.current;
    handleRef.current = null;
    if (handle) {
      try {
        await handle.cancel();
      } catch {
        // Cleanup best-effort.
      }
    }
  }, []);

  useEffect(() => {
    if (!open) {
      void teardown();
      setState('idle');
      setDevices([]);
      setProgress(null);
      setErrorMsg(null);
      setTimeoutSeconds(DEFAULT_TIMEOUT);
    }
  }, [open, teardown]);

  useEffect(() => {
    return () => {
      void teardown();
    };
  }, [teardown]);

  async function handleStart(): Promise<void> {
    if (state === 'running') return;
    const clamped = Math.max(MIN_TIMEOUT, Math.min(MAX_TIMEOUT, timeoutSeconds || DEFAULT_TIMEOUT));
    setDevices([]);
    setProgress({ found: 0, elapsedSeconds: 0 });
    setErrorMsg(null);
    setState('running');
    try {
      const handle = await runDiscovery({
        timeoutSeconds: clamped,
        onDevice: (device) => {
          if (!mountedRef.current) return;
          setDevices((prev) =>
            prev.some((d) => d.id === device.id) ? prev : [...prev, device],
          );
        },
        onProgress: (p) => safeSet(setProgress, p),
        onCompleted: (result) => {
          if (!mountedRef.current) return;
          setProgress({ found: result.found, elapsedSeconds: result.durationSeconds });
          setState('completed');
          handleRef.current = null;
        },
        onError: (err) => {
          if (!mountedRef.current) return;
          setErrorMsg(err instanceof Error ? err.message : 'SignalR connection error');
          setState('error');
        },
      });
      handleRef.current = handle;
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to start discovery';
      if (mountedRef.current) {
        setErrorMsg(msg);
        setState('error');
      }
      toast.error(msg);
    }
  }

  async function handleCancel(): Promise<void> {
    await teardown();
    if (mountedRef.current) {
      setState('completed');
    }
  }

  function handleClose(): void {
    onOpenChange(false);
  }

  function handlePick(device: DiscoveredDevice): void {
    onSelect(device);
  }

  const isRunning = state === 'running';
  const elapsed = progress?.elapsedSeconds ?? 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Discover cameras</DialogTitle>
          <DialogDescription>
            Broadcasts WS-Discovery on the local network and lists ONVIF devices that respond.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="disc-timeout">Timeout (sec)</Label>
            <Input
              id="disc-timeout"
              type="number"
              min={MIN_TIMEOUT}
              max={MAX_TIMEOUT}
              value={timeoutSeconds}
              onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
              disabled={isRunning}
              className="w-28"
            />
          </div>
          {isRunning ? (
            <Button variant="outline" onClick={handleCancel}>
              Cancel
            </Button>
          ) : (
            <Button onClick={handleStart}>
              <Radar className="mr-1 h-4 w-4" />
              {state === 'completed' || state === 'error' ? 'Rescan' : 'Start'}
            </Button>
          )}
          {(isRunning || state === 'completed') && progress && (
            <div className="ml-auto text-xs text-muted-foreground">
              {elapsed.toFixed(0)}s / {timeoutSeconds}s &middot; found {progress.found}
            </div>
          )}
        </div>

        {errorMsg && (
          <p className="text-sm text-destructive" role="alert">
            {errorMsg}
          </p>
        )}

        <div className="max-h-[360px] overflow-y-auto rounded-md border">
          {devices.length === 0 ? (
            <div className="p-6 text-center text-sm text-muted-foreground">
              {state === 'running' && 'Scanning...'}
              {state === 'completed' && 'No devices found.'}
              {state === 'idle' && 'No devices yet. Click Start to scan.'}
              {state === 'error' && 'Scan failed.'}
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/50 text-left">
                <tr>
                  <th className="px-3 py-2 font-medium">Name</th>
                  <th className="px-3 py-2 font-medium">Address</th>
                  <th className="px-3 py-2 font-medium">Hardware</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {devices.map((d) => (
                  <tr key={d.id} className="border-b last:border-0 hover:bg-accent/40">
                    <td className="px-3 py-2 font-medium">{d.name ?? d.ip}</td>
                    <td className="px-3 py-2 font-mono text-xs">
                      {d.ip}:{d.port}
                    </td>
                    <td className="px-3 py-2 text-muted-foreground">{d.hardware ?? '-'}</td>
                    <td className="px-3 py-2 text-right">
                      <Button size="sm" onClick={() => handlePick(d)}>
                        Add
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <DialogFooter>
          {isRunning ? (
            <Button variant="outline" onClick={handleCancel}>
              Cancel
            </Button>
          ) : (
            <Button variant="outline" onClick={handleClose}>
              Close
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
