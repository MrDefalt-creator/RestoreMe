import { Archive, HardDriveDownload, History, LayoutDashboard, Menu, ShieldCheck, Workflow } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'

import { useUiStore } from '@/app/store/ui-store'
import { env } from '@/shared/config/env'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/Button'
import { Badge } from '@/shared/ui/Badge'

const navigation = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/agents', label: 'Agents', icon: HardDriveDownload },
  { to: '/pending-agents', label: 'Pending', icon: ShieldCheck },
  { to: '/policies', label: 'Policies', icon: Workflow },
  { to: '/jobs', label: 'Jobs', icon: History },
  { to: '/artifacts', label: 'Artifacts', icon: Archive },
]

export function AppShell() {
  const sidebarState = useUiStore((state) => state.sidebarState)
  const toggleSidebar = useUiStore((state) => state.toggleSidebar)

  return (
    <div className="min-h-screen bg-[linear-gradient(180deg,rgba(248,251,253,0.94),rgba(237,244,250,0.98))]">
      <div className="flex min-h-screen w-full bg-[rgba(255,255,255,0.58)] shadow-[0_24px_90px_rgba(16,32,51,0.06)]">
        <aside
          className={cn(
            'sticky top-0 min-h-screen border-r border-white/10 bg-[linear-gradient(180deg,rgba(11,24,39,0.98),rgba(16,46,72,0.96))] text-white transition-all duration-300',
            sidebarState === 'expanded' ? 'w-[286px]' : 'w-[88px]',
          )}
        >
          <div
            className={cn(
              'flex h-full flex-col gap-8 p-4',
              sidebarState === 'expanded' ? '' : 'items-center',
            )}
          >
            <div
              className={cn(
                'space-y-5',
                sidebarState === 'expanded' ? '' : 'flex w-full flex-col items-center',
              )}
            >
              <div
                className={cn(
                  'flex items-center',
                  sidebarState === 'expanded'
                    ? 'justify-between gap-3'
                    : 'w-full justify-center gap-0',
                )}
              >
                <div
                  className={cn(
                    'overflow-hidden transition-all',
                    sidebarState === 'expanded' ? 'w-auto opacity-100' : 'w-0 opacity-0',
                  )}
                >
                  <p className="text-xs font-semibold uppercase tracking-[0.3em] text-sky-200/85">
                    RestorMe
                  </p>
                  <h1 className="mt-2 text-2xl font-semibold tracking-tight">
                    Backup Console
                  </h1>
                </div>
                <Button
                  variant="ghost"
                  className="h-11 w-11 rounded-2xl border border-white/10 bg-white/5 p-0 text-white hover:bg-white/10"
                  onClick={toggleSidebar}
                >
                  <Menu className="h-4 w-4" />
                </Button>
              </div>
              {sidebarState === 'expanded' ? (
                <p className="max-w-[14rem] text-sm text-sky-100/72">
                  Operational workspace for agents, policies and backup history.
                </p>
              ) : null}
            </div>

            <nav
              className={cn(
                'flex flex-1 flex-col gap-2',
                sidebarState === 'expanded' ? '' : 'items-center',
              )}
            >
              {navigation.map(({ to, label, icon: Icon, end }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    cn(
                      'group flex items-center rounded-2xl text-sm transition duration-200',
                      sidebarState === 'expanded'
                        ? 'gap-3 px-3 py-2.5'
                        : 'h-16 w-16 justify-center px-0 py-0',
                      sidebarState === 'expanded' && isActive
                        ? 'bg-sky-400/7 text-white ring-1 ring-inset ring-sky-200/14'
                        : 'text-sky-100/72 hover:bg-white/7 hover:text-white',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      <span
                        className={cn(
                          'flex shrink-0 items-center justify-center rounded-xl transition-all duration-200',
                          sidebarState === 'expanded' ? 'h-10 w-10' : 'h-12 w-12',
                          isActive
                            ? cn(
                                'text-sky-50 ring-1 ring-inset ring-sky-200/18',
                                sidebarState === 'expanded'
                                  ? 'bg-linear-to-br from-sky-300/24 to-cyan-200/10 shadow-[0_10px_24px_rgba(7,16,24,0.16)]'
                                  : 'bg-linear-to-br from-sky-300/20 to-cyan-200/10 shadow-[0_0_0_1px_rgba(125,211,252,0.18),0_8px_18px_rgba(7,16,24,0.16)]',
                              )
                            : 'bg-white/6 text-sky-100/80 group-hover:bg-white/10 group-hover:text-white',
                        )}
                      >
                        <Icon
                          strokeWidth={1.9}
                          className={cn(
                            sidebarState === 'expanded' ? 'h-[1.05rem] w-[1.05rem]' : 'h-[1.1rem] w-[1.1rem]',
                          )}
                        />
                      </span>
                      <span
                        className={cn(
                          'overflow-hidden whitespace-nowrap transition-all duration-200',
                          sidebarState === 'expanded' ? 'max-w-[140px] opacity-100' : 'max-w-0 opacity-0',
                        )}
                      >
                        {label}
                      </span>
                    </>
                  )}
                </NavLink>
              ))}
            </nav>

            {env.apiMode === 'mock' ? (
              sidebarState === 'expanded' ? (
              <div className="rounded-[24px] border border-white/10 bg-white/6 p-4">
                <div className="flex items-center gap-3">
                  <Badge tone="warning">Mock data mode</Badge>
                </div>
                <p className="mt-3 text-sm text-sky-100/76">
                  The prototype starts in mock mode until admin endpoints are completed.
                </p>
              </div>
            ) : (
              <div className="flex justify-center">
                <span className="h-2.5 w-2.5 rounded-full bg-amber-400" />
              </div>
            )
            ) : null}
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="flex items-center justify-between gap-4 border-b border-surface-200/80 bg-[rgba(255,255,255,0.78)] px-5 py-4 md:px-8">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.26em] text-accent-600">
                Diploma prototype
              </p>
              <p className="mt-1 text-sm text-ink-800">
                Architecture-first admin panel for RestorMe backup operations.
              </p>
            </div>
            <Badge tone="accent">Vite + React + Zustand + Query</Badge>
          </header>
          <main className="min-w-0 flex-1 overflow-x-hidden px-4 py-5 md:px-8 md:py-7">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  )
}
