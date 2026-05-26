import * as RadixDialog from '@radix-ui/react-dialog'
import { useNavigate } from 'react-router-dom'
import { ShieldCheck, X } from 'lucide-react'

import { Button } from '@/shared/ui/Button'
import { cn } from '@/shared/lib/cn'
import { useI18n } from '@/shared/i18n'
import { useUiStore } from '@/app/store/ui-store'
import { useOnboardingSteps, type OnboardingStep, type OnboardingStepStatus } from './useOnboardingSteps'

interface Props {
  open: boolean
  onClose: () => void
}

export function OnboardingModal({ open, onClose }: Props) {
  const { t } = useI18n()
  const navigate = useNavigate()
  const setInstallAgentDialogOpen = useUiStore((s) => s.setInstallAgentDialogOpen)
  const setOnboardingWidgetExpanded = useUiStore((s) => s.setOnboardingWidgetExpanded)
  const { steps } = useOnboardingSteps()

  function runStepAction(step: OnboardingStep) {
    if (step.action.kind === 'navigate') {
      navigate(step.action.to)
    } else {
      setInstallAgentDialogOpen(true)
    }
  }

  function start() {
    onClose()
    setOnboardingWidgetExpanded(true)
    const firstActive = steps.find((s) => s.status === 'active')
    if (firstActive) {
      runStepAction(firstActive)
    }
  }

  return (
    <RadixDialog.Root open={open} onOpenChange={(o) => !o && onClose()}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay
          className={cn(
            'fixed inset-0 z-50 bg-background/85 backdrop-blur-sm',
            'data-[state=open]:animate-in data-[state=closed]:animate-out',
            'data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
          )}
        />
        <RadixDialog.Content
          aria-describedby={undefined}
          className={cn(
            'fixed left-1/2 top-[10vh] z-50 w-full max-w-[520px] -translate-x-1/2',
            'overflow-hidden rounded-2xl border border-border bg-card shadow-[var(--shadow-xl)] focus:outline-none',
            'data-[state=open]:animate-in data-[state=closed]:animate-out',
            'data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
            'data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95',
          )}
        >
          <button
            type="button"
            onClick={onClose}
            className="absolute right-3 top-3 z-10 flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-secondary hover:text-foreground"
            aria-label={t('Close')}
          >
            <X className="h-4 w-4" />
          </button>

          {/* Gradient hero with shield icon */}
          <div
            className="px-6 py-8 text-center"
            style={{
              background:
                'linear-gradient(135deg, hsl(var(--primary) / 0.14), hsl(var(--accent) / 0.12))',
            }}
          >
            <div
              className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-card"
              style={{ boxShadow: 'var(--shadow-md)' }}
            >
              <ShieldCheck className="h-7 w-7 text-primary" strokeWidth={1.8} />
            </div>
            <RadixDialog.Title className="mt-4 text-2xl font-semibold tracking-tight text-foreground">
              {t('Welcome to RestoreMe')}
            </RadixDialog.Title>
            <p className="mt-1 text-sm text-muted-foreground">
              {t('Protect your first machine in 3 steps')}
            </p>
          </div>

          {/* Steps */}
          <ol className="divide-y divide-border">
            {steps.map((step, i) => (
              <StepRow
                key={step.id}
                index={i + 1}
                step={step}
                onAction={() => {
                  onClose()
                  runStepAction(step)
                }}
                t={t}
              />
            ))}
          </ol>

          {/* Footer */}
          <div className="flex flex-col gap-3 border-t border-border bg-secondary/30 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
            <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
              <span className="relative flex h-2 w-2">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-success/60" />
                <span className="relative inline-flex h-2 w-2 rounded-full bg-success" />
              </span>
              {t('Polling for new agents')}
            </span>
            <div className="flex items-center justify-end gap-2">
              <Button variant="ghost" size="sm" onClick={onClose}>
                {t('Later')}
              </Button>
              <Button variant="primary" size="sm" onClick={start}>
                {t('Get started')}
              </Button>
            </div>
          </div>
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  )
}

function StepRow({
  index,
  step,
  onAction,
  t,
}: {
  index: number
  step: OnboardingStep
  onAction: () => void
  t: (k: string, p?: Record<string, string | number>) => string
}) {
  return (
    <li className="flex items-center gap-4 px-6 py-4">
      <StepDot status={step.status} index={index} />
      <div className="min-w-0 flex-1">
        <p
          className={cn(
            'text-sm font-medium',
            step.status === 'done'
              ? 'text-muted-foreground line-through'
              : 'text-foreground',
          )}
        >
          {t(step.titleKey)}
        </p>
        <p className="mt-0.5 text-xs leading-5 text-muted-foreground">
          {t(step.descriptionKey)}
        </p>
      </div>
      <div className="shrink-0">
        {step.status === 'active' ? (
          <Button variant="primary" size="sm" onClick={onAction}>
            {t(step.action.labelKey)}
          </Button>
        ) : step.status === 'done' ? (
          <Button variant="secondary" size="sm" disabled>
            {t('Done')}
          </Button>
        ) : (
          <Button variant="secondary" size="sm" disabled className="opacity-50">
            {t('Locked')}
          </Button>
        )}
      </div>
    </li>
  )
}

function StepDot({ status, index }: { status: OnboardingStepStatus; index: number }) {
  return (
    <span
      className={cn(
        'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border text-xs font-semibold transition-colors',
        status === 'active' &&
          'border-primary bg-primary text-primary-foreground shadow-[0_6px_18px_hsl(var(--primary)/0.30)]',
        status === 'done' &&
          'border-success/40 bg-success/15 text-success',
        status === 'todo' && 'border-border bg-card text-muted-foreground',
      )}
      aria-hidden
    >
      {status === 'done' ? '✓' : index}
    </span>
  )
}
