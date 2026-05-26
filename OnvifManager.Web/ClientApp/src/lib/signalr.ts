import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { cancelDiscovery, startDiscovery } from '@/api/discovery';
import type {
  DiscoveredDevice,
  DiscoveryCompleted,
  DiscoveryProgress,
} from '@/api/types';

export function createDiscoveryConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hubs/discovery')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export interface RunDiscoveryOptions {
  timeoutSeconds: number;
  onDevice: (device: DiscoveredDevice) => void;
  onProgress?: (progress: DiscoveryProgress) => void;
  onCompleted: (result: DiscoveryCompleted) => void;
  onError?: (err: unknown) => void;
}

export interface DiscoveryHandle {
  sessionId: string;
  cancel: () => Promise<void>;
}

export async function runDiscovery(opts: RunDiscoveryOptions): Promise<DiscoveryHandle> {
  const connection = createDiscoveryConnection();

  connection.on('DeviceFound', (device: DiscoveredDevice) => {
    opts.onDevice(device);
  });
  if (opts.onProgress) {
    connection.on('DiscoveryProgress', (progress: DiscoveryProgress) => {
      opts.onProgress?.(progress);
    });
  }

  let stopped = false;
  const stopConnection = async (): Promise<void> => {
    if (stopped) return;
    stopped = true;
    try {
      if (connection.state !== HubConnectionState.Disconnected) {
        await connection.stop();
      }
    } catch {
      // Ignore stop errors; the hub will tear down.
    }
  };

  connection.on('DiscoveryCompleted', (result: DiscoveryCompleted) => {
    try {
      opts.onCompleted(result);
    } finally {
      void stopConnection();
    }
  });

  connection.onclose((err) => {
    if (err && opts.onError) opts.onError(err);
  });

  try {
    await connection.start();
    const session = await startDiscovery(opts.timeoutSeconds);
    try {
      await connection.invoke('JoinSession', session.sessionId);
    } catch (err) {
      // JoinSession failed -> try to cancel server-side session and tear down.
      await cancelDiscovery(session.sessionId).catch(() => undefined);
      await stopConnection();
      throw err;
    }

    return {
      sessionId: session.sessionId,
      cancel: async () => {
        try {
          await cancelDiscovery(session.sessionId);
        } finally {
          try {
            if (connection.state === HubConnectionState.Connected) {
              await connection.invoke('LeaveSession', session.sessionId);
            }
          } catch {
            // Best-effort; LeaveSession is not critical.
          }
          await stopConnection();
        }
      },
    };
  } catch (err) {
    await stopConnection();
    throw err;
  }
}
