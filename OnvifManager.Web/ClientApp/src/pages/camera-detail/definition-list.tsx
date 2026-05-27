import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export interface DefinitionItem {
  label: string;
  value: ReactNode;
}

interface DefinitionListProps {
  items: DefinitionItem[];
  className?: string;
}

export function DefinitionList({ items, className }: DefinitionListProps) {
  return (
    <dl
      className={cn(
        'grid grid-cols-[max-content_1fr] gap-x-6 gap-y-2 text-sm',
        className,
      )}
    >
      {items.map((item) => (
        <div key={item.label} className="contents">
          <dt className="font-medium text-muted-foreground">{item.label}</dt>
          <dd className="break-all">{item.value ?? '-'}</dd>
        </div>
      ))}
    </dl>
  );
}
