import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { CheckCircle2, Circle, Loader2, X } from 'lucide-react'

import { Button } from '@/shared/ui/Button'
import { useI18n } from '@/shared/i18n'
import { useUiStore } from '@/app/store/ui-store'
import { cn } from '@/shared/lib/cn'
import { useOnboardingSteps, type OnboardingStep } from './useOnboardingSteps'

export function OnboardingWidget() {
  const { t } = useI18n()
  const navigate = useNavigate()
  const expanded = useUiStore((s) => s.onboardingWidgetExpanded)
  const setExpanded = useUiStore((s) => s.setOnboardingWidgetExpanded)
  const onboardingDone = useUiStore((s) => s.onboardingDone)
  const setOnboardingDone = useUiStore((s) => s.setOnboardingDone)
  const setInstallAgentDialogOpen = useUiStore((s) => s.setInstallAgentDialogOpen)
  const { steps, done, total, percent, allDone, activeStep, isLoading } = useOnboardingSteps()

  // Persist completion the moment the user reaches all-done. Doing this
  // synchronously (no 3 s celebration timer) prevents the flag from being
  // lost when the underlying counts flip back — e.g. when the user later
  // removes every agent — and stops the widget from re-appearing as if
  // the tour was never finished.
  useEffect(() => {
    if (!allDone || onboardingDone || isLoading) return
    setOnboardingDone(true)
    setExpanded(false)
  }, [allDone, onboardingDone, isLoading, setOnboardingDone, setExpanded])

  // Re-show the widget after onboarding is complete when a "recommended"
  // step (e.g. set-personal-password) becomes active again — for example
  // after an admin resets the user's password.
  const hasReactivatingStep = steps.some(
    (s) => s.id === 'set-personal-password' && s.status === 'active',
  )
  if ((onboardingDone && !hasReactivatingStep) || isLoading) return null

  function runStepAction(step: OnboardingStep) {
    if (step.action.kind === 'navigate') {
      navigate(step.action.to)
    } else {
      setInstallAgentDialogOpen(true)
    }
  }

  return (
    <div className="fixed bottom-4 right-4 z-40">
      {expanded ? (
        <div className="w-[360px] overflow-hidden rounded-xl border border-border bg-card shadow-[var(--shadow-xl)] animate-scale-in">
          <div className="flex items-center justify-between gap-3 border-b border-border/80 px-4 py-3">
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-foreground">
                {allDone ? `${t('Setup complete')} 🎉` : t('Setup')}
              </p>
              <p className="text-xs text-muted-foreground">
                {t('{done} of {total} steps', { done, total })}
                {' · '}
                {percent}%
              </p>
            </div>
            <button
              type="button"
              onClick={() => setExpanded(false)}
              className="flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:bg-secondary hover:text-foreground"
              aria-label={t('Hide setup checklist')}
              title={t('Hide setup checklist')}
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="h-1 w-full bg-secondary">
            <div
              className="h-full rounded-r-full bg-primary transition-[width] duration-300"
              style={{ width: `${percent}%` }}
            />
          </div>

          <ul className="max-h-[360px] divide-y divide-border overflow-y-auto">
            {steps.map((step) => (
              <li key={step.id} className="flex items-start gap-3 px-4 py-3">
                <div className="mt-0.5 shrink-0">
                  {step.status === 'done' ? (
                    <CheckCircle2 className="h-4 w-4 text-success" />
                  ) : step.status === 'active' ? (
                    <Loader2 className="h-4 w-4 animate-spin text-primary" />
                  ) : (
                    <Circle className="h-4 w-4 text-muted-foreground/40" />
                  )}
                </div>
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
                {step.status === 'active' ? (
                  <Button
                    variant="primary"
                    size="sm"
                    className="shrink-0"
                    onClick={() => runStepAction(step)}
                  >
                    {t(step.action.labelKey)}
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>

          {!allDone && activeStep ? (
            <div className="border-t border-border/80 bg-secondary/35 px-4 py-3 text-xs text-muted-foreground">
              {t('Step {current} of {total}', { current: done + 1, total })}
              {' · '}
              {t(activeStep.titleKey)}
            </div>
          ) : null}
        </div>
      ) : (
        <CollapsedDot
          percent={percent}
          label={allDone ? `${t('All set')} · 100%` : `${t('Setup')} · ${percent}%`}
          onClick={() => setExpanded(true)}
          aria-label={t('Show setup checklist')}
        />
      )}
    </div>
  )
}

interface CollapsedDotProps {
  percent: number
  label: string
  onClick: () => void
  'aria-label': string
}

function CollapsedDot({ percent, label, onClick, 'aria-label': ariaLabel }: CollapsedDotProps) {
  const size = 56
  const radius = 24
  const stroke = 4
  const circumference = 2 * Math.PI * radius
  const offset = circumference - (percent / 100) * circumference

  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className="group flex items-center gap-2 rounded-full border border-border bg-card pl-1 pr-3 shadow-[var(--shadow-lg)] transition hover:shadow-[var(--shadow-xl)]"
    >
      <span className="relative inline-flex items-center justify-center" style={{ width: size, height: size }}>
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke="hsl(var(--secondary))"
            strokeWidth={stroke}
          />
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke="hsl(var(--primary))"
            strokeWidth={stroke}
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            strokeLinecap="round"
            transform={`rotate(-90 ${size / 2} ${size / 2})`}
            style={{ transition: 'stroke-dashoffset 300ms var(--ease-out)' }}
          />
        </svg>
        <span className="absolute text-xs font-semibold text-foreground">{percent}%</span>
      </span>
      <span className="text-sm font-medium text-foreground">{label}</span>
    </button>
  )
}
