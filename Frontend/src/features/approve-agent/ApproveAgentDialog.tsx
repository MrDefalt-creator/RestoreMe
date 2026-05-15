import { useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { approvePendingAgent } from '@/entities/agent/api'
import type { PendingAgent } from '@/entities/agent/model/types'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { useI18n } from '@/shared/i18n'

const approveSchema = z.object({
  name: z.string().trim().min(2, 'Name is required').max(80, 'Name is too long'),
})

type ApproveAgentValues = z.infer<typeof approveSchema>

type ApproveAgentDialogProps = {
  open: boolean
  pendingAgent: PendingAgent | null
  onClose: () => void
}

export function ApproveAgentDialog({
  open,
  pendingAgent,
  onClose,
}: ApproveAgentDialogProps) {
  const { t } = useI18n()
  const formError = (message?: string) => (message ? t(message) : undefined)
  const queryClient = useQueryClient()
  const form = useForm<ApproveAgentValues>({
    resolver: zodResolver(approveSchema),
    mode: 'onChange',
    defaultValues: {
      name: pendingAgent?.machineName ?? '',
    },
  })

  useEffect(() => {
    form.reset({
      name: pendingAgent?.machineName ?? '',
    })
  }, [form, pendingAgent])

  const mutation = useMutation({
    mutationFn: (values: ApproveAgentValues) => {
      if (!pendingAgent) {
        throw new Error('Pending agent is required')
      }

      return approvePendingAgent({
        pendingId: pendingAgent.id,
        name: values.name,
      })
    },
    onSuccess: () => {
      toast.success(t('Pending agent approved'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.pendingAgents })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Approval failed'))
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Approve pending agent')}
      description={t('Assign the machine a readable agent name before it becomes visible in the operational workspace.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button
            disabled={!form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {mutation.isPending ? t('Approving...') : t('Approve agent')}
          </Button>
        </>
      }
    >
      <div className="space-y-2">
        <label className="text-sm font-medium text-ink-900" htmlFor="approve-name">
          {t('Agent name')}
        </label>
        <Input id="approve-name" placeholder={t('Accounting workstation')} {...form.register('name')} />
        {form.formState.errors.name ? (
          <p className="text-sm text-danger-500">{formError(form.formState.errors.name.message)}</p>
        ) : null}
      </div>
    </Dialog>
  )
}
