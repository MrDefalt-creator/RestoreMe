import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { AlertTriangle, Check, RotateCcw } from 'lucide-react'

import { getAgents } from '@/shared/api/agents'
import { requestRestore, type Artifact } from '@/shared/api/artifacts'
import { queryKeys } from '@/shared/lib/query'
import { formatFileSize, formatRelativeTime } from '@/shared/lib/format'
import { useUiStore } from '@/app/store/ui-store'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'
import { Switch } from '@/shared/ui/Switch'
import { cn } from '@/shared/lib/cn'
import { useI18n } from '@/shared/i18n'

type Step = 'source' | 'target' | 'confirm'

const STEP_ORDER: Step[] = ['source', 'target', 'confirm']

interface Props {
  open: boolean
  artifact: Artifact | null
  onClose: () => void
}

function getDisplayName(artifact: Artifact): string {
  return artifact.name ?? artifact.fileName ?? artifact.objectKey?.split('/').pop() ?? artifact.id.slice(0, 8)
}

type WizardState = {
  step: Step
  useOtherAgent: boolean
  targetAgentId: string
  targetName: string
  dryRun: boolean
  confirmName: string
}

const INITIAL_WIZARD: WizardState = {
  step: 'source',
  useOtherAgent: false,
  targetAgentId: '',
  targetName: '',
  dryRun: false,
  confirmName: '',
}

export function RestoreWizardDialog({ open, artifact, onClose }: Props) {
  const { t } = useI18n()
  const setActiveRestoreJobId = useUiStore((state) => state.setActiveRestoreJobId)

  const [wiz, setWiz] = useState<WizardState>(INITIAL_WIZARD)
  const { step, useOtherAgent, targetAgentId, targetName, dryRun, confirmName } = wiz

  function patch(update: Partial<WizardState>) {
    setWiz((prev) => ({ ...prev, ...update }))
  }

  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
    enabled: open && step === 'target',
  })

  const restoreMutation = useMutation({
    mutationFn: () =>
      requestRestore({
        artifactId: artifact!.id,
        targetAgentId: useOtherAgent && targetAgentId ? targetAgentId : undefined,
        targetName: targetName || undefined,
        dryRun,
        force: false,
      }),
    onSuccess: (data) => {
      setActiveRestoreJobId(data.restoreJobId)
      toast.success(t('Restore job queued'))
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Restore request failed'))
    },
  })

  useEffect(() => {
    if (!open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setWiz(INITIAL_WIZARD)
    } else if (artifact) {
      setWiz({ ...INITIAL_WIZARD, targetName: artifact.name ?? artifact.fileName ?? '' })
    }
  }, [open, artifact])

  if (!artifact) return null

  const displayName = getDisplayName(artifact)
  const agents = agentsQuery.data ?? []

  const stepIndex = STEP_ORDER.indexOf(step)
  const stepTitles: Record<Step, string> = {
    source: t('Restore backup — Source'),
    target: t('Restore backup — Target'),
    confirm: t('Restore backup — Confirm'),
  }

  const targetAgent = useOtherAgent && targetAgentId
    ? agents.find((a) => a.id === targetAgentId)
    : null

  const footer = (
    <div className="flex w-full items-center justify-between gap-3">
      <Button variant="ghost" onClick={step === 'source' ? onClose : () => patch({ step: step === 'confirm' ? 'target' : 'source' })}>
        {step === 'source' ? t('Cancel') : t('Back')}
      </Button>
      <div className="flex gap-2">
        {step !== 'confirm' ? (
          <Button
            variant="primary"
            onClick={() => patch({ step: step === 'source' ? 'target' : 'confirm' })}
            disabled={step === 'target' && useOtherAgent && !targetAgentId}
          >
            {t('Next')}
          </Button>
        ) : (
          <Button
            variant="primary"
            onClick={() => restoreMutation.mutate()}
            disabled={confirmName !== displayName || restoreMutation.isPending}
          >
            <RotateCcw className="h-4 w-4" />
            {restoreMutation.isPending ? t('Queuing...') : dryRun ? t('Dry run') : t('Restore')}
          </Button>
        )}
      </div>
    </div>
  )

  return (
    <Dialog
      open={open}
      title={stepTitles[step]}
      footer={footer}
      onClose={onClose}
    >
      <Stepper stepIndex={stepIndex} t={t} />

      {step === 'source' && (
        <div className="space-y-4">
          <div className="rounded-lg border border-border bg-secondary/40 p-4 space-y-2">
            <p className="font-medium text-foreground">{displayName}</p>
            {artifact.type && (
              <p className="text-xs uppercase tracking-wider text-muted-foreground">{artifact.type}</p>
            )}
          </div>
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div className="space-y-0.5">
              <p className="text-xs text-muted-foreground">{t('Size')}</p>
              <p className="font-medium">{formatFileSize(artifact.size)}</p>
            </div>
            <div className="space-y-0.5">
              <p className="text-xs text-muted-foreground">{t('Created')}</p>
              <p className="font-medium">{formatRelativeTime(artifact.createdAt)}</p>
            </div>
            {artifact.checksum && (
              <div className="col-span-2 space-y-0.5">
                <p className="text-xs text-muted-foreground">{t('Checksum')}</p>
                <p className="font-mono text-xs truncate">{artifact.checksum}</p>
              </div>
            )}
          </div>
        </div>
      )}

      {step === 'target' && (
        <div className="space-y-5">
          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">
              {t('Restore to')}
            </p>
            <div className="grid gap-2 sm:grid-cols-2">
              <TargetCard
                tone="recommended"
                label={t('Original agent')}
                description={t('In-place restore on the originating host.')}
                selected={!useOtherAgent}
                onClick={() => patch({ useOtherAgent: false, targetAgentId: '' })}
                t={t}
              />
              <TargetCard
                tone="advanced"
                label={t('Different agent')}
                description={t('For cross-host recovery or migration.')}
                selected={useOtherAgent}
                onClick={() => patch({ useOtherAgent: true })}
                t={t}
              />
            </div>
            {useOtherAgent && (
              <Select
                value={targetAgentId}
                onChange={(e) => patch({ targetAgentId: e.target.value })}
                className="mt-2"
              >
                <option value="">{t('Select agent...')}</option>
                {agents.map((agent) => (
                  <option key={agent.id} value={agent.id}>
                    {agent.name}{agent.machineName ? ` (${agent.machineName})` : ''}
                  </option>
                ))}
              </Select>
            )}
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium">{t('Target path / name')}</label>
            <Input
              value={targetName}
              onChange={(e) => patch({ targetName: e.target.value })}
              placeholder={displayName}
            />
            <p className="text-xs text-muted-foreground">{t('Leave blank to restore to original location.')}</p>
          </div>

          <div className="rounded-lg border border-warning/40 bg-warning/10 px-4 py-3 flex items-start gap-3">
            <AlertTriangle className="h-4 w-4 shrink-0 text-warning mt-0.5" />
            <p className="text-sm text-foreground">
              {t('This will overwrite existing data at the target location. Size: {size}', { size: formatFileSize(artifact.size) })}
            </p>
          </div>

          <label
            htmlFor="restore-dry-run"
            className="flex cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background/60 px-4 py-3 text-sm"
          >
            <span>{t('Dry run (verify only, no data written)')}</span>
            <Switch
              id="restore-dry-run"
              checked={dryRun}
              onCheckedChange={(value) => patch({ dryRun: value })}
            />
          </label>
        </div>
      )}

      {step === 'confirm' && (
        <div className="space-y-5">
          <div className="rounded-lg border border-border bg-secondary/40 p-4">
            <dl className="grid gap-3 text-sm sm:grid-cols-2">
              <SummaryRow label={t('Artifact')} value={displayName} mono />
              <SummaryRow label={t('Size')} value={formatFileSize(artifact.size)} />
              <SummaryRow
                label={t('Target agent')}
                value={targetAgent?.name ?? t('Originating agent')}
              />
              {targetName ? <SummaryRow label={t('Target name')} value={targetName} mono /> : null}
              <SummaryRow label={t('Mode')} value={dryRun ? t('Dry run') : t('Full restore')} />
            </dl>
          </div>

          <div className="space-y-1.5">
            <label htmlFor="restore-confirm-name" className="text-sm font-medium">
              {t('Type "{name}" to confirm', { name: displayName })}
            </label>
            <Input
              id="restore-confirm-name"
              value={confirmName}
              onChange={(e) => patch({ confirmName: e.target.value })}
              placeholder={displayName}
              autoFocus
            />
          </div>
        </div>
      )}
    </Dialog>
  )
}

function Stepper({ stepIndex, t }: { stepIndex: number; t: (key: string) => string }) {
  const labels = [t('Source'), t('Target'), t('Confirm')]
  return (
    <div className="mb-5">
      <div className="flex items-center gap-2">
        {STEP_ORDER.map((_, i) => {
          const isDone = i < stepIndex
          const isActive = i === stepIndex
          return (
            <div key={i} className="flex flex-1 items-center gap-2">
              <div
                className={cn(
                  'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors',
                  isDone && 'bg-primary text-primary-foreground',
                  isActive && 'bg-primary text-primary-foreground ring-2 ring-primary/30 ring-offset-2 ring-offset-card',
                  !isDone && !isActive && 'border border-border bg-background text-muted-foreground',
                )}
                aria-current={isActive ? 'step' : undefined}
              >
                {isDone ? <Check className="h-3.5 w-3.5" /> : i + 1}
              </div>
              {i < STEP_ORDER.length - 1 ? (
                <div
                  className={cn(
                    'h-px flex-1 transition-colors',
                    isDone ? 'bg-primary' : 'bg-border',
                  )}
                  aria-hidden
                />
              ) : null}
            </div>
          )
        })}
      </div>
      <div className="mt-2 grid grid-cols-3 text-[11px]">
        {labels.map((label, i) => (
          <span
            key={label}
            className={cn(
              'text-center',
              i === stepIndex ? 'font-medium text-foreground' : 'text-muted-foreground',
            )}
          >
            {label}
          </span>
        ))}
      </div>
    </div>
  )
}

interface TargetCardProps {
  tone: 'recommended' | 'advanced'
  label: string
  description: string
  selected: boolean
  onClick: () => void
  t: (key: string) => string
}

function TargetCard({ tone, label, description, selected, onClick, t }: TargetCardProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={selected}
      className={cn(
        'group flex flex-col gap-2 rounded-lg border bg-card px-3 py-3 text-left transition-colors',
        'hover:border-primary/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        selected
          ? 'border-primary shadow-[0_0_0_3px_hsl(var(--primary)/0.18)]'
          : 'border-border',
      )}
    >
      <Badge variant={tone === 'recommended' ? 'success' : 'outline'} className="self-start">
        {tone === 'recommended' ? t('recommended') : t('advanced')}
      </Badge>
      <p className="text-sm font-medium text-foreground">{label}</p>
      <p className="text-xs text-muted-foreground">{description}</p>
    </button>
  )
}

function SummaryRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-xs uppercase tracking-wider text-muted-foreground">{label}</dt>
      <dd className={cn('text-foreground', mono ? 'font-mono text-xs truncate' : 'text-sm font-medium')}>
        {value}
      </dd>
    </div>
  )
}
