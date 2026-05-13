import { Suspense, lazy, type ReactNode } from 'react'
import { createBrowserRouter } from 'react-router-dom'

import { RequireAuth } from '@/app/providers/RequireAuth'
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
const AccountPage = lazy(() => import('@/pages/account/AccountPage').then(module => ({ default: module.AccountPage })))

function withSuspense(node: ReactNode) {
  return (
    <Suspense
      fallback={
        <div className="rounded-3xl border border-slate-200 bg-white/75 p-8 text-sm text-slate-700 shadow-xl animate-fade-in dark:border-slate-800 dark:bg-slate-900/75 dark:text-slate-300">
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
    errorElement: <NotFoundPage />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        {withSuspense(<AppShell />)}
      </RequireAuth>
    ),
    errorElement: <NotFoundPage />,
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
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
])
