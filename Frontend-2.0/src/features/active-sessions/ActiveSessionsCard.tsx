import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MonitorSmartphone } from 'lucide-react'
import { toast } from 'sonner'

import { getSessions, logoutAll, revokeSession, type SessionDto } from '@/shared/api/auth'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/Card'
import { useI18n } from '@/shared/i18n'

export function ActiveSessionsCard() {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const sessionsQuery = useQuery({
    queryKey: queryKeys.sessions,
    queryFn: getSessions,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.sessions })

  const revokeMutation = useMutation({
    mutationFn: (id: string) => revokeSession(id),
    onSuccess: () => {
      toast.success(t('Session signed out'))
      void invalidate()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to sign out session'))
    },
  })

  const logoutAllMutation = useMutation({
    mutationFn: () => logoutAll(),
    onSuccess: () => {
      toast.success(t('Signed out everywhere'))
      void invalidate()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to sign out'))
    },
  })

  const sessions = sessionsQuery.data ?? []

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('Active sessions')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          {t('Devices with an active session on your account. Sign out any you don’t recognise.')}
        </p>

        {sessionsQuery.isLoading ? (
          <p className="text-sm text-muted-foreground">{t('Loading...')}</p>
        ) : sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('No active sessions.')}</p>
        ) : (
          <ul className="space-y-3">
            {sessions.map((session) => (
              <SessionRow
                key={session.id}
                session={session}
                onRevoke={() => revokeMutation.mutate(session.id)}
                disabled={revokeMutation.isPending || logoutAllMutation.isPending}
                signOutLabel={t('Sign out')}
                thisDeviceLabel={t('This device')}
                lastUsedLabel={t('Last used')}
                neverLabel={t('Never')}
              />
            ))}
          </ul>
        )}

        <div className="border-t border-border pt-4">
          <Button
            variant="secondary"
            size="sm"
            disabled={logoutAllMutation.isPending || sessions.length === 0}
            onClick={() => logoutAllMutation.mutate()}
          >
            {t('Sign out everywhere')}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

function SessionRow({
  session,
  onRevoke,
  disabled,
  signOutLabel,
  thisDeviceLabel,
  lastUsedLabel,
  neverLabel,
}: {
  session: SessionDto
  onRevoke: () => void
  disabled: boolean
  signOutLabel: string
  thisDeviceLabel: string
  lastUsedLabel: string
  neverLabel: string
}) {
  return (
    <li className="flex items-start justify-between gap-4 rounded-lg border border-border bg-background/60 p-4">
      <div className="flex min-w-0 items-start gap-3">
        <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
          <MonitorSmartphone className="h-4 w-4" />
        </span>
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-sm font-medium text-foreground">
              {session.userAgent?.trim() || session.createdByIp || 'Unknown device'}
            </p>
            {session.current ? <Badge variant="success">{thisDeviceLabel}</Badge> : null}
          </div>
          <p className="text-xs text-muted-foreground">
            {lastUsedLabel}: {session.lastUsedAtUtc ? formatDateTime(session.lastUsedAtUtc) : neverLabel}
          </p>
        </div>
      </div>
      {session.current ? null : (
        <Button variant="secondary" size="sm" disabled={disabled} onClick={onRevoke}>
          {signOutLabel}
        </Button>
      )}
    </li>
  )
}
