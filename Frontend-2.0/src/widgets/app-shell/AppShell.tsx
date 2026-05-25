import { useEffect } from 'react'
import {
  Archive,
  Bell,
  HardDriveDownload,
  History,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  ScrollText,
  ShieldCheck,
  Sun,
  UserRound,
  Users,
  Workflow,
  X,
} from 'lucide-react'
import * as Tooltip from '@radix-ui/react-tooltip'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { useTheme } from '@/app/providers/ThemeProvider'
import { useUiStore, type Density } from '@/app/store/ui-store'
import { BrandMark } from '@/shared/ui/BrandMark'
import { Button } from '@/shared/ui/Button'
import { LiveBadge } from '@/shared/ui/LiveBadge'
import { SegmentedControl } from '@/shared/ui/SegmentedControl'
import { ErrorBoundary } from '@/shared/ui/ErrorBoundary'
import { cn } from '@/shared/lib/cn'
import { normalizeAuthRole, logout } from '@/shared/api/auth'
import { authEvents } from '@/shared/lib/auth-events'
import { formatRoleLabel, useI18n } from '@/shared/i18n'
import { CommandPalette, useCommandPalette } from '@/widgets/command-palette'
import { InstallAgentDialog } from '@/features/install-agent'

type NavItem = {
  to: string
  label: string
  icon: typeof LayoutDashboard
  end?: boolean
  roles?: ('admin' | 'operator' | 'viewer')[]
}

const navigation: NavItem[] = [
  { to: '/', label: 'Overview', icon: LayoutDashboard, end: true },
  { to: '/agents', label: 'Agents', icon: HardDriveDownload },
  { to: '/pending-agents', label: 'Approvals', icon: ShieldCheck },
  { to: '/policies', label: 'Policies', icon: Workflow },
  { to: '/jobs', label: 'Jobs', icon: History },
  { to: '/backups', label: 'Backups', icon: Archive },
  { to: '/users', label: 'Users', icon: Users, roles: ['admin'] },
  { to: '/notifications', label: 'Notifications', icon: Bell, roles: ['admin'] },
  { to: '/audit-log', label: 'Audit log', icon: ScrollText, roles: ['admin'] },
  { to: '/account', label: 'Account', icon: KeyRound },
]

export function AppShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const { t } = useI18n()
  const { theme, setThemeMode } = useTheme()
  const sidebarState = useUiStore((state) => state.sidebarState)
  const toggleSidebar = useUiStore((state) => state.toggleSidebar)
  const mobileNavOpen = useUiStore((state) => state.mobileNavOpen)
  const toggleMobileNav = useUiStore((state) => state.toggleMobileNav)
  const closeMobileNav = useUiStore((state) => state.closeMobileNav)
  const density = useUiStore((state) => state.density)
  const setDensity = useUiStore((state) => state.setDensity)
  const installAgentDialogOpen = useUiStore((state) => state.installAgentDialogOpen)
  const setInstallAgentDialogOpen = useUiStore((state) => state.setInstallAgentDialogOpen)
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)
  const isExpanded = sidebarState === 'expanded'
  const isExpandedView = mobileNavOpen || isExpanded
  const isDark = theme === 'dark'
  const canInstall = user?.role === 'admin' || user?.role === 'operator'

  useCommandPalette()

  useEffect(() => {
    if (!mobileNavOpen) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closeMobileNav()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [mobileNavOpen, closeMobileNav])

  useEffect(() => {
    if (typeof window === 'undefined') return
    const mq = window.matchMedia('(min-width: 768px)')
    const onChange = (event: MediaQueryListEvent) => {
      if (event.matches) closeMobileNav()
    }
    mq.addEventListener('change', onChange)
    return () => mq.removeEventListener('change', onChange)
  }, [closeMobileNav])

  useEffect(() => {
    document.documentElement.setAttribute('data-density', density)
  }, [density])

  useEffect(() => {
    return authEvents.onUnauthorized(() => {
      toast.error(t('Session expired. Please sign in again.'))
      if (location.pathname !== '/login') {
        navigate('/login', { replace: true })
      }
    })
  }, [navigate, location.pathname, t])

  const availableNavigation = navigation.filter((item) => {
    if (!item.roles) {
      return true
    }

    return Boolean(user?.role && item.roles.includes(normalizeAuthRole(user.role)))
  })

  return (
    <div className="min-h-screen bg-transparent text-foreground transition-colors duration-300">
      <div className="flex min-h-screen w-full">
        {mobileNavOpen ? (
          <button
            type="button"
            aria-label={t('Close navigation')}
            className="fixed inset-0 z-30 bg-black/50 backdrop-blur-sm md:hidden"
            onClick={closeMobileNav}
          />
        ) : null}
        <aside
          id="app-shell-sidebar"
          className={cn(
            'fixed inset-y-0 left-0 z-40 h-screen w-[280px] shrink-0 border-r border-border bg-card/95 backdrop-blur-xl transition-transform duration-200 ease-out',
            'md:sticky md:top-0 md:translate-x-0 md:transition-[width]',
            mobileNavOpen ? 'translate-x-0' : '-translate-x-full',
            isExpanded ? 'md:w-[264px]' : 'md:w-[82px]',
          )}
        >
          <div className="flex h-full flex-col gap-6 p-4">
            <div className={cn('flex items-center gap-3', isExpandedView ? 'justify-between' : 'justify-center')}>
              <BrandMark compact={!isExpandedView} subtitle="RestoreMe" />
              {mobileNavOpen ? (
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={closeMobileNav}
                  title={t('Close navigation')}
                  aria-label={t('Close navigation')}
                  className="md:hidden"
                >
                  <X className="h-4 w-4" />
                </Button>
              ) : null}
              {isExpanded ? (
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={toggleSidebar}
                  title={t('Collapse sidebar')}
                  aria-label={t('Collapse sidebar')}
                  className="hidden md:inline-flex"
                >
                  <Menu className="h-4 w-4" />
                </Button>
              ) : null}
            </div>

            {!isExpandedView ? (
              <Button
                variant="ghost"
                size="icon"
                className="mx-auto hidden md:inline-flex"
                onClick={toggleSidebar}
                title={t('Expand sidebar')}
                aria-label={t('Expand sidebar')}
              >
                <Menu className="h-4 w-4" />
              </Button>
            ) : null}

            <Tooltip.Provider delayDuration={200}>
              <nav className="flex flex-1 flex-col gap-1">
                {availableNavigation.map(({ to, label, icon: Icon, end }) => (
                  <Tooltip.Root key={to}>
                    <Tooltip.Trigger asChild>
                      <NavLink
                        to={to}
                        end={end}
                        onClick={closeMobileNav}
                        className={({ isActive }) =>
                          cn(
                            'group flex h-11 items-center rounded-lg text-sm font-medium transition duration-150 ease-out',
                            isExpandedView ? 'gap-3 px-3' : 'justify-center px-0',
                            isActive
                              ? 'bg-primary text-primary-foreground shadow-[0_10px_28px_hsl(var(--primary)/0.18)]'
                              : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                          )
                        }
                        title={isExpandedView ? undefined : t(label)}
                      >
                        <Icon className="h-4 w-4 shrink-0" strokeWidth={1.9} />
                        {isExpandedView ? <span className="truncate">{t(label)}</span> : null}
                      </NavLink>
                    </Tooltip.Trigger>
                    {!isExpandedView ? (
                      <Tooltip.Portal>
                        <Tooltip.Content
                          side="right"
                          sideOffset={12}
                          className="z-50 rounded-md border border-border bg-card px-3 py-1.5 text-sm text-foreground shadow-[var(--shadow-md)]"
                        >
                          {t(label)}
                          <Tooltip.Arrow className="fill-border" />
                        </Tooltip.Content>
                      </Tooltip.Portal>
                    ) : null}
                  </Tooltip.Root>
                ))}
              </nav>
            </Tooltip.Provider>

            <div className="space-y-3 border-t border-border pt-4">
              <Button
                variant="secondary"
                size={isExpandedView ? 'md' : 'icon'}
                className={cn('w-full', isExpandedView ? 'justify-start' : '')}
                onClick={() => setThemeMode(isDark ? 'light' : 'dark')}
                title={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
                aria-label={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
              >
                {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
                {isExpandedView ? <span>{isDark ? t('Light theme') : t('Dark theme')}</span> : null}
              </Button>

              {user ? (
                <div className={cn('rounded-lg border border-border bg-background/70 p-3', isExpandedView ? '' : 'px-2')}>
                  <div className={cn('flex items-center gap-3', isExpandedView ? '' : 'justify-center')}>
                    <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-foreground">
                      <UserRound className="h-4 w-4" />
                    </span>
                    {isExpandedView ? (
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-foreground">{user.username}</p>
                        <p className="text-xs text-muted-foreground">{formatRoleLabel(normalizeAuthRole(user.role), t)}</p>
                      </div>
                    ) : null}
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-30 border-b border-border bg-background/78 px-4 py-3 backdrop-blur-xl md:px-8">
            <div className="flex items-center justify-between gap-4">
              <div className="min-w-0">
                <p className="text-sm font-medium text-foreground">RestoreMe</p>
                <p className="text-xs text-muted-foreground">{t('Calm backup operations console')}</p>
              </div>
              <div className="flex items-center gap-2">
                <Button
                  variant="secondary"
                  size="icon"
                  className="md:hidden"
                  onClick={toggleMobileNav}
                  title={t('Toggle navigation')}
                  aria-label={t('Toggle navigation')}
                  aria-expanded={mobileNavOpen}
                  aria-controls="app-shell-sidebar"
                >
                  <Menu className="h-4 w-4" />
                </Button>
                <LiveBadge />
                <SegmentedControl<Density>
                  value={density}
                  onChange={setDensity}
                  aria-label={t('Density')}
                  options={[
                    { value: 'comfy', label: t('Comfy') },
                    { value: 'compact', label: t('Compact') },
                  ]}
                />
                <Button
                  variant="secondary"
                  size="icon"
                  onClick={() => setThemeMode(isDark ? 'light' : 'dark')}
                  title={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
                  aria-label={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
                >
                  {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={async () => {
                    try { await logout() } catch { /* ignore */ }
                    clearSession()
                    navigate('/login', { replace: true })
                  }}
                >
                  <LogOut className="h-4 w-4" />
                  {t('Sign out')}
                </Button>
              </div>
            </div>
          </header>

          <main className="min-w-0 flex-1 overflow-x-hidden px-4 py-6 md:px-8 md:py-8">
            <div className="mx-auto w-full max-w-[1680px] animate-fade-in">
              <ErrorBoundary resetKey={location.pathname}>
                <Outlet />
              </ErrorBoundary>
            </div>
          </main>
        </div>
      </div>
      <CommandPalette />
      {canInstall ? (
        <InstallAgentDialog open={installAgentDialogOpen} onClose={() => setInstallAgentDialogOpen(false)} />
      ) : null}
    </div>
  )
}
