import { Suspense, lazy, type ReactNode } from 'react'
import { createBrowserRouter } from 'react-router-dom'

import { RequireAuth } from '@/app/providers/RequireAuth'
import { ErrorPage } from '@/pages/errors/ErrorPage'
import { NotFoundPage } from '@/pages/errors/NotFoundPage'

const AppShell = lazy(() => import('@/widgets/app-shell/AppShell').then(module => ({ default: module.AppShell })))
const LoginPage = lazy(() => import('@/pages/login/LoginPage').then(module => ({ default: module.LoginPage })))
const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage').then(module => ({ default: module.DashboardPage })))
const AgentsPage = lazy(() => import('@/pages/agents/AgentsPage').then(module => ({ default: module.AgentsPage })))
const PendingAgentsPage = lazy(() => import('@/pages/pending-agents/PendingAgentsPage').then(module => ({ default: module.PendingAgentsPage })))
const PoliciesPage = lazy(() => import('@/pages/policies/PoliciesPage').then(module => ({ default: module.PoliciesPage })))
const JobsPage = lazy(() => import('@/pages/jobs/JobsPage').then(module => ({ default: module.JobsPage })))
const ArtifactsPage = lazy(() => import('@/pages/artifacts/ArtifactsPage').then(module => ({ default: module.ArtifactsPage })))
const UsersPage = lazy(() => import('@/pages/users/UsersPage').then(module => ({ default: module.UsersPage })))
const NotificationChannelsPage = lazy(() => import('@/pages/notifications/NotificationChannelsPage').then(module => ({ default: module.NotificationChannelsPage })))
const AuditLogPage = lazy(() => import('@/pages/audit-log/AuditLogPage').then(module => ({ default: module.AuditLogPage })))
const AccountPage = lazy(() => import('@/pages/account/AccountPage').then(module => ({ default: module.AccountPage })))

function withSuspense(node: ReactNode) {
  return (
    <Suspense
      fallback={
        <div className="rounded-3xl border border-border bg-card/75 p-8 text-sm text-muted-foreground shadow-[var(--shadow-xl)] animate-fade-in">
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
    path: '/login',
    element: withSuspense(<LoginPage />),
    errorElement: <ErrorPage />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        {withSuspense(<AppShell />)}
      </RequireAuth>
    ),
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: withSuspense(<DashboardPage />) },
      { path: 'account', element: <AccountPage /> },
      { path: 'agents', element: withSuspense(<AgentsPage />) },
      { path: 'pending-agents', element: withSuspense(<PendingAgentsPage />) },
      { path: 'policies', element: withSuspense(<PoliciesPage />) },
      { path: 'jobs', element: withSuspense(<JobsPage />) },
      { path: 'backups', element: withSuspense(<ArtifactsPage />) },
      { path: 'artifacts', element: withSuspense(<ArtifactsPage />) },
      { path: 'users', element: withSuspense(<UsersPage />) },
      { path: 'notifications', element: withSuspense(<NotificationChannelsPage />) },
      { path: 'audit-log', element: withSuspense(<AuditLogPage />) },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
])
