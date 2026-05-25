import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { AlertTriangle, RotateCcw } from 'lucide-react'

import { getAgents } from '@/shared/api/agents'
import { getRestoreStatus, requestRestore, type Artifact } from '@/shared/api/artifacts'
import { queryKeys } from '@/shared/lib/query'
import { formatFileSize, formatRelativeTime } from '@/shared/lib/format'
import { useUiStore } from '@/app/store/ui-store'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'
import { useI18n } from '@/shared/i18n'

type Step = 'source' | 'target' | 'confirm'

interface Props {
  open: boolean
  artifact: Artifact | null
  onClose: () => void
}

function getDisplayName(artifact: Artifact): string {
  return artifact.name ?? artifact.fileName ?? artifact.objectKey?.split('/').pop() ?? artifact.id.slice(0, 8)
}

export function RestoreWizardDialog({ open, artifact, onClose }: Props) {
  const { t } = useI18n()
  const setActiveRestoreJobId = useUiStore((state) => state.setActiveRestoreJobId)

  const [step, setStep] = useState<Step>('source')
  const [useOtherAgent, setUseOtherAgent] = useState(false)
  const [targetAgentId, setTargetAgentId] = useState('')
  const [targetName, setTargetName] = useState('')
  const [dryRun, setDryRun] = useState(false)
  const [confirmName, setConfirmName] = useState('')

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
      setStep('source')
      setConfirmName('')
      setUseOtherAgent(false)
      setTargetAgentId('')
      setDryRun(false)
    } else if (artifact) {
      setTargetName(artifact.name ?? artifact.fileName ?? '')
    }
  }, [open, artifact])

  if (!artifact) return null

  const displayName = getDisplayName(artifact)
  const agents = agentsQuery.data ?? []

  const stepTitles: Record<Step, string> = {
    source: t('Restore backup — Source'),
    target: t('Restore backup — Target'),
    confirm: t('Restore backup — Confirm'),
  }

  const footer = (
    <div className="flex w-full items-center justify-between gap-3">
      <Button variant="ghost" onClick={step === 'source' ? onClose : () => setStep(step === 'confirm' ? 'target' : 'source')}>
        {step === 'source' ? t('Cancel') : t('Back')}
      </Button>
      <div className="flex gap-2">
        <span className="text-xs text-muted-foreground self-center">
          {step === 'source' ? '1/3' : step === 'target' ? '2/3' : '3/3'}
        </span>
        {step !== 'confirm' ? (
          <Button variant="primary" onClick={() => setStep(step === 'source' ? 'target' : 'confirm')}>
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
            <p className="text-sm font-medium">{t('Restore target')}</p>
            <div className="flex flex-col gap-2">
              <label className="flex items-center gap-3 cursor-pointer">
                <input
                  type="radio"
                  checked={!useOtherAgent}
                  onChange={() => { setUseOtherAgent(false); setTargetAgentId('') }}
                  className="accent-primary"
                />
                <span className="text-sm">{t('Originating agent (default)')}</span>
              </label>
              <label className="flex items-center gap-3 cursor-pointer">
                <input
                  type="radio"
                  checked={useOtherAgent}
                  onChange={() => setUseOtherAgent(true)}
                  className="accent-primary"
                />
                <span className="text-sm">{t('Different agent')}</span>
              </label>
            </div>
            {useOtherAgent && (
              <Select
                value={targetAgentId}
                onChange={(e) => setTargetAgentId(e.target.value)}
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
              onChange={(e) => setTargetName(e.target.value)}
              placeholder={displayName}
            />
            <p className="text-xs text-muted-foreground">{t('Leave blank to restore to original location.')}</p>
          </div>

          <div className="rounded-lg border border-warning/40 bg-warning/8 px-4 py-3 flex items-start gap-3">
            <AlertTriangle className="h-4 w-4 shrink-0 text-warning mt-0.5" />
            <p className="text-sm text-warning-foreground">
              {t('This will overwrite existing data at the target location. Size: {size}', { size: formatFileSize(artifact.size) })}
            </p>
          </div>

          <label className="flex items-center gap-3 cursor-pointer">
            <input
              type="checkbox"
              checked={dryRun}
              onChange={(e) => setDryRun(e.target.checked)}
              className="accent-primary"
            />
            <span className="text-sm">{t('Dry run (verify only, no data written)')}</span>
          </label>
        </div>
      )}

      {step === 'confirm' && (
        <div className="space-y-5">
          <table className="w-full text-sm">
            <tbody className="divide-y divide-border">
              <tr>
                <td className="py-2 pr-4 text-muted-foreground w-1/3">{t('Artifact')}</td>
                <td className="py-2 font-medium truncate max-w-0">{displayName}</td>
              </tr>
              <tr>
                <td className="py-2 pr-4 text-muted-foreground">{t('Size')}</td>
                <td className="py-2">{formatFileSize(artifact.size)}</td>
              </tr>
              <tr>
                <td className="py-2 pr-4 text-muted-foreground">{t('Target agent')}</td>
                <td className="py-2">
                  {useOtherAgent && targetAgentId
                    ? (agentsQuery.data?.find((a) => a.id === targetAgentId)?.name ?? targetAgentId)
                    : t('Originating agent')}
                </td>
              </tr>
              {targetName && (
                <tr>
                  <td className="py-2 pr-4 text-muted-foreground">{t('Target name')}</td>
                  <td className="py-2 font-mono text-xs">{targetName}</td>
                </tr>
              )}
              <tr>
                <td className="py-2 pr-4 text-muted-foreground">{t('Mode')}</td>
                <td className="py-2">{dryRun ? t('Dry run') : t('Full restore')}</td>
              </tr>
            </tbody>
          </table>

          <div className="space-y-1.5">
            <label className="text-sm font-medium">
              {t('Type "{name}" to confirm', { name: displayName })}
            </label>
            <Input
              value={confirmName}
              onChange={(e) => setConfirmName(e.target.value)}
              placeholder={displayName}
              autoFocus
            />
          </div>
        </div>
      )}
    </Dialog>
  )
}
