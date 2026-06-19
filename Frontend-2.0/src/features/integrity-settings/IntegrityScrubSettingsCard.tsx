import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'

import {
  getIntegritySettings,
  updateIntegritySettings,
  type IntegrityScrubSettings,
} from '@/shared/api/integritySettings'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'
import { useI18n } from '@/shared/i18n'

const QUERY_KEY = ['integrity-settings'] as const

function minutesToHHMM(m: number): string {
  const h = Math.floor(m / 60)
    .toString()
    .padStart(2, '0')
  const mm = (m % 60).toString().padStart(2, '0')
  return `${h}:${mm}`
}

function hhmmToMinutes(value: string): number {
  const [h, m] = value.split(':').map((n) => parseInt(n, 10))
  return (Number.isFinite(h) ? h : 0) * 60 + (Number.isFinite(m) ? m : 0)
}

export function IntegrityScrubSettingsCard() {
  const { t } = useI18n()
  const settingsQuery = useQuery({ queryKey: QUERY_KEY, queryFn: getIntegritySettings })

  if (settingsQuery.isLoading || !settingsQuery.data) {
    return (
      <Card>
        <CardContent className="p-5 text-sm text-muted-foreground">
          {t('Integrity scrub schedule')}…
        </CardContent>
      </Card>
    )
  }

  // Remount the form (fresh initial state) whenever the persisted values change.
  const s = settingsQuery.data
  return (
    <SettingsForm
      key={`${s.isEnabled}-${s.intervalDays}-${s.runAtMinutesUtc}-${s.batchSize}`}
      settings={s}
    />
  )
}

function SettingsForm({ settings }: { settings: IntegrityScrubSettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const [isEnabled, setIsEnabled] = useState(settings.isEnabled)
  const [intervalDays, setIntervalDays] = useState(settings.intervalDays)
  const [time, setTime] = useState(minutesToHHMM(settings.runAtMinutesUtc))
  const [batchSize, setBatchSize] = useState(settings.batchSize)

  const saveMutation = useMutation({
    mutationFn: () =>
      updateIntegritySettings({
        isEnabled,
        intervalDays,
        runAtMinutesUtc: hhmmToMinutes(time),
        batchSize,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEY })
      toast.success(t('Integrity schedule saved'))
    },
    onError: (error) =>
      toast.error(error instanceof Error ? error.message : t('Could not save settings')),
  })

  return (
    <Card>
      <CardContent className="space-y-4 p-5">
        <div className="flex items-start gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
            <ShieldCheck className="h-5 w-5" />
          </span>
          <div>
            <h3 className="text-base font-semibold tracking-tight text-foreground">
              {t('Integrity scrub schedule')}
            </h3>
            <p className="text-sm text-muted-foreground">
              {t('Periodically re-hash stored backups to detect silent corruption.')}
            </p>
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm text-foreground">
          <input
            type="checkbox"
            checked={isEnabled}
            onChange={(e) => setIsEnabled(e.target.checked)}
          />
          {t('Enabled')}
        </label>

        <div className="grid gap-3 sm:grid-cols-3">
          <label className="space-y-1 text-sm text-muted-foreground">
            {t('Every (days)')}
            <Input
              type="number"
              min={1}
              value={intervalDays}
              onChange={(e) => setIntervalDays(Math.max(1, Number(e.target.value)))}
            />
          </label>
          <label className="space-y-1 text-sm text-muted-foreground">
            {t('At time (UTC)')}
            <Input type="time" value={time} onChange={(e) => setTime(e.target.value)} />
          </label>
          <label className="space-y-1 text-sm text-muted-foreground">
            {t('Batch size')}
            <Input
              type="number"
              min={1}
              value={batchSize}
              onChange={(e) => setBatchSize(Math.max(1, Number(e.target.value)))}
            />
          </label>
        </div>

        {settings.nextRunAt ? (
          <p className="text-xs text-muted-foreground">
            {t('Next run')}: {new Date(settings.nextRunAt).toLocaleString()}
          </p>
        ) : null}

        <Button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}>
          {saveMutation.isPending ? t('Saving...') : t('Save')}
        </Button>
      </CardContent>
    </Card>
  )
}
