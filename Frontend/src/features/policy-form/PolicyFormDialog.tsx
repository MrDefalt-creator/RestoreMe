import { useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import type { Agent } from '@/entities/agent/model/types'
import { createPolicy, updatePolicy } from '@/entities/policy/api'
import type { BackupPolicy } from '@/entities/policy/model/types'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'

const policySchema = z.object({
  agentId: z.string().uuid('Select an agent'),
  name: z.string().trim().min(3, 'Name is required').max(100, 'Name is too long'),
  sourcePath: z.string().trim().min(3, 'Source path is required'),
  intervalSeconds: z.number().int().min(60, 'Interval must be at least 60 seconds'),
  isEnabled: z.boolean(),
})

type PolicyFormValues = z.infer<typeof policySchema>

type PolicyFormDialogProps = {
  open: boolean
  agents: Agent[]
  policy: BackupPolicy | null
  onClose: () => void
}

const defaultValues: PolicyFormValues = {
  agentId: '',
  name: '',
  sourcePath: '',
  intervalSeconds: 900,
  isEnabled: true,
}

export function PolicyFormDialog({
  open,
  agents,
  policy,
  onClose,
}: PolicyFormDialogProps) {
  const queryClient = useQueryClient()
  const form = useForm<PolicyFormValues>({
    resolver: zodResolver(policySchema),
    mode: 'onChange',
    defaultValues,
  })

  useEffect(() => {
    form.reset(
      policy
        ? {
            agentId: policy.agentId,
            name: policy.name,
            sourcePath: policy.sourcePath,
            intervalSeconds: policy.intervalSeconds,
            isEnabled: policy.isEnabled,
          }
        : {
            ...defaultValues,
            agentId: agents[0]?.id ?? '',
          },
    )
  }, [agents, form, policy])

  const mutation = useMutation({
    mutationFn: (values: PolicyFormValues) =>
      policy
        ? updatePolicy(policy.id, values)
        : createPolicy(values),
    onSuccess: () => {
      toast.success(policy ? 'Policy updated' : 'Policy created')
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Policy save failed')
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={policy ? 'Edit backup policy' : 'Create backup policy'}
      description="Policies define source path, execution cadence and whether the agent should keep scheduling runs."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={!form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {policy ? 'Save changes' : 'Create policy'}
          </Button>
        </>
      }
    >
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <label className="text-sm font-medium text-ink-900" htmlFor="policy-agent">
            Agent
          </label>
          <Select id="policy-agent" {...form.register('agentId')}>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} ({agent.machineName})
              </option>
            ))}
          </Select>
          {form.formState.errors.agentId ? (
            <p className="text-sm text-danger-500">{form.formState.errors.agentId.message}</p>
          ) : null}
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium text-ink-900" htmlFor="policy-interval">
            Interval (seconds)
          </label>
          <Input
            id="policy-interval"
            type="number"
            min={60}
            step={60}
            {...form.register('intervalSeconds', {
              setValueAs: (value) => Number(value),
            })}
          />
          {form.formState.errors.intervalSeconds ? (
            <p className="text-sm text-danger-500">
              {form.formState.errors.intervalSeconds.message}
            </p>
          ) : null}
        </div>

        <div className="space-y-2 md:col-span-2">
          <label className="text-sm font-medium text-ink-900" htmlFor="policy-name">
            Policy name
          </label>
          <Input id="policy-name" placeholder="Documents every 15 minutes" {...form.register('name')} />
          {form.formState.errors.name ? (
            <p className="text-sm text-danger-500">{form.formState.errors.name.message}</p>
          ) : null}
        </div>

        <div className="space-y-2 md:col-span-2">
          <label className="text-sm font-medium text-ink-900" htmlFor="policy-source-path">
            Source path
          </label>
          <Input id="policy-source-path" placeholder="C:\\Users\\Backup" {...form.register('sourcePath')} />
          {form.formState.errors.sourcePath ? (
            <p className="text-sm text-danger-500">{form.formState.errors.sourcePath.message}</p>
          ) : null}
        </div>
      </div>

      <label className="flex items-center gap-3 rounded-2xl border border-surface-200 bg-white px-4 py-3">
        <input type="checkbox" className="h-4 w-4 accent-sky-600" {...form.register('isEnabled')} />
        <span className="text-sm text-ink-900">Enable scheduling immediately</span>
      </label>
    </Dialog>
  )
}
