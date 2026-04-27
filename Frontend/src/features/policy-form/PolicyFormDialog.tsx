import { useEffect, useRef } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import type { Agent } from '@/entities/agent/model/types'
import { createPolicy, updatePolicy } from '@/entities/policy/api'
import type {
  BackupPolicy,
  BackupPolicyDatabaseSettings,
  PolicyType,
  UpsertPolicyInput,
} from '@/entities/policy/model/types'
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

const databaseSettingsSchema = z.object({
  engine: z.enum(['postgres', 'mysql']),
  authMode: z.enum(['integrated', 'credentials']),
  host: z.string(),
  port: z.number().int().min(0, 'Port must be zero or greater').nullable(),
  databaseName: z.string(),
  username: z.string(),
  password: z.string(),
})

const policySchema = z.object({
  agentId: z.string().uuid('Select an agent'),
  type: z.enum(['filesystem', 'postgres', 'mysql']),
  name: z.string().trim().min(3, 'Name is required').max(100, 'Name is too long'),
  sourcePath: z.string(),
  interval: intervalSchema,
  isEnabled: z.boolean(),
  databaseSettings: databaseSettingsSchema,
}).superRefine((values, context) => {
  if (intervalPartsToSeconds(values.interval) < 60) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Interval must be at least 60 seconds',
      path: ['interval', 'seconds'],
    })
  }

  if (values.type === 'filesystem') {
    if (values.sourcePath.trim().length < 3) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Source path is required',
        path: ['sourcePath'],
      })
    }

    return
  }

  if (values.databaseSettings.databaseName.trim().length < 1) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Database name is required',
      path: ['databaseSettings', 'databaseName'],
    })
  }

  if (values.type === 'mysql' && values.databaseSettings.host.trim().length < 1) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      message: 'Host is required for MySQL dumps',
      path: ['databaseSettings', 'host'],
    })
  }

  if (values.databaseSettings.authMode === 'credentials') {
    if (values.databaseSettings.username.trim().length < 1) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Username is required',
        path: ['databaseSettings', 'username'],
      })
    }

    if (values.databaseSettings.password.trim().length < 1) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Password is required',
        path: ['databaseSettings', 'password'],
      })
    }
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

function createDefaultDatabaseSettings(type: PolicyType) {
  return {
    engine: type === 'mysql' ? 'mysql' : 'postgres',
    authMode: type === 'mysql' ? 'credentials' : 'integrated',
    host: 'localhost',
    port: type === 'mysql' ? 3306 : 5432,
    databaseName: '',
    username: '',
    password: '',
  } as const
}

function toFormDatabaseSettings(settings: BackupPolicyDatabaseSettings | null, type: PolicyType) {
  if (!settings) {
    return createDefaultDatabaseSettings(type)
  }

  return {
    engine: settings.engine,
    authMode: settings.authMode,
    host: settings.host ?? '',
    port: settings.port,
    databaseName: settings.databaseName,
    username: settings.username ?? '',
    password: settings.password ?? '',
  } as const
}

const defaultValues: PolicyFormValues = {
  agentId: '',
  type: 'filesystem',
  name: '',
  sourcePath: '',
  interval: secondsToIntervalParts(900),
  isEnabled: true,
  databaseSettings: createDefaultDatabaseSettings('postgres'),
}

function toPolicyPayload(values: PolicyFormValues): UpsertPolicyInput {
  const isFilesystem = values.type === 'filesystem'

  return {
    agentId: values.agentId,
    type: values.type,
    name: values.name,
    sourcePath: isFilesystem ? values.sourcePath.trim() : '',
    intervalSeconds: intervalPartsToSeconds(values.interval),
    isEnabled: values.isEnabled,
    databaseSettings: isFilesystem
      ? null
      : {
          engine: values.type === 'mysql' ? 'mysql' : 'postgres',
          authMode: values.type === 'mysql' ? 'credentials' : values.databaseSettings.authMode,
          host: values.databaseSettings.host.trim() || null,
          port: values.databaseSettings.port,
          databaseName: values.databaseSettings.databaseName.trim(),
          username: values.databaseSettings.username.trim() || null,
          password: values.databaseSettings.password.trim() || null,
        },
  }
}

function getPolicyFormValues(policy: BackupPolicy): PolicyFormValues {
  return {
    agentId: policy.agentId,
    type: policy.type,
    name: policy.name,
    sourcePath: policy.sourcePath,
    interval: secondsToIntervalParts(policy.intervalSeconds),
    isEnabled: policy.isEnabled,
    databaseSettings: toFormDatabaseSettings(policy.databaseSettings, policy.type),
  }
}

export function PolicyFormDialog({
  open,
  agents,
  policy,
  onClose,
}: PolicyFormDialogProps) {
  const queryClient = useQueryClient()
  const previousOpenRef = useRef(false)
  const previousPolicyIdRef = useRef<string | null>(null)
  const form = useForm<PolicyFormValues>({
    resolver: zodResolver(policySchema),
    mode: 'onChange',
    defaultValues,
  })

  const policyType = form.watch('type')
  const authMode = form.watch('databaseSettings.authMode')

  useEffect(() => {
    if (!open) {
      previousOpenRef.current = false
      previousPolicyIdRef.current = policy?.id ?? null
      return
    }

    const currentPolicyId = policy?.id ?? null
    const shouldReset =
      !previousOpenRef.current || previousPolicyIdRef.current !== currentPolicyId

    if (shouldReset) {
      form.reset(
        policy
          ? getPolicyFormValues(policy)
          : {
              ...defaultValues,
              agentId: agents[0]?.id ?? '',
            },
      )
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
    if (policyType === 'filesystem') {
      return
    }

    form.setValue(
      'databaseSettings.engine',
      policyType === 'mysql' ? 'mysql' : 'postgres',
      { shouldValidate: true },
    )

    if (policyType === 'mysql') {
      form.setValue('databaseSettings.authMode', 'credentials', {
        shouldValidate: true,
      })

      if (!form.getValues('databaseSettings.port')) {
        form.setValue('databaseSettings.port', 3306, { shouldValidate: true })
      }
    }

    if (policyType === 'postgres' && !form.getValues('databaseSettings.port')) {
      form.setValue('databaseSettings.port', 5432, { shouldValidate: true })
    }
  }, [form, policyType])

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

  const sourcePathError = form.formState.errors.sourcePath?.message
  const databaseError =
    form.formState.errors.databaseSettings?.databaseName?.message ||
    form.formState.errors.databaseSettings?.host?.message ||
    form.formState.errors.databaseSettings?.username?.message ||
    form.formState.errors.databaseSettings?.password?.message ||
    form.formState.errors.databaseSettings?.port?.message

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={policy ? 'Edit backup policy' : 'Create backup policy'}
      description="Policies can protect file paths or create logical database dumps through the same scheduling flow."
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
          <Select id="policy-agent" className="truncate" {...form.register('agentId')}>
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
          <label className="text-sm font-medium text-ink-900" htmlFor="policy-type">
            Policy type
          </label>
          <Select id="policy-type" {...form.register('type')}>
            <option value="filesystem">Filesystem backup</option>
            <option value="postgres">PostgreSQL logical dump</option>
            <option value="mysql">MySQL logical dump</option>
          </Select>
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

        {policyType === 'filesystem' ? (
          <div className="space-y-2 md:col-span-2">
            <label className="text-sm font-medium text-ink-900" htmlFor="policy-source-path">
              Source path
            </label>
            <Input id="policy-source-path" placeholder="C:\\Users\\Backup" {...form.register('sourcePath')} />
            {sourcePathError ? (
              <p className="text-sm text-danger-500">{sourcePathError}</p>
            ) : null}
          </div>
        ) : (
          <>
            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="policy-db-name">
                Database name
              </label>
              <Input id="policy-db-name" placeholder="restoreme_db" {...form.register('databaseSettings.databaseName')} />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="policy-db-host">
                Host
              </label>
              <Input id="policy-db-host" placeholder="localhost" {...form.register('databaseSettings.host')} />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="policy-db-port">
                Port
              </label>
              <Input
                id="policy-db-port"
                type="number"
                min={0}
                step={1}
                placeholder={policyType === 'mysql' ? '3306' : '5432'}
                {...form.register('databaseSettings.port', {
                  setValueAs: (value) => (value === '' ? null : Number(value)),
                })}
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="policy-auth-mode">
                Auth mode
              </label>
              <Select id="policy-auth-mode" {...form.register('databaseSettings.authMode')} disabled={policyType === 'mysql'}>
                {policyType === 'postgres' ? (
                  <>
                    <option value="integrated">Integrated / local</option>
                    <option value="credentials">Username + password</option>
                  </>
                ) : (
                  <option value="credentials">Username + password</option>
                )}
              </Select>
            </div>

            <div className="space-y-2 md:col-span-2">
              <p className="rounded-2xl border border-surface-200 bg-surface-100 px-4 py-3 text-sm text-ink-800">
                {policyType === 'postgres'
                  ? 'Recommended: use integrated/local auth when pg_dump can access the database without storing a password in the policy. Credentials mode stays available as the universal fallback.'
                  : 'MySQL uses credentials mode in this first iteration. Make sure mysqldump is installed on the agent machine.'}
              </p>
            </div>

            {authMode === 'credentials' ? (
              <>
                <div className="space-y-2">
                  <label className="text-sm font-medium text-ink-900" htmlFor="policy-db-username">
                    Username
                  </label>
                  <Input id="policy-db-username" placeholder="backup_user" {...form.register('databaseSettings.username')} />
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium text-ink-900" htmlFor="policy-db-password">
                    Password
                  </label>
                  <Input id="policy-db-password" type="password" placeholder="Enter database password" {...form.register('databaseSettings.password')} />
                </div>
              </>
            ) : null}

            {databaseError ? (
              <div className="md:col-span-2">
                <p className="text-sm text-danger-500">{databaseError}</p>
              </div>
            ) : null}
          </>
        )}
      </div>

      <label className="flex items-center gap-3 rounded-2xl border border-surface-200 bg-white px-4 py-3">
        <input type="checkbox" className="h-4 w-4 accent-sky-600" {...form.register('isEnabled')} />
        <span className="text-sm text-ink-900">Enable scheduling immediately</span>
      </label>
    </Dialog>
  )
}
