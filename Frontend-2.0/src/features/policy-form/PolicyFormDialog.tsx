import { useEffect, useMemo, useRef } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { format } from 'date-fns'

import type { Agent } from '@/shared/api/agents'
import {
  createPolicy,
  previewSchedule,
  updatePolicy,
  type BackupPolicy,
  type SchedulePreviewInput,
  type UpsertPolicyInput,
} from '@/shared/api/policies'
import { queryKeys } from '@/shared/lib/query'
import { useDebouncedValue } from '@/shared/lib/useDebouncedValue'
import {
  buildCronExpression,
  minutesToTime,
  parseCronPreset,
  timeToMinutes,
} from '@/shared/lib/schedule'
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
  scheduleKind: z.enum(['interval', 'cron']),
  cronPreset: z.enum(['daily', 'weekly', 'monthly', 'custom']),
  cronTime: z.string(),
  cronWeekday: z.number().int().min(0).max(6),
  cronDayOfMonth: z.number().int().min(1).max(31),
  cronExpression: z.string(),
  timeZoneId: z.string(),
  windowEnabled: z.boolean(),
  windowStart: z.string(),
  windowEnd: z.string(),
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

  if (values.scheduleKind === 'cron') {
    if (values.timeZoneId.trim().length < 1) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: 'Timezone is required', path: ['timeZoneId'] })
    }
    if (values.cronPreset === 'custom' && values.cronExpression.trim().split(/\s+/).length !== 5) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: 'Cron expression must have 5 fields', path: ['cronExpression'] })
    }
    if (values.cronPreset !== 'custom' && !/^\d{2}:\d{2}$/.test(values.cronTime)) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: 'Run time is required', path: ['cronTime'] })
    }
  }

  if (values.scheduleKind === 'interval' && values.windowEnabled) {
    if (!/^\d{2}:\d{2}$/.test(values.windowStart) || !/^\d{2}:\d{2}$/.test(values.windowEnd)) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: 'Window start and end are required', path: ['windowStart'] })
    } else if (values.windowStart === values.windowEnd) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: 'Window start and end must differ', path: ['windowEnd'] })
    }
    if (values.timeZoneId.trim().length < 1) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: 'Timezone is required', path: ['timeZoneId'] })
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
  scheduleKind: 'interval',
  cronPreset: 'daily',
  cronTime: '03:00',
  cronWeekday: 1,
  cronDayOfMonth: 1,
  cronExpression: '',
  timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'Etc/UTC',
  windowEnabled: false,
  windowStart: '22:00',
  windowEnd: '06:00',
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
    scheduleKind: policy.scheduleKind,
    ...(() => {
      if (policy.scheduleKind !== 'cron' || !policy.cronExpression) {
        return { cronPreset: 'daily' as const, cronTime: '03:00', cronWeekday: 1, cronDayOfMonth: 1, cronExpression: '' }
      }
      const parsed = parseCronPreset(policy.cronExpression)
      if (!parsed) {
        return { cronPreset: 'custom' as const, cronTime: '03:00', cronWeekday: 1, cronDayOfMonth: 1, cronExpression: policy.cronExpression }
      }
      return {
        cronPreset: parsed.preset,
        cronTime: parsed.time,
        cronWeekday: parsed.weekday ?? 1,
        cronDayOfMonth: parsed.dayOfMonth ?? 1,
        cronExpression: policy.cronExpression,
      }
    })(),
    timeZoneId: policy.timeZoneId ?? (Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'Etc/UTC'),
    windowEnabled: policy.windowStartMinutes != null,
    windowStart: policy.windowStartMinutes != null ? minutesToTime(policy.windowStartMinutes) : '22:00',
    windowEnd: policy.windowEndMinutes != null ? minutesToTime(policy.windowEndMinutes) : '06:00',
  }
}

function toPayload(values: PolicyFormValues): UpsertPolicyInput {
  const isFilesystem = values.type === 'filesystem'

  return {
    agentId: values.agentId,
    type: values.type,
    name: values.name.trim(),
    sourcePath: isFilesystem ? values.sourcePath.trim() : '',
    isEnabled: values.isEnabled,
    retentionDays: values.retentionDays,
    retentionMaxCount: values.retentionMaxCount,
    retentionMaxTotalBytes:
      values.retentionMaxSizeGb != null ? Math.round(values.retentionMaxSizeGb * BYTES_PER_GB) : null,
    scheduleKind: values.scheduleKind,
    intervalSeconds: values.scheduleKind === 'interval' ? intervalToSeconds(values) : 0,
    cronExpression:
      values.scheduleKind === 'cron'
        ? values.cronPreset === 'custom'
          ? values.cronExpression.trim()
          : buildCronExpression(
              values.cronPreset === 'weekly'
                ? { kind: 'weekly', weekday: values.cronWeekday }
                : values.cronPreset === 'monthly'
                  ? { kind: 'monthly', dayOfMonth: values.cronDayOfMonth }
                  : { kind: 'daily' },
              values.cronTime,
            )
        : null,
    timeZoneId:
      values.scheduleKind === 'cron' || (values.scheduleKind === 'interval' && values.windowEnabled)
        ? values.timeZoneId
        : null,
    windowStartMinutes:
      values.scheduleKind === 'interval' && values.windowEnabled ? timeToMinutes(values.windowStart) : null,
    windowEndMinutes:
      values.scheduleKind === 'interval' && values.windowEnabled ? timeToMinutes(values.windowEnd) : null,
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
  const { t, language } = useI18n()
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
  const [
    scheduleKind,
    intervalValue,
    intervalUnit,
    cronPreset,
    cronTime,
    cronWeekday,
    cronDayOfMonth,
    cronExpression,
    timeZoneId,
    windowEnabled,
    windowStart,
    windowEnd,
  ] = useWatch({
    control: form.control,
    name: [
      'scheduleKind',
      'intervalValue',
      'intervalUnit',
      'cronPreset',
      'cronTime',
      'cronWeekday',
      'cronDayOfMonth',
      'cronExpression',
      'timeZoneId',
      'windowEnabled',
      'windowStart',
      'windowEnd',
    ],
  })

  const timeZoneOptions = useMemo<string[]>(() => {
    const intlWithSupportedValues = Intl as typeof Intl & {
      supportedValuesOf?: (key: string) => string[]
    }
    return (
      intlWithSupportedValues.supportedValuesOf?.('timeZone') ?? [
        Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'Etc/UTC',
      ]
    )
  }, [])

  const weekdayOptions = useMemo(() => {
    const formatter = new Intl.DateTimeFormat(language === 'ru' ? 'ru' : 'en-US', { weekday: 'long' })
    return Array.from({ length: 7 }, (_, index) => ({
      value: index,
      label: formatter.format(new Date(Date.UTC(2023, 0, 1 + index))),
    }))
  }, [language])

  const schedulePreviewInput = useMemo<SchedulePreviewInput | null>(() => {
    const isTime = (value: string) => /^\d{2}:\d{2}$/.test(value)

    if (scheduleKind === 'cron') {
      if (timeZoneId.trim().length < 1) return null
      // A half-typed time ("3", "03:") would build an expression like
      // "NaN 3 * * *" — skip the request until it's a real HH:MM.
      if (cronPreset !== 'custom' && !isTime(cronTime)) return null
      const expression =
        cronPreset === 'custom'
          ? cronExpression.trim()
          : buildCronExpression(
              cronPreset === 'weekly'
                ? { kind: 'weekly', weekday: cronWeekday }
                : cronPreset === 'monthly'
                  ? { kind: 'monthly', dayOfMonth: cronDayOfMonth }
                  : { kind: 'daily' },
              cronTime,
            )
      if (expression.split(/\s+/).length !== 5) return null

      return {
        scheduleKind: 'cron',
        intervalSeconds: 0,
        cronExpression: expression,
        timeZoneId,
        windowStartMinutes: null,
        windowEndMinutes: null,
      }
    }

    if (windowEnabled && (!isTime(windowStart) || !isTime(windowEnd))) return null

    return {
      scheduleKind: 'interval',
      intervalSeconds: intervalToSeconds({ intervalValue, intervalUnit }),
      cronExpression: null,
      timeZoneId: windowEnabled ? timeZoneId : null,
      windowStartMinutes: windowEnabled ? timeToMinutes(windowStart) : null,
      windowEndMinutes: windowEnabled ? timeToMinutes(windowEnd) : null,
    }
  }, [
    scheduleKind,
    intervalValue,
    intervalUnit,
    cronPreset,
    cronTime,
    cronWeekday,
    cronDayOfMonth,
    cronExpression,
    timeZoneId,
    windowEnabled,
    windowStart,
    windowEnd,
  ])

  const debouncedPreviewInput = useDebouncedValue(schedulePreviewInput, 400)

  const schedulePreviewQuery = useQuery({
    queryKey: ['policy-schedule-preview', debouncedPreviewInput],
    queryFn: () => previewSchedule(debouncedPreviewInput!),
    enabled: open && debouncedPreviewInput != null,
    retry: false,
    staleTime: 5_000,
  })

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

        <div className="space-y-4 rounded-lg border border-border bg-background/70 p-4 md:col-span-2">
          <Field label={t('Schedule')}>
            <Select {...form.register('scheduleKind')}>
              <option value="interval">{t('Fixed interval')}</option>
              <option value="cron">{t('Cron schedule')}</option>
            </Select>
          </Field>

          {scheduleKind === 'interval' ? (
            <>
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

              <Controller
                name="windowEnabled"
                control={form.control}
                render={({ field }) => (
                  <label
                    htmlFor="policy-window-enabled"
                    className="flex cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background/70 px-4 py-3 text-sm text-muted-foreground"
                  >
                    <span>{t('Only run within a time window')}</span>
                    <Switch
                      id="policy-window-enabled"
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </label>
                )}
              />

              {windowEnabled ? (
                <div className="grid gap-4 sm:grid-cols-3">
                  <Field label={t('Window start')} error={formError(form.formState.errors.windowStart?.message)}>
                    <Input type="time" {...form.register('windowStart')} />
                  </Field>
                  <Field label={t('Window end')} error={formError(form.formState.errors.windowEnd?.message)}>
                    <Input type="time" {...form.register('windowEnd')} />
                  </Field>
                  <Field label={t('Timezone')} error={formError(form.formState.errors.timeZoneId?.message)}>
                    <Select {...form.register('timeZoneId')}>
                      {timeZoneOptions.map((tz) => (
                        <option key={tz} value={tz}>
                          {tz}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>
              ) : null}
            </>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t('Cron schedule')}>
                <Select {...form.register('cronPreset')}>
                  <option value="daily">{t('Daily')}</option>
                  <option value="weekly">{t('Weekly')}</option>
                  <option value="monthly">{t('Monthly')}</option>
                  <option value="custom">{t('Custom cron')}</option>
                </Select>
              </Field>

              {cronPreset !== 'custom' ? (
                <Field label={t('Run at')} error={formError(form.formState.errors.cronTime?.message)}>
                  <Input type="time" {...form.register('cronTime')} />
                </Field>
              ) : null}

              {cronPreset === 'weekly' ? (
                <Field label={t('Day of week')}>
                  <Select
                    {...form.register('cronWeekday', {
                      setValueAs: (value) => Number(value || 0),
                    })}
                  >
                    {weekdayOptions.map((weekday) => (
                      <option key={weekday.value} value={weekday.value}>
                        {weekday.label}
                      </option>
                    ))}
                  </Select>
                </Field>
              ) : null}

              {cronPreset === 'monthly' ? (
                <Field label={t('Day of month')}>
                  <Input
                    type="number"
                    min={1}
                    max={31}
                    step={1}
                    {...form.register('cronDayOfMonth', {
                      setValueAs: (value) => Number(value || 1),
                    })}
                  />
                </Field>
              ) : null}

              {cronPreset === 'custom' ? (
                <Field
                  label={t('Cron expression')}
                  error={formError(form.formState.errors.cronExpression?.message)}
                  className="sm:col-span-2"
                >
                  <Input placeholder="0 3 * * *" {...form.register('cronExpression')} />
                </Field>
              ) : null}

              <Field label={t('Timezone')} error={formError(form.formState.errors.timeZoneId?.message)}>
                <Select {...form.register('timeZoneId')}>
                  {timeZoneOptions.map((tz) => (
                    <option key={tz} value={tz}>
                      {tz}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
          )}

          {schedulePreviewQuery.data && schedulePreviewQuery.data.length > 0 ? (
            <p className="text-sm text-muted-foreground">
              {t('Next:')} {schedulePreviewQuery.data.map((iso) => format(new Date(iso), 'dd MMM HH:mm')).join(' · ')}
            </p>
          ) : null}
        </div>

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
