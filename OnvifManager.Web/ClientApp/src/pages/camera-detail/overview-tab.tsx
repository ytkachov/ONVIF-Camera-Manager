import type { Camera } from '@/api/types';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { DefinitionList } from './definition-list';

interface OverviewTabProps {
  camera: Camera;
}

export function OverviewTab({ camera }: OverviewTabProps) {
  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Connection</CardTitle>
        </CardHeader>
        <CardContent>
          <DefinitionList
            items={[
              { label: 'Name', value: camera.name },
              { label: 'IP', value: camera.ip },
              { label: 'Port', value: camera.port },
              { label: 'Username', value: camera.username },
              { label: 'Manufacturer', value: camera.manufacturer ?? '-' },
              { label: 'Model', value: camera.model ?? '-' },
              { label: 'Firmware', value: camera.firmwareVersion ?? '-' },
            ]}
          />
        </CardContent>
      </Card>

      <details className="rounded-md border bg-muted/40 p-3 text-sm">
        <summary className="cursor-pointer select-none font-medium text-muted-foreground">
          Raw record (debug)
        </summary>
        <pre className="mt-2 overflow-auto rounded-md bg-muted p-3 text-xs">
          {JSON.stringify(camera, null, 2)}
        </pre>
      </details>
    </div>
  );
}
