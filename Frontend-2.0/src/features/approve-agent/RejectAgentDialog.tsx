import { useMutation, useQueryClient } from '@tanstack/react-query'
import { XCircle } from 'lucide-react'
import { toast } from 'sonner'

import { rejectAgent, type PendingAgent } from '@/shared/api/agents'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { useI18n } from '@/shared/i18n'

type RejectAgentDialogProps = {
  open: boolean
  pendingAgent: PendingAgent | null
  onClose: () => void
}

export function RejectAgentDialog({ open, pendingAgent, onClose }: RejectAgentDialogProps) {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: rejectAgent,
    onMutate: async (pendingId) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.pendingAgents })
      const previous = queryClient.getQueryData<PendingAgent[]>(queryKeys.pendingAgents)
      queryClient.setQueryData<PendingAgent[]>(queryKeys.pendingAgents, (old) =>
        (old ?? []).filter((agent) => agent.id !== pendingId),
      )
      onClose()
      return { previous }
    },
    onSuccess: () => {
      toast.success(t('Agent rejected'))
    },
    onError: (error, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKeys.pendingAgents, context.previous)
      }
      toast.error(error instanceof Error ? error.message : t('Failed to reject agent'))
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.pendingAgents })
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Reject pending agent')}
      description={t('The agent will be told that this registration request was rejected and will stop waiting for approval.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button
            variant="danger"
            disabled={!pendingAgent || mutation.isPending}
            onClick={() => {
              if (!pendingAgent) return
              mutation.mutate(pendingAgent.id)
            }}
          >
            {mutation.isPending ? t('Rejecting...') : t('Reject agent')}
          </Button>
        </>
      }
    >
      <div className="rounded-lg border border-destructive/20 bg-destructive/8 p-4">
        <div className="flex items-start gap-3">
          <XCircle className="mt-0.5 h-5 w-5 shrink-0 text-destructive" />
          <div>
            <p className="font-medium text-foreground">{pendingAgent?.machineName}</p>
            <p className="mt-1 text-sm leading-6 text-muted-foreground">
              {t('This action keeps the machine out of backup policy assignment until it registers again under a pending request.')}
            </p>
          </div>
        </div>
      </div>
    </Dialog>
  )
}
