import { useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { approveAgent, type PendingAgent } from '@/shared/api/agents'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { useI18n } from '@/shared/i18n'

type ApproveAgentDialogProps = {
  open: boolean
  pendingAgent: PendingAgent | null
  onClose: () => void
}

export function ApproveAgentDialog({ open, pendingAgent, onClose }: ApproveAgentDialogProps) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [name, setName] = useState(pendingAgent?.machineName ?? '')
  // Reset the editable name when the parent swaps to a different pending
  // agent — render-time setState with ref tracking, per the React docs.
  const lastIdRef = useRef(pendingAgent?.id ?? null)
  if (lastIdRef.current !== (pendingAgent?.id ?? null)) {
    lastIdRef.current = pendingAgent?.id ?? null
    setName(pendingAgent?.machineName ?? '')
  }

  const mutation = useMutation({
    mutationFn: approveAgent,
    onSuccess: () => {
      toast.success(t('Agent approved'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.pendingAgents })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Failed to approve agent'))
    },
  })

  const trimmed = name.trim()
  const isReady = Boolean(pendingAgent) && trimmed.length >= 2 && !mutation.isPending

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Approve pending agent')}
      description={t('Assign a readable name before this machine becomes available for backup policies.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button
            disabled={!isReady}
            onClick={() => {
              if (!pendingAgent) return
              mutation.mutate({ pendingId: pendingAgent.id, name: trimmed })
            }}
          >
            {mutation.isPending ? t('Approving...') : t('Approve agent')}
          </Button>
        </>
      }
    >
      <div className="space-y-2">
        <label className="text-sm font-medium text-foreground" htmlFor="approve-agent-name">
          {t('Agent name')}
        </label>
        <Input
          id="approve-agent-name"
          value={name}
          onChange={(event) => setName(event.target.value)}
          placeholder={t('Accounting workstation')}
        />
      </div>
    </Dialog>
  )
}
