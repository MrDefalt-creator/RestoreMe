import {
  Archive,
  HardDriveDownload,
  History,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  RefreshCw,
  ShieldCheck,
  Sun,
  UserRound,
  Users,
  Workflow,
} from 'lucide-react'
import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'

import { useAuthStore } from '@/app/store/auth-store'
import { useTheme } from '@/app/providers/ThemeProvider'
import { useUiStore } from '@/app/store/ui-store'
import { BrandMark } from '@/shared/ui/BrandMark'
import { Button } from '@/shared/ui/Button'
import { cn } from '@/shared/lib/cn'
import { normalizeAuthRole } from '@/shared/api/auth'
import { formatRoleLabel, useI18n } from '@/shared/i18n'

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
  { to: '/account', label: 'Account', icon: KeyRound },
]

export function AppShell() {
  const navigate = useNavigate()
  const { t } = useI18n()
  const { theme, setTheme } = useTheme()
  const queryClient = useQueryClient()
  const isFetching = useIsFetching()
  const sidebarState = useUiStore((state) => state.sidebarState)
  const toggleSidebar = useUiStore((state) => state.toggleSidebar)
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)
  const isExpanded = sidebarState === 'expanded'
  const isDark = theme === 'dark'

  const availableNavigation = navigation.filter((item) => {
    if (!item.roles) {
      return true
    }

    return Boolean(user?.role && item.roles.includes(normalizeAuthRole(user.role)))
  })

  return (
    <div className="min-h-screen bg-transparent text-foreground transition-colors duration-300">
      <div className="flex min-h-screen w-full">
        <aside
          className={cn(
            'sticky top-0 hidden h-screen shrink-0 border-r border-border bg-card/92 backdrop-blur-xl transition-[width] duration-200 ease-out md:block',
            isExpanded ? 'w-[264px]' : 'w-[82px]',
          )}
        >
          <div className="flex h-full flex-col gap-6 p-4">
            <div className={cn('flex items-center gap-3', isExpanded ? 'justify-between' : 'justify-center')}>
              <BrandMark compact={!isExpanded} subtitle="RestoreMe" />
              {isExpanded ? (
                <Button variant="ghost" size="icon" onClick={toggleSidebar} title={t('Collapse sidebar')}>
                  <Menu className="h-4 w-4" />
                </Button>
              ) : null}
            </div>

            {!isExpanded ? (
              <Button
                variant="ghost"
                size="icon"
                className="mx-auto"
                onClick={toggleSidebar}
                title={t('Expand sidebar')}
              >
                <Menu className="h-4 w-4" />
              </Button>
            ) : null}

            <nav className="flex flex-1 flex-col gap-1">
              {availableNavigation.map(({ to, label, icon: Icon, end }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    cn(
                      'group flex h-11 items-center rounded-lg text-sm font-medium transition duration-150 ease-out',
                      isExpanded ? 'gap-3 px-3' : 'justify-center px-0',
                      isActive
                        ? 'bg-primary text-primary-foreground shadow-[0_10px_28px_hsl(var(--primary)/0.18)]'
                        : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                    )
                  }
                  title={isExpanded ? undefined : t(label)}
                >
                  <Icon className="h-4 w-4 shrink-0" strokeWidth={1.9} />
                  {isExpanded ? <span className="truncate">{t(label)}</span> : null}
                </NavLink>
              ))}
            </nav>

            <div className="space-y-3 border-t border-border pt-4">
              <Button
                variant="secondary"
                size={isExpanded ? 'md' : 'icon'}
                className={cn('w-full', isExpanded ? 'justify-start' : '')}
                onClick={() => setTheme(isDark ? 'light' : 'dark')}
                title={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
              >
                {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
                {isExpanded ? <span>{isDark ? t('Light theme') : t('Dark theme')}</span> : null}
              </Button>

              {user ? (
                <div className={cn('rounded-lg border border-border bg-background/70 p-3', isExpanded ? '' : 'px-2')}>
                  <div className={cn('flex items-center gap-3', isExpanded ? '' : 'justify-center')}>
                    <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-foreground">
                      <UserRound className="h-4 w-4" />
                    </span>
                    {isExpanded ? (
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
                  onClick={toggleSidebar}
                  title={t('Toggle navigation')}
                >
                  <Menu className="h-4 w-4" />
                </Button>
                <Button
                  variant="secondary"
                  size="icon"
                  onClick={() => void queryClient.invalidateQueries()}
                  disabled={isFetching > 0}
                  title={t('Refresh data')}
                >
                  <RefreshCw className={cn('h-4 w-4', isFetching > 0 ? 'animate-spin' : '')} />
                </Button>
                <Button
                  variant="secondary"
                  size="icon"
                  onClick={() => setTheme(isDark ? 'light' : 'dark')}
                  title={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
                >
                  {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => {
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
              <Outlet />
            </div>
          </main>
        </div>
      </div>
    </div>
  )
}
