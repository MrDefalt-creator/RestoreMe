import { useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import type { Agent } from '@/entities/agent/model/types'
import { createPolicy, updatePolicy } from '@/entities/policy/api'
import type { BackupPolicy } from '@/entities/policy/model/types'
import type { UpsertPolicyInput } from '@/entities/policy/model/types'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'

const maxHours = 23
const maxMinutes = 59
const maxSeconds = 59

function intervalPartsToSeconds(parts: IntervalParts) {
  return (
    parts.days * 86_400 +
    parts.hours * 3_600 +
    parts.minutes * 60 +
    parts.seconds
  )
}

function secondsToIntervalParts(totalSeconds: number): IntervalParts {
  const safeSeconds = Math.max(0, Math.floor(totalSeconds))

  return {
    days: Math.floor(safeSeconds / 86_400),
    hours: Math.floor((safeSeconds % 86_400) / 3_600),
    minutes: Math.floor((safeSeconds % 3_600) / 60),
    seconds: safeSeconds % 60,
  }
}

const intervalSchema = z.object({
  days: z.number().int().min(0, 'Days must be zero or greater'),
  hours: z.number().int().min(0, 'Hours must be zero or greater').max(maxHours, 'Hours must be between 0 and 23'),
  minutes: z.number().int().min(0, 'Minutes must be zero or greater').max(maxMinutes, 'Minutes must be between 0 and 59'),
  seconds: z.number().int().min(0, 'Seconds must be zero or greater').max(maxSeconds, 'Seconds must be between 0 and 59'),
})

const policySchema = z.object({
  agentId: z.string().uuid('Select an agent'),
  name: z.string().trim().min(3, 'Name is required').max(100, 'Name is too long'),
  sourcePath: z.string().trim().min(3, 'Source path is required'),
  interval: intervalSchema,
  isEnabled: z.boolean(),
}).superRefine((values, context) => {
  if (intervalPartsToSeconds(values.interval) < 60) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Interval must be at least 60 seconds',
      path: ['interval', 'seconds'],
    })
  }
})

type PolicyFormValues = z.infer<typeof policySchema>
type IntervalParts = z.infer<typeof intervalSchema>

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
  interval: secondsToIntervalParts(900),
  isEnabled: true,
}

function toPolicyPayload(values: PolicyFormValues): UpsertPolicyInput {
  return {
    agentId: values.agentId,
    name: values.name,
    sourcePath: values.sourcePath,
    intervalSeconds: intervalPartsToSeconds(values.interval),
    isEnabled: values.isEnabled,
  }
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
            interval: secondsToIntervalParts(policy.intervalSeconds),
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
        ? updatePolicy(policy.id, toPolicyPayload(values))
        : createPolicy(toPolicyPayload(values)),
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
      <div className="grid gap-5 md:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
        <div className="space-y-2">
          <label className="text-sm font-medium text-ink-900" htmlFor="policy-agent">
            Agent
          </label>
          <Select
            id="policy-agent"
            className="truncate"
            {...form.register('agentId')}
          >
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
            Interval
          </label>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <div className="space-y-2">
              <Input
                id="policy-interval-days"
                type="number"
                min={0}
                step={1}
                placeholder="0"
                className="text-center tabular-nums"
                {...form.register('interval.days', {
                  setValueAs: (value) => Number(value || 0),
                })}
              />
              <p className="text-center text-xs text-ink-800/70">Days</p>
            </div>
            <div className="space-y-2">
              <Input
                id="policy-interval-hours"
                type="number"
                min={0}
                max={23}
                step={1}
                placeholder="0"
                className="text-center tabular-nums"
                {...form.register('interval.hours', {
                  setValueAs: (value) => Number(value || 0),
                })}
              />
              <p className="text-center text-xs text-ink-800/70">Hours</p>
            </div>
            <div className="space-y-2">
              <Input
                id="policy-interval-minutes"
                type="number"
                min={0}
                max={59}
                step={1}
                placeholder="15"
                className="text-center tabular-nums"
                {...form.register('interval.minutes', {
                  setValueAs: (value) => Number(value || 0),
                })}
              />
              <p className="text-center text-xs text-ink-800/70">Minutes</p>
            </div>
            <div className="space-y-2">
              <Input
                id="policy-interval-seconds"
                type="number"
                min={0}
                max={59}
                step={1}
                placeholder="0"
                className="text-center tabular-nums"
                {...form.register('interval.seconds', {
                  setValueAs: (value) => Number(value || 0),
                })}
              />
              <p className="text-center text-xs text-ink-800/70">Seconds</p>
            </div>
          </div>
          {form.formState.errors.interval ? (
            <p className="text-sm text-danger-500">
              {form.formState.errors.interval.days?.message ||
                form.formState.errors.interval.hours?.message ||
                form.formState.errors.interval.minutes?.message ||
                form.formState.errors.interval.seconds?.message}
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
