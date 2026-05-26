import { NavLink, Outlet } from 'react-router-dom';
import { Camera as CameraIcon, Search, Activity } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useHealth } from '@/hooks/useHealth';

interface NavItem {
  to: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  disabled?: boolean;
}

const navItems: NavItem[] = [
  { to: '/cameras', label: 'Cameras', icon: CameraIcon },
  { to: '/discover', label: 'Discover', icon: Search, disabled: true },
  { to: '/events', label: 'Events', icon: Activity, disabled: true },
];

export function AppShell() {
  const health = useHealth();

  return (
    <div className="flex h-screen w-screen bg-background text-foreground">
      <aside className="flex w-60 shrink-0 flex-col border-r bg-muted/40">
        <div className="flex h-14 items-center border-b px-4 font-semibold">ONVIF Manager</div>
        <nav className="flex-1 space-y-1 p-2">
          {navItems.map((item) => {
            const Icon = item.icon;
            if (item.disabled) {
              return (
                <span
                  key={item.to}
                  className="flex cursor-not-allowed items-center gap-2 rounded-md px-3 py-2 text-sm text-muted-foreground opacity-60"
                  title="Coming in a later milestone"
                >
                  <Icon className="h-4 w-4" />
                  {item.label}
                </span>
              );
            }
            return (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors',
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'hover:bg-accent hover:text-accent-foreground',
                  )
                }
              >
                <Icon className="h-4 w-4" />
                {item.label}
              </NavLink>
            );
          })}
        </nav>
        <div className="border-t p-3 text-xs text-muted-foreground">
          {health.isLoading && <span>Backend: ...</span>}
          {health.isError && <span className="text-destructive">Backend: offline</span>}
          {health.data && (
            <span>
              Backend: {health.data.status} v{health.data.version}
            </span>
          )}
        </div>
      </aside>
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  );
}
