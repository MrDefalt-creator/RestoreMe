import { useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import type { Agent } from '@/shared/api/agents'
import {
  createPolicy,
  updatePolicy,
  type BackupPolicy,
  type UpsertPolicyInput,
} from '@/shared/api/policies'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'

const policySchema = z.object({
  agentId: z.string().min(1, 'Select an agent'),
  type: z.enum(['filesystem', 'postgres', 'mysql']),
  name: z.string().trim().min(3, 'Name is required').max(100, 'Name is too long'),
  sourcePath: z.string(),
  intervalValue: z.number().int().min(1, 'Interval must be at least 1'),
  intervalUnit: z.enum(['minutes', 'hours', 'days']),
  isEnabled: z.boolean(),
  databaseName: z.string(),
  host: z.string(),
  port: z.number().int().min(0).nullable(),
  authMode: z.enum(['integrated', 'credentials']),
  username: z.string(),
  password: z.string(),
}).superRefine((values, context) => {
  if (values.type === 'filesystem' && values.sourcePath.trim().length < 3) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Source path is required',
      path: ['sourcePath'],
    })
  }

  if (values.type !== 'filesystem' && values.databaseName.trim().length < 1) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Database name is required',
      path: ['databaseName'],
    })
  }

  if (values.type === 'mysql' && values.host.trim().length < 1) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Host is required for MySQL dumps',
      path: ['host'],
    })
  }

  if (values.type !== 'filesystem' && values.authMode === 'credentials') {
    if (values.username.trim().length < 1) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Username is required',
        path: ['username'],
      })
    }

    if (values.password.trim().length < 1) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Password is required',
        path: ['password'],
      })
    }
  }
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
  type: 'filesystem',
  name: '',
  sourcePath: '',
  intervalValue: 15,
  intervalUnit: 'minutes',
  isEnabled: true,
  databaseName: '',
  host: 'localhost',
  port: 5432,
  authMode: 'integrated',
  username: '',
  password: '',
}

function secondsToInterval(intervalSeconds: number): Pick<PolicyFormValues, 'intervalValue' | 'intervalUnit'> {
  if (intervalSeconds >= 86_400 && intervalSeconds % 86_400 === 0) {
    return { intervalValue: intervalSeconds / 86_400, intervalUnit: 'days' }
  }

  if (intervalSeconds >= 3_600 && intervalSeconds % 3_600 === 0) {
    return { intervalValue: intervalSeconds / 3_600, intervalUnit: 'hours' }
  }

  return { intervalValue: Math.max(1, Math.round(intervalSeconds / 60)), intervalUnit: 'minutes' }
}

function intervalToSeconds(values: Pick<PolicyFormValues, 'intervalValue' | 'intervalUnit'>): number {
  switch (values.intervalUnit) {
    case 'days':
      return values.intervalValue * 86_400
    case 'hours':
      return values.intervalValue * 3_600
    default:
      return values.intervalValue * 60
  }
}

function toFormValues(policy: BackupPolicy | null, agents: Agent[]): PolicyFormValues {
  if (!policy) {
    return {
      ...defaultValues,
      agentId: agents[0]?.id ?? '',
    }
  }

  return {
    agentId: policy.agentId,
    type: policy.type,
    name: policy.name,
    sourcePath: policy.sourcePath ?? '',
    ...secondsToInterval(policy.intervalSeconds),
    isEnabled: policy.isEnabled,
    databaseName: policy.databaseSettings?.databaseName ?? '',
    host: policy.databaseSettings?.host ?? 'localhost',
    port: policy.databaseSettings?.port ?? (policy.type === 'mysql' ? 3306 : 5432),
    authMode: policy.databaseSettings?.authMode ?? (policy.type === 'mysql' ? 'credentials' : 'integrated'),
    username: policy.databaseSettings?.username ?? '',
    password: policy.databaseSettings?.password ?? '',
  }
}

function toPayload(values: PolicyFormValues): UpsertPolicyInput {
  const isFilesystem = values.type === 'filesystem'

  return {
    agentId: values.agentId,
    type: values.type,
    name: values.name.trim(),
    sourcePath: isFilesystem ? values.sourcePath.trim() : '',
    intervalSeconds: intervalToSeconds(values),
    isEnabled: values.isEnabled,
    databaseSettings: isFilesystem
      ? null
      : {
          engine: values.type === 'mysql' ? 'mysql' : 'postgres',
          authMode: values.type === 'mysql' ? 'credentials' : values.authMode,
          host: values.host.trim() || null,
          port: values.port,
          databaseName: values.databaseName.trim(),
          username: values.username.trim() || null,
          password: values.password.trim() || null,
        },
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
  const policyType = form.watch('type')
  const authMode = form.watch('authMode')

  useEffect(() => {
    if (!open) {
      return
    }

    form.reset(toFormValues(policy, agents))
  }, [agents, form, open, policy])

  useEffect(() => {
    if (policyType === 'mysql') {
      form.setValue('authMode', 'credentials', { shouldValidate: true })
      if (!form.getValues('port')) {
        form.setValue('port', 3306, { shouldValidate: true })
      }
    }

    if (policyType === 'postgres' && !form.getValues('port')) {
      form.setValue('port', 5432, { shouldValidate: true })
    }
  }, [form, policyType])

  const mutation = useMutation({
    mutationFn: (values: PolicyFormValues) =>
      policy
        ? updatePolicy(policy.id, toPayload(values))
        : createPolicy(toPayload(values)),
    onSuccess: () => {
      toast.success(policy ? 'Policy updated' : 'Policy created')
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Policy save failed')
    },
  })

  const canCreate = agents.length > 0

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={policy ? 'Edit backup policy' : 'Create backup policy'}
      description="Choose what should be protected, how often it runs, and which agent owns the work."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={!canCreate || !form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {mutation.isPending ? 'Saving...' : policy ? 'Save changes' : 'Create policy'}
          </Button>
        </>
      }
    >
      {!canCreate ? (
        <div className="rounded-lg border border-border bg-secondary p-4 text-sm text-muted-foreground">
          Approve at least one agent before creating backup policies.
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Agent" error={form.formState.errors.agentId?.message}>
          <Select {...form.register('agentId')}>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} {agent.machineName ? `(${agent.machineName})` : ''}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Policy type">
          <Select {...form.register('type')}>
            <option value="filesystem">Filesystem backup</option>
            <option value="postgres">PostgreSQL dump</option>
            <option value="mysql">MySQL dump</option>
          </Select>
        </Field>

        <Field label="Name" error={form.formState.errors.name?.message} className="md:col-span-2">
          <Input placeholder="Documents every 15 minutes" {...form.register('name')} />
        </Field>

        <Field
          label="Run every"
          error={form.formState.errors.intervalValue?.message ?? form.formState.errors.intervalUnit?.message}
        >
          <div className="grid grid-cols-[1fr_150px] gap-3">
            <Input
              type="number"
              min={1}
              step={1}
              {...form.register('intervalValue', {
                setValueAs: (value) => Number(value || 0),
              })}
            />
            <Select {...form.register('intervalUnit')}>
              <option value="minutes">Minutes</option>
              <option value="hours">Hours</option>
              <option value="days">Days</option>
            </Select>
          </div>
        </Field>

        <label className="flex items-center gap-3 rounded-lg border border-border bg-background/70 px-4 py-3 text-sm text-muted-foreground">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-border accent-[hsl(var(--primary))]"
            {...form.register('isEnabled')}
          />
          <span>Enable scheduling immediately</span>
        </label>

        {policyType === 'filesystem' ? (
          <Field label="Source path" error={form.formState.errors.sourcePath?.message} className="md:col-span-2">
            <Input placeholder="C:\\Users\\Backup" {...form.register('sourcePath')} />
          </Field>
        ) : (
          <>
            <Field label="Database name" error={form.formState.errors.databaseName?.message}>
              <Input placeholder="restoreme_db" {...form.register('databaseName')} />
            </Field>
            <Field label="Host" error={form.formState.errors.host?.message}>
              <Input placeholder="localhost" {...form.register('host')} />
            </Field>
            <Field label="Port">
              <Input
                type="number"
                min={0}
                step={1}
                placeholder={policyType === 'mysql' ? '3306' : '5432'}
                {...form.register('port', {
                  setValueAs: (value) => (value === '' ? null : Number(value)),
                })}
              />
            </Field>
            <Field label="Auth mode">
              <Select {...form.register('authMode')} disabled={policyType === 'mysql'}>
                <option value="integrated">Integrated / local</option>
                <option value="credentials">Username + password</option>
              </Select>
            </Field>

            {authMode === 'credentials' ? (
              <>
                <Field label="Username" error={form.formState.errors.username?.message}>
                  <Input placeholder="backup_user" {...form.register('username')} />
                </Field>
                <Field label="Password" error={form.formState.errors.password?.message}>
                  <Input type="password" placeholder="Database password" {...form.register('password')} />
                </Field>
              </>
            ) : null}
          </>
        )}
      </div>
    </Dialog>
  )
}

function Field({
  label,
  error,
  className,
  children,
}: {
  label: string
  error?: string
  className?: string
  children: React.ReactNode
}) {
  return (
    <div className={className}>
      <label className="mb-2 block text-sm font-medium text-foreground">{label}</label>
      {children}
      {error ? <p className="mt-2 text-sm text-destructive">{error}</p> : null}
    </div>
  )
}
