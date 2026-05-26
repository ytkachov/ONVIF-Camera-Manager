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

// Random 32-hex-char id matching the server regex ^[A-Za-z0-9_-]{8,64}$.
// crypto.randomUUID() requires a secure context (HTTPS/localhost), which
// http://<lan-ip>:8080 is not, so we build the id from raw random bytes.
function generateSessionId(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

export async function runDiscovery(opts: RunDiscoveryOptions): Promise<DiscoveryHandle> {
  const connection = createDiscoveryConnection();
  const sessionId = generateSessionId();

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
    // Connect and join the per-session group BEFORE kicking off discovery on
    // the server: otherwise ProbeMatch responses from cameras in the LAN can
    // arrive in the milliseconds between POST and JoinSession and broadcast
    // into an empty group — UI then shows "found N" via the ticker but the
    // device list stays empty.
    await connection.start();
    await connection.invoke('JoinSession', sessionId);

    try {
      await startDiscovery(opts.timeoutSeconds, sessionId);
    } catch (err) {
      // Start failed -> leave the (now-orphan) group and tear down.
      try {
        await connection.invoke('LeaveSession', sessionId);
      } catch {
        // Best-effort; LeaveSession is not critical.
      }
      await stopConnection();
      throw err;
    }

    return {
      sessionId,
      cancel: async () => {
        try {
          await cancelDiscovery(sessionId);
        } finally {
          try {
            if (connection.state === HubConnectionState.Connected) {
              await connection.invoke('LeaveSession', sessionId);
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
