import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Inbox, Server, XCircle } from 'lucide-react'

import { approveAgent, getPendingAgents, rejectAgent, type PendingAgent } from '@/shared/api/agents'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/Card'
import { Dialog } from '@/shared/ui/Dialog'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime } from '@/shared/lib/format'
import { toast } from 'sonner'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { useAuthStore } from '@/app/store/auth-store'

export function PendingAgentsPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const role = useAuthStore((state) => state.user?.role)
  const canApprove = role === 'admin' || role === 'operator'
  const [selectedAgent, setSelectedAgent] = useState<PendingAgent | null>(null)
  const [rejectingAgent, setRejectingAgent] = useState<PendingAgent | null>(null)
  const [agentName, setAgentName] = useState('')
  const pendingQuery = useQuery({
    queryKey: queryKeys.pendingAgents,
    queryFn: getPendingAgents,
    ...liveQueryOptions,
  })

  const approveMutation = useMutation({
    mutationFn: approveAgent,
    onSuccess: () => {
      void pendingQuery.refetch()
      setSelectedAgent(null)
      setAgentName('')
      toast.success(t('Agent approved'))
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Failed to approve agent'))
    },
  })

  const rejectMutation = useMutation({
    mutationFn: rejectAgent,
    onSuccess: () => {
      void pendingQuery.refetch()
      setRejectingAgent(null)
      toast.success(t('Agent rejected'))
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Failed to reject agent'))
    },
  })

  const pendingAgents = pendingQuery.data ?? []

  return (
    <div className="space-y-8">
      {/* Section Header */}
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold tracking-tight text-foreground">
          {t('Pending Approvals')}
        </h1>
        <p className="mt-2 text-muted-foreground">
          {t('Review and approve new agent registration requests')}
        </p>
        <Badge variant="accent">
          {t('{count} waiting', { count: pendingAgents.length })}
        </Badge>
      </div>

      {/* Pending Agents */}
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
                    <Button
                      variant="success"
                      onClick={() => {
                        setSelectedAgent(agent)
                        setAgentName(agent.machineName)
                      }}
                      disabled={approveMutation.isPending || rejectMutation.isPending}
                      className="flex-1"
                    >
                      {approveMutation.isPending ? t('Approving...') : t('Approve')}
                    </Button>
                    <Button
                      variant="danger"
                      className="flex-1"
                      onClick={() => setRejectingAgent(agent)}
                      disabled={approveMutation.isPending || rejectMutation.isPending}
                    >
                      {rejectMutation.isPending ? t('Rejecting...') : t('Reject')}
                    </Button>
                  </div>
                ) : (
                  <div className="rounded-lg border border-border bg-secondary p-3 text-sm text-muted-foreground">
                    {t('Read only')}
                  </div>
                )}

                {(approveMutation.error || rejectMutation.error) && (
                  <div className="rounded-lg border border-destructive/20 bg-destructive/8 p-3 text-sm text-destructive">
                    {(approveMutation.error ?? rejectMutation.error)?.message}
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
      <Dialog
        open={canApprove && Boolean(selectedAgent)}
        onClose={() => setSelectedAgent(null)}
        title={t('Approve pending agent')}
        description={t('Assign a readable name before this machine becomes available for backup policies.')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setSelectedAgent(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={!selectedAgent || agentName.trim().length < 2 || approveMutation.isPending}
              onClick={() => {
                if (!selectedAgent) {
                  return
                }
                approveMutation.mutate({
                  pendingId: selectedAgent.id,
                  name: agentName.trim(),
                })
              }}
            >
              {approveMutation.isPending ? t('Approving...') : t('Approve agent')}
            </Button>
          </>
        }
      >
        <div className="space-y-2">
          <label className="text-sm font-medium text-foreground" htmlFor="pending-agent-name">
            {t('Agent name')}
          </label>
          <Input
            id="pending-agent-name"
            value={agentName}
            onChange={(event) => setAgentName(event.target.value)}
            placeholder={t('Accounting workstation')}
          />
        </div>
      </Dialog>
      <Dialog
        open={canApprove && Boolean(rejectingAgent)}
        onClose={() => setRejectingAgent(null)}
        title={t('Reject pending agent')}
        description={t('The agent will be told that this registration request was rejected and will stop waiting for approval.')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setRejectingAgent(null)}>
              {t('Cancel')}
            </Button>
            <Button
              variant="danger"
              disabled={!rejectingAgent || rejectMutation.isPending}
              onClick={() => {
                if (!rejectingAgent) {
                  return
                }
                rejectMutation.mutate(rejectingAgent.id)
              }}
            >
              {rejectMutation.isPending ? t('Rejecting...') : t('Reject agent')}
            </Button>
          </>
        }
      >
        <div className="rounded-lg border border-destructive/20 bg-destructive/8 p-4">
          <div className="flex items-start gap-3">
            <XCircle className="mt-0.5 h-5 w-5 shrink-0 text-destructive" />
            <div>
              <p className="font-medium text-foreground">{rejectingAgent?.machineName}</p>
              <p className="mt-1 text-sm leading-6 text-muted-foreground">
                {t('This action keeps the machine out of backup policy assignment until it registers again under a pending request.')}
              </p>
            </div>
          </div>
        </div>
      </Dialog>
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
