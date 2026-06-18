import { useEffect, useRef } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm, useWatch } from 'react-hook-form'
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
import { Switch } from '@/shared/ui/Switch'
import { useI18n } from '@/shared/i18n'

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
  retentionDays: z.number().int().min(1).max(3650).nullable(),
  retentionMaxCount: z.number().int().min(1).max(10000).nullable(),
  retentionMaxSizeGb: z.number().positive().nullable(),
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
  retentionDays: null,
  retentionMaxCount: null,
  retentionMaxSizeGb: null,
}

const BYTES_PER_GB = 1024 ** 3

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
    retentionDays: policy.retentionDays ?? null,
    retentionMaxCount: policy.retentionMaxCount ?? null,
    retentionMaxSizeGb:
      policy.retentionMaxTotalBytes != null ? policy.retentionMaxTotalBytes / BYTES_PER_GB : null,
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
    retentionDays: values.retentionDays,
    retentionMaxCount: values.retentionMaxCount,
    retentionMaxTotalBytes:
      values.retentionMaxSizeGb != null ? Math.round(values.retentionMaxSizeGb * BYTES_PER_GB) : null,
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
  const { t } = useI18n()
  const formError = (message?: string) => (message ? t(message) : undefined)
  const queryClient = useQueryClient()
  const previousOpenRef = useRef(false)
  const previousPolicyIdRef = useRef<string | null>(null)
  const form = useForm<PolicyFormValues>({
    resolver: zodResolver(policySchema),
    mode: 'onChange',
    defaultValues,
  })
  const policyType = useWatch({ control: form.control, name: 'type' })
  const authMode = useWatch({ control: form.control, name: 'authMode' })

  useEffect(() => {
    if (!open) {
      previousOpenRef.current = false
      previousPolicyIdRef.current = policy?.id ?? null
      return
    }

    const currentPolicyId = policy?.id ?? null
    const shouldReset = !previousOpenRef.current || previousPolicyIdRef.current !== currentPolicyId

    if (shouldReset) {
      form.reset(toFormValues(policy, agents))
    }

    previousOpenRef.current = true
    previousPolicyIdRef.current = currentPolicyId
  }, [agents, form, open, policy])

  useEffect(() => {
    if (!open || policy || form.getValues('agentId') || form.formState.isDirty) {
      return
    }

    const firstAgentId = agents[0]?.id

    if (firstAgentId) {
      form.setValue('agentId', firstAgentId, { shouldValidate: true })
    }
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
      toast.success(policy ? t('Policy updated') : t('Policy created'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Policy save failed'))
    },
  })

  const canCreate = agents.length > 0

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={policy ? t('Edit backup policy') : t('Create backup policy')}
      description={t('Choose what should be protected, how often it runs, and which agent owns the work.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button
            disabled={!canCreate || !form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {mutation.isPending ? t('Saving...') : policy ? t('Save changes') : t('Create policy')}
          </Button>
        </>
      }
    >
      {!canCreate ? (
        <div className="rounded-lg border border-border bg-secondary p-4 text-sm text-muted-foreground">
          {t('Approve at least one agent before creating backup policies.')}
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2">
        <Field label={t('Agent')} error={formError(form.formState.errors.agentId?.message)}>
          <Select {...form.register('agentId')}>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} {agent.machineName ? `(${agent.machineName})` : ''}
              </option>
            ))}
          </Select>
        </Field>

        <Field label={t('Policy type')}>
          <Select {...form.register('type')}>
            <option value="filesystem">{t('Filesystem backup')}</option>
            <option value="postgres">{t('PostgreSQL dump')}</option>
            <option value="mysql">{t('MySQL dump')}</option>
          </Select>
        </Field>

        <Field label={t('Name')} error={formError(form.formState.errors.name?.message)} className="md:col-span-2">
          <Input placeholder={t('Documents every 15 minutes')} {...form.register('name')} />
        </Field>

        <Field
          label={t('Run every')}
          error={formError(form.formState.errors.intervalValue?.message ?? form.formState.errors.intervalUnit?.message)}
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
              <option value="minutes">{t('Minutes')}</option>
              <option value="hours">{t('Hours')}</option>
              <option value="days">{t('Days')}</option>
            </Select>
          </div>
        </Field>

        <Field label={t('Keep backups for (days)')}>
          <Input
            type="number"
            min={1}
            max={3650}
            step={1}
            placeholder={t('Unlimited')}
            {...form.register('retentionDays', {
              setValueAs: (value) => (value === '' || value === null ? null : Number(value)),
            })}
          />
        </Field>

        <Field label={t('Keep last N backups')} error={formError(form.formState.errors.retentionMaxCount?.message)}>
          <Input
            type="number"
            min={1}
            max={10000}
            step={1}
            placeholder={t('Unlimited')}
            {...form.register('retentionMaxCount', {
              setValueAs: (value) => (value === '' || value === null ? null : Number(value)),
            })}
          />
        </Field>

        <Field label={t('Max total size (GB)')} error={formError(form.formState.errors.retentionMaxSizeGb?.message)}>
          <Input
            type="number"
            min={0}
            step="any"
            placeholder={t('Unlimited')}
            {...form.register('retentionMaxSizeGb', {
              setValueAs: (value) => (value === '' || value === null ? null : Number(value)),
            })}
          />
        </Field>

        <Controller
          name="isEnabled"
          control={form.control}
          render={({ field }) => (
            <label
              htmlFor="policy-enabled"
              className="flex cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background/70 px-4 py-3 text-sm text-muted-foreground"
            >
              <span>{t('Enable scheduling immediately')}</span>
              <Switch
                id="policy-enabled"
                checked={field.value}
                onCheckedChange={field.onChange}
              />
            </label>
          )}
        />

        {policyType === 'filesystem' ? (
          <Field label={t('Source path')} error={formError(form.formState.errors.sourcePath?.message)} className="md:col-span-2">
            <Input placeholder="C:\\Users\\Backup" {...form.register('sourcePath')} />
          </Field>
        ) : (
          <>
            <Field label={t('Database name')} error={formError(form.formState.errors.databaseName?.message)}>
              <Input placeholder="restoreme_db" {...form.register('databaseName')} />
            </Field>
            <Field label={t('Host')} error={formError(form.formState.errors.host?.message)}>
              <Input placeholder="localhost" {...form.register('host')} />
            </Field>
            <Field label={t('Port')}>
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
            <Field label={t('Auth mode')}>
              <Select {...form.register('authMode')} disabled={policyType === 'mysql'}>
                <option value="integrated">{t('Integrated / local')}</option>
                <option value="credentials">{t('Username + password')}</option>
              </Select>
            </Field>

            {authMode === 'credentials' ? (
              <>
                <Field label={t('Username')} error={formError(form.formState.errors.username?.message)}>
                  <Input placeholder="backup_user" {...form.register('username')} />
                </Field>
                <Field label={t('Password')} error={formError(form.formState.errors.password?.message)}>
                  <Input type="password" placeholder={t('Database password')} {...form.register('password')} />
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
