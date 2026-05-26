import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'

import { getDashboardSummary, type DashboardSummary } from '@/shared/api/dashboard'
import { queryKeys } from '@/shared/lib/query'

export type OnboardingStepId = 'install' | 'approve' | 'policy'
export type OnboardingStepStatus = 'todo' | 'active' | 'done'

export interface OnboardingStep {
  id: OnboardingStepId
  titleKey: string
  descriptionKey: string
  status: OnboardingStepStatus
  /** Route the step CTA should navigate to (or special `install-agent-dialog` action) */
  action:
    | { kind: 'navigate'; to: string; labelKey: string }
    | { kind: 'install-agent-dialog'; labelKey: string }
}

export interface OnboardingState {
  steps: OnboardingStep[]
  done: number
  total: number
  percent: number
  allDone: boolean
  /** Active step (the next thing the user should do) — null if everything's done */
  activeStep: OnboardingStep | null
  isLoading: boolean
}

const EMPTY_SUMMARY: DashboardSummary = {
  agents: { online: 0, stale: 0, offline: 0, total: 0 },
  pendingAgentsCount: 0,
  policies: { active: 0, total: 0, byType: { filesystem: 0, postgres: 0, mysql: 0 } },
  jobs: {
    completed: 0,
    running: 0,
    failed: 0,
    total: 0,
    last7Days: [],
    unresolvedFailures: [],
    recent: [],
  },
  artifacts: { total: 0, totalSize: 0, recent: [] },
}

export function useOnboardingSteps(): OnboardingState {
  const summaryQuery = useQuery({
    queryKey: [...queryKeys.dashboard, 'summary'] as const,
    queryFn: getDashboardSummary,
    staleTime: 30_000,
  })

  return useMemo(() => {
    const summary = summaryQuery.data ?? EMPTY_SUMMARY
    const hasAgent = summary.agents.total > 0
    const hasPending = summary.pendingAgentsCount > 0
    const hasPolicy = summary.policies.active > 0

    // Step statuses — only one step is "active" at a time (the next actionable one).
    // Steps before are "done", steps after are "todo".
    const installDone = hasAgent
    const approveDone = hasAgent // implies the request was approved (otherwise we'd still see pending only)
    const policyDone = hasPolicy

    const flags = [installDone, approveDone, policyDone]
    const firstUndoneIdx = flags.findIndex((f) => !f)

    function statusFor(idx: number): OnboardingStepStatus {
      if (flags[idx]) return 'done'
      if (idx === firstUndoneIdx) return 'active'
      return 'todo'
    }

    const steps: OnboardingStep[] = [
      {
        id: 'install',
        titleKey: 'Install your first agent',
        descriptionKey: 'Deploy the RestoreMe agent on a machine you want to protect.',
        status: statusFor(0),
        action: { kind: 'install-agent-dialog', labelKey: 'Install agent' },
      },
      {
        id: 'approve',
        titleKey: 'Approve the pending request',
        descriptionKey: 'Once installed, the agent appears here for review. Approve it to activate.',
        status: hasPending && !hasAgent ? 'active' : statusFor(1),
        action: { kind: 'navigate', to: '/pending-agents', labelKey: 'Review' },
      },
      {
        id: 'policy',
        titleKey: 'Create your first backup policy',
        descriptionKey: 'Define what to back up, where to store it, and how often.',
        status: statusFor(2),
        action: { kind: 'navigate', to: '/policies', labelKey: 'Create policy' },
      },
    ]

    const done = steps.filter((s) => s.status === 'done').length
    const total = steps.length
    const percent = Math.round((done / total) * 100)
    const allDone = done === total
    const activeStep = steps.find((s) => s.status === 'active') ?? null

    return {
      steps,
      done,
      total,
      percent,
      allDone,
      activeStep,
      isLoading: summaryQuery.isLoading && !summaryQuery.data,
    }
  }, [summaryQuery.data, summaryQuery.isLoading])
}
