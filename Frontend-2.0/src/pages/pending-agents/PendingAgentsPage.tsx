import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Inbox, Server } from 'lucide-react'

import { getPendingAgents, type PendingAgent } from '@/shared/api/agents'
import { ApproveAgentDialog, RejectAgentDialog } from '@/features/approve-agent'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime } from '@/shared/lib/format'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { useAuthStore } from '@/app/store/auth-store'

export function PendingAgentsPage() {
  const { t, tp } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const role = useAuthStore((state) => state.user?.role)
  const canApprove = role === 'admin' || role === 'operator'
  const [approving, setApproving] = useState<PendingAgent | null>(null)
  const [rejecting, setRejecting] = useState<PendingAgent | null>(null)

  const pendingQuery = useQuery({
    queryKey: queryKeys.pendingAgents,
    queryFn: getPendingAgents,
    ...liveQueryOptions,
  })

  const pendingAgents = pendingQuery.data ?? []

  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold tracking-tight text-foreground">
          {t('Pending Approvals')}
        </h1>
        <p className="mt-2 text-muted-foreground">
          {t('Review and approve new agent registration requests')}
        </p>
        <Badge variant="accent">
          {tp('{count} waiting', pendingAgents.length, { count: pendingAgents.length })}
        </Badge>
      </div>

      <div className="space-y-4">
        {pendingAgents.length ? (
          pendingAgents.map((agent) => (
            <Card key={agent.id} className="overflow-hidden">
              <CardHeader className="pb-4">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-4">
                    <div className="flex h-14 w-14 items-center justify-center rounded-lg bg-primary/10 text-primary shadow-[var(--shadow-sm)]">
                      <Server className="h-6 w-6" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <CardTitle className="text-lg">{agent.machineName}</CardTitle>
                      <CardDescription>
                        {t('Registered')}: {formatDateTime(agent.createdAt)}
                      </CardDescription>
                    </div>
                  </div>
                  <Badge variant="accent">{t('Pending')}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-x-8 gap-y-4 border-y border-border py-4 text-sm md:grid-cols-2">
                  <PendingDetail label={t('Agent ID')} value={agent.id} mono />
                  <PendingDetail label={t('OS')} value={agent.osType} />
                  <PendingDetail label={t('Version')} value={agent.version} />
                  <PendingDetail label={t('Status')} value={t(agent.status)} />
                </div>

                {canApprove ? (
                  <div className="flex gap-3">
                    <Button variant="success" onClick={() => setApproving(agent)} className="flex-1">
                      {t('Approve')}
                    </Button>
                    <Button variant="danger" onClick={() => setRejecting(agent)} className="flex-1">
                      {t('Reject')}
                    </Button>
                  </div>
                ) : (
                  <div className="rounded-lg border border-border bg-secondary p-3 text-sm text-muted-foreground">
                    {t('Read only')}
                  </div>
                )}
              </CardContent>
            </Card>
          ))
        ) : (
          <EmptyState
            icon={<Inbox className="h-9 w-9" />}
            title={t('No pending requests')}
            description={t('All agents have been approved. New registrations will appear here.')}
          />
        )}
      </div>

      <ApproveAgentDialog
        open={canApprove && Boolean(approving)}
        pendingAgent={approving}
        onClose={() => setApproving(null)}
      />
      <RejectAgentDialog
        open={canApprove && Boolean(rejecting)}
        pendingAgent={rejecting}
        onClose={() => setRejecting(null)}
      />
    </div>
  )
}

function PendingDetail({
  label,
  value,
  mono = false,
}: {
  label: string
  value: string
  mono?: boolean
}) {
  return (
    <div className="min-w-0">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted-foreground">{label}</p>
      <p className={mono ? 'mt-1 truncate font-mono text-xs text-foreground' : 'mt-1 truncate text-sm font-medium text-foreground'}>
        {value}
      </p>
    </div>
  )
}
