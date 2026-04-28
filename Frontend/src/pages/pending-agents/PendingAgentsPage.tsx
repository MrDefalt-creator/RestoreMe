import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'

import { useAuthStore } from '@/app/store/auth-store'
import { ApproveAgentDialog } from '@/features/approve-agent/ApproveAgentDialog'
import { getPendingAgents } from '@/entities/agent/api'
import type { PendingAgent } from '@/entities/agent/model/types'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { SectionHeading } from '@/shared/ui/SectionHeading'

export function PendingAgentsPage() {
  const [selectedPendingAgent, setSelectedPendingAgent] = useState<PendingAgent | null>(null)
  const role = useAuthStore((state) => state.user?.role)
  const canApprove = role === 'operator' || role === 'admin'
  const pendingAgentsQuery = useQuery({
    queryKey: queryKeys.pendingAgents,
    queryFn: getPendingAgents,
  })

  const pendingAgents = pendingAgentsQuery.data ?? []

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow="Admissions"
        title="Pending agent requests"
        description="Review machine registrations before they become manageable backup agents in the admin panel."
      />

      {pendingAgents.length ? (
        <Card className="overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-surface-100 text-ink-800">
                <tr>
                  {['Machine', 'OS', 'Version', 'Created', 'Status', 'Action'].map((label) => (
                    <th key={label} className="px-4 py-3 font-medium">
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {pendingAgents.map((agent) => (
                  <tr key={agent.id} className="border-t border-surface-200">
                    <td className="px-4 py-3 font-medium text-ink-950">{agent.machineName}</td>
                    <td className="px-4 py-3 text-ink-800">{agent.osType}</td>
                    <td className="px-4 py-3 text-ink-800">{agent.version}</td>
                    <td className="px-4 py-3 text-ink-800">{formatDateTime(agent.createdAt)}</td>
                    <td className="px-4 py-3">
                      <Badge tone="warning">{agent.status}</Badge>
                    </td>
                    <td className="px-4 py-3">
                      {canApprove ? (
                        <Button size="sm" onClick={() => setSelectedPendingAgent(agent)}>
                          Approve
                        </Button>
                      ) : (
                        <span className="text-xs font-medium uppercase tracking-[0.18em] text-ink-800/55">
                          Read only
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      ) : (
        <EmptyState title="Queue is empty" description="There are no pending registration requests right now." />
      )}

      <ApproveAgentDialog
        open={canApprove && Boolean(selectedPendingAgent)}
        pendingAgent={selectedPendingAgent}
        onClose={() => setSelectedPendingAgent(null)}
      />
    </div>
  )
}
