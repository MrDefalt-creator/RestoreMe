/* eslint-disable react-refresh/only-export-components */
import { Suspense, lazy, type ReactNode } from 'react'
import { createBrowserRouter } from 'react-router-dom'

import { AppShell } from '@/widgets/app-shell/AppShell'

const DashboardPage = lazy(() =>
  import('@/pages/dashboard/DashboardPage').then((module) => ({
    default: module.DashboardPage,
  })),
)
const AgentsPage = lazy(() =>
  import('@/pages/agents/AgentsPage').then((module) => ({
    default: module.AgentsPage,
  })),
)
const PendingAgentsPage = lazy(() =>
  import('@/pages/pending-agents/PendingAgentsPage').then((module) => ({
    default: module.PendingAgentsPage,
  })),
)
const PoliciesPage = lazy(() =>
  import('@/pages/policies/PoliciesPage').then((module) => ({
    default: module.PoliciesPage,
  })),
)
const JobsPage = lazy(() =>
  import('@/pages/jobs/JobsPage').then((module) => ({
    default: module.JobsPage,
  })),
)
const ArtifactsPage = lazy(() =>
  import('@/pages/artifacts/ArtifactsPage').then((module) => ({
    default: module.ArtifactsPage,
  })),
)

function withSuspense(node: ReactNode) {
  return (
    <Suspense
      fallback={
        <div className="rounded-3xl border border-surface-200 bg-white/75 p-8 text-sm text-ink-800 shadow-panel">
          Loading workspace...
        </div>
      }
    >
      {node}
    </Suspense>
  )
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: withSuspense(<DashboardPage />) },
      { path: 'agents', element: withSuspense(<AgentsPage />) },
      { path: 'pending-agents', element: withSuspense(<PendingAgentsPage />) },
      { path: 'policies', element: withSuspense(<PoliciesPage />) },
      { path: 'jobs', element: withSuspense(<JobsPage />) },
      { path: 'artifacts', element: withSuspense(<ArtifactsPage />) },
    ],
  },
])
