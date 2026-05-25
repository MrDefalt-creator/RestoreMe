import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { CheckCircle2, Circle, Loader2 } from 'lucide-react'

import { useUiStore } from '@/app/store/ui-store'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/Card'
import { useI18n } from '@/shared/i18n'
import type { DashboardSummary } from '@/shared/api/dashboard'

type StepStatus = 'idle' | 'active' | 'done'

interface Props {
  summary: DashboardSummary
}

export function FirstRunCard({ summary }: Props) {
  const { t } = useI18n()
  const navigate = useNavigate()
  const setInstallAgentDialogOpen = useUiStore((state) => state.setInstallAgentDialogOpen)
  const setFirstRunDismissed = useUiStore((state) => state.setFirstRunDismissed)

  const step1: StepStatus = summary.agents.total > 0 ? 'done' : 'active'
  const step2: StepStatus = summary.agents.total > 0 ? 'done' : summary.pendingAgentsCount > 0 ? 'active' : 'idle'
  const step3: StepStatus = summary.policies.active > 0 ? 'done' : summary.agents.total > 0 ? 'active' : 'idle'
  const allDone = step1 === 'done' && step2 === 'done' && step3 === 'done'

  useEffect(() => {
    if (allDone) {
      const timer = setTimeout(() => setFirstRunDismissed(true), 1200)
      return () => clearTimeout(timer)
    }
  }, [allDone, setFirstRunDismissed])

  const steps = [
    {
      status: step1,
      title: t('Install your first agent'),
      description: t('Deploy the RestoreMe agent on a machine you want to protect.'),
      action: (
        <Button variant="primary" size="sm" onClick={() => setInstallAgentDialogOpen(true)}>
          {t('Install agent')}
        </Button>
      ),
    },
    {
      status: step2,
      title: t('Approve the pending request'),
      description: t('Once installed, the agent appears here for review. Approve it to activate.'),
      action: (
        <Button variant="secondary" size="sm" onClick={() => navigate('/pending-agents')}>
          {t('Review')}
        </Button>
      ),
    },
    {
      status: step3,
      title: t('Create your first backup policy'),
      description: t('Define what to back up, where to store it, and how often.'),
      action: (
        <Button variant="secondary" size="sm" onClick={() => navigate('/policies')}>
          {t('Create policy')}
        </Button>
      ),
    },
  ]

  return (
    <Card
      className={allDone ? 'transition-opacity duration-500 opacity-0' : ''}
    >
      <CardHeader>
        <CardTitle>{t('Welcome to RestoreMe')}</CardTitle>
        <p className="text-sm text-muted-foreground">
          {t('Complete these steps to protect your first machine.')}
        </p>
      </CardHeader>
      <CardContent>
        <div className="divide-y divide-border">
          {steps.map((step, i) => (
            <div key={i} className="flex items-start gap-4 py-4 first:pt-0 last:pb-0">
              <div className="mt-0.5 shrink-0">
                {step.status === 'done' ? (
                  <CheckCircle2 className="h-5 w-5 text-success" />
                ) : step.status === 'active' ? (
                  <Loader2 className="h-5 w-5 animate-spin text-primary" />
                ) : (
                  <Circle className="h-5 w-5 text-muted-foreground/40" />
                )}
              </div>
              <div className="min-w-0 flex-1">
                <p className={`font-medium ${step.status === 'done' ? 'text-muted-foreground line-through' : 'text-foreground'}`}>
                  {step.title}
                </p>
                <p className="mt-0.5 text-sm text-muted-foreground">{step.description}</p>
              </div>
              {step.status !== 'done' && step.status !== 'idle' && (
                <div className="shrink-0">{step.action}</div>
              )}
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
