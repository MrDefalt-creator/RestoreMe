import * as RadixDialog from '@radix-ui/react-dialog'
import { Command } from 'cmdk'
import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Archive,
  Bell,
  Download,
  HardDriveDownload,
  History,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Moon,
  ScrollText,
  Search,
  ShieldCheck,
  Users,
  Workflow,
} from 'lucide-react'

import { useAuthStore } from '@/app/store/auth-store'
import { useTheme } from '@/app/providers/ThemeProvider'
import { useUiStore } from '@/app/store/ui-store'
import { queryKeys } from '@/shared/lib/query'
import { useI18n } from '@/shared/i18n'
import type { Agent } from '@/shared/api/agents'
import type { BackupPolicy } from '@/shared/api/policies'
import type { Job } from '@/shared/api/jobs'
import type { Artifact } from '@/shared/api/artifacts'

const NAV_PAGES = [
  { to: '/', label: 'Overview', Icon: LayoutDashboard },
  { to: '/agents', label: 'Agents', Icon: HardDriveDownload },
  { to: '/pending-agents', label: 'Approvals', Icon: ShieldCheck },
  { to: '/policies', label: 'Policies', Icon: Workflow },
  { to: '/jobs', label: 'Jobs', Icon: History },
  { to: '/backups', label: 'Backups', Icon: Archive },
  { to: '/users', label: 'Users', Icon: Users },
  { to: '/notifications', label: 'Notifications', Icon: Bell },
  { to: '/audit-log', label: 'Audit log', Icon: ScrollText },
  { to: '/account', label: 'Account', Icon: KeyRound },
]

const ITEM = 'flex cursor-default select-none items-center gap-3 rounded-md px-3 py-2 text-sm text-foreground outline-none data-[selected=true]:bg-secondary'

export function CommandPalette() {
  const { t } = useI18n()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { theme, setThemeMode } = useTheme()
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)
  const isOpen = useUiStore((state) => state.commandPaletteOpen)
  const setOpen = useUiStore((state) => state.setCommandPaletteOpen)
  const setInstallAgentDialogOpen = useUiStore((state) => state.setInstallAgentDialogOpen)

  const canWrite = user?.role === 'admin' || user?.role === 'operator'
  const isDark = theme === 'dark'

  const agents = (queryClient.getQueryData<Agent[]>(queryKeys.agents) ?? []).slice(0, 50)
  const policies = (queryClient.getQueryData<BackupPolicy[]>(queryKeys.policies) ?? []).slice(0, 50)
  const jobs = (queryClient.getQueryData<Job[]>(queryKeys.jobs) ?? []).slice(0, 50)
  const artifacts = (queryClient.getQueryData<Artifact[]>(queryKeys.artifacts) ?? []).slice(0, 50)

  function go(to: string) {
    navigate(to)
    setOpen(false)
  }

  return (
    <RadixDialog.Root open={isOpen} onOpenChange={(open) => !open && setOpen(false)}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <RadixDialog.Content
          className="fixed left-1/2 top-[15vh] z-50 w-full max-w-[560px] -translate-x-1/2 outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95"
        >
          <RadixDialog.Title className="sr-only">{t('Command palette')}</RadixDialog.Title>
          <Command className="overflow-hidden rounded-xl border border-border bg-card shadow-[var(--shadow-xl)]">
            <div className="flex items-center border-b border-border px-4">
              <Search className="h-4 w-4 shrink-0 text-muted-foreground" />
              <Command.Input
                placeholder={t('Search or type a command...')}
                className="flex-1 bg-transparent py-3 pl-3 text-sm text-foreground outline-none placeholder:text-muted-foreground"
              />
              <kbd className="hidden rounded border border-border px-1.5 py-0.5 text-xs text-muted-foreground sm:block">ESC</kbd>
            </div>

            <Command.List className="max-h-[400px] overflow-y-auto p-2">
              <Command.Empty className="px-4 py-8 text-center text-sm text-muted-foreground">
                {t('No results found.')}
              </Command.Empty>

              <Command.Group heading={t('Pages')}>
                {NAV_PAGES.map(({ to, label, Icon }) => (
                  <Command.Item key={to} value={label} onSelect={() => go(to)} className={ITEM}>
                    <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
                    {t(label)}
                  </Command.Item>
                ))}
              </Command.Group>

              {agents.length > 0 && (
                <Command.Group heading={t('Agents')}>
                  {agents.map((agent) => (
                    <Command.Item
                      key={agent.id}
                      value={`${agent.name} ${agent.machineName ?? ''}`}
                      onSelect={() => go(`/agents?id=${agent.id}`)}
                      className={ITEM}
                    >
                      <HardDriveDownload className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <span className="flex-1 truncate">{agent.name}</span>
                      {agent.machineName ? <span className="text-xs text-muted-foreground">{agent.machineName}</span> : null}
                    </Command.Item>
                  ))}
                </Command.Group>
              )}

              {policies.length > 0 && (
                <Command.Group heading={t('Policies')}>
                  {policies.map((policy) => (
                    <Command.Item
                      key={policy.id}
                      value={policy.name}
                      onSelect={() => go(`/policies?id=${policy.id}`)}
                      className={ITEM}
                    >
                      <Workflow className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <span className="flex-1 truncate">{policy.name}</span>
                      <span className="text-xs uppercase tracking-wider text-muted-foreground">{policy.type}</span>
                    </Command.Item>
                  ))}
                </Command.Group>
              )}

              {jobs.length > 0 && (
                <Command.Group heading={t('Jobs')}>
                  {jobs.map((job) => (
                    <Command.Item
                      key={job.id}
                      value={`${job.policyName ?? ''} ${job.agentName ?? ''} ${job.status}`}
                      onSelect={() => go(`/jobs?id=${job.id}`)}
                      className={ITEM}
                    >
                      <History className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <span className="flex-1 truncate">{job.policyName ?? job.name ?? job.id.slice(0, 8)}</span>
                      <span className="text-xs text-muted-foreground">{job.status}</span>
                    </Command.Item>
                  ))}
                </Command.Group>
              )}

              {artifacts.length > 0 && (
                <Command.Group heading={t('Backups')}>
                  {artifacts.map((artifact) => (
                    <Command.Item
                      key={artifact.id}
                      value={`${artifact.name ?? ''} ${artifact.fileName ?? ''}`}
                      onSelect={() => go(`/backups?id=${artifact.id}`)}
                      className={ITEM}
                    >
                      <Archive className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <span className="flex-1 truncate">{artifact.name ?? artifact.fileName ?? artifact.id.slice(0, 8)}</span>
                    </Command.Item>
                  ))}
                </Command.Group>
              )}

              <Command.Group heading={t('Actions')}>
                {canWrite ? (
                  <Command.Item
                    value={t('Install new agent')}
                    onSelect={() => { setInstallAgentDialogOpen(true); setOpen(false) }}
                    className={ITEM}
                  >
                    <Download className="h-4 w-4 shrink-0 text-muted-foreground" />
                    {t('Install new agent')}
                  </Command.Item>
                ) : null}
                {canWrite ? (
                  <Command.Item
                    value={t('Create policy')}
                    onSelect={() => go('/policies')}
                    className={ITEM}
                  >
                    <Workflow className="h-4 w-4 shrink-0 text-muted-foreground" />
                    {t('Create policy')}
                  </Command.Item>
                ) : null}
                <Command.Item
                  value={isDark ? t('Switch to light theme') : t('Switch to dark theme')}
                  onSelect={() => { setThemeMode(isDark ? 'light' : 'dark'); setOpen(false) }}
                  className={ITEM}
                >
                  <Moon className="h-4 w-4 shrink-0 text-muted-foreground" />
                  {t('Toggle theme')}
                </Command.Item>
                <Command.Item
                  value={t('Sign out')}
                  onSelect={() => { clearSession(); navigate('/login', { replace: true }); setOpen(false) }}
                  className={`${ITEM} text-destructive data-[selected=true]:bg-destructive/10 data-[selected=true]:text-destructive`}
                >
                  <LogOut className="h-4 w-4 shrink-0" />
                  {t('Sign out')}
                </Command.Item>
              </Command.Group>
            </Command.List>
          </Command>
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  )
}
