import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shield, UserCog } from 'lucide-react'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { CreateUserDialog } from '@/features/user-management/CreateUserDialog'
import { DeleteUserDialog } from '@/features/user-management/DeleteUserDialog'
import { SetUserPasswordDialog } from '@/features/user-management/SetUserPasswordDialog'
import { getUsers, updateUserRole, updateUserStatus } from '@/entities/user/api'
import type { AdminUser, UserRole } from '@/entities/user/model/types'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'

function formatRole(role: UserRole) {
  switch (role) {
    case 'admin':
      return 'Admin'
    case 'operator':
      return 'Operator'
    default:
      return 'Viewer'
  }
}

function getRoleTone(role: UserRole): 'success' | 'warning' | 'neutral' {
  switch (role) {
    case 'admin':
      return 'warning'
    case 'operator':
      return 'success'
    default:
      return 'neutral'
  }
}

function useUserMutations() {
  const queryClient = useQueryClient()

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: UserRole }) =>
      updateUserRole(userId, role),
    onSuccess: () => {
      toast.success('User role updated')
      void queryClient.invalidateQueries({ queryKey: queryKeys.users })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to update role')
    },
  })

  const statusMutation = useMutation({
    mutationFn: ({ userId, isActive }: { userId: string; isActive: boolean }) =>
      updateUserStatus(userId, isActive),
    onSuccess: () => {
      toast.success('User status updated')
      void queryClient.invalidateQueries({ queryKey: queryKeys.users })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to update status')
    },
  })

  return { roleMutation, statusMutation }
}

function isSameUser(
  currentUser: { id?: string; username?: string } | null | undefined,
  user: AdminUser,
) {
  const currentId = currentUser?.id?.trim().toLowerCase()
  const rowId = user.id.trim().toLowerCase()

  if (currentId && currentId === rowId) {
    return true
  }

  const currentUsername = currentUser?.username?.trim().toLowerCase()
  const rowUsername = user.username.trim().toLowerCase()
  return Boolean(currentUsername && currentUsername === rowUsername)
}

export function UsersPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const [passwordUser, setPasswordUser] = useState<AdminUser | null>(null)
  const [deletingUser, setDeletingUser] = useState<AdminUser | null>(null)
  const currentUser = useAuthStore((state) => state.user)
  const isAdmin = currentUser?.role === 'admin'
  const queryClient = useQueryClient()
  const usersQuery = useQuery({
    queryKey: queryKeys.users,
    queryFn: getUsers,
    enabled: isAdmin,
  })
  const { roleMutation, statusMutation } = useUserMutations()

  if (!isAdmin) {
    return (
      <div className="space-y-8">
        <SectionHeading
          eyebrow="Security"
          title="User access management"
          description="This workspace is reserved for administrators who manage operator and viewer access."
        />
        <EmptyState
          title="Administrator access required"
          description="Sign in as an administrator to create users, change roles and disable stale accounts."
        />
      </div>
    )
  }

  const users = usersQuery.data ?? []

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow="Security"
        title="User access management"
        description="Issue operator and viewer accounts, adjust privileges, rotate passwords and remove stale access without exposing backup agents or policy controls to every user."
        action={
          <Button onClick={() => setIsDialogOpen(true)}>
            Create user
          </Button>
        }
      />

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card>
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">Accounts</p>
              <p className="mt-3 text-3xl font-semibold text-ink-950">{users.length}</p>
              <p className="mt-2 text-sm text-ink-800/75">Platform identities seeded or created for the control plane.</p>
            </div>
            <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-surface-100 text-ink-900">
              <UserCog className="h-5 w-5" />
            </span>
          </div>
        </Card>
        <Card>
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">Active users</p>
              <p className="mt-3 text-3xl font-semibold text-ink-950">{users.filter((user) => user.isActive).length}</p>
              <p className="mt-2 text-sm text-ink-800/75">Accounts that can currently access the administrative workspace.</p>
            </div>
            <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-surface-100 text-ink-900">
              <Shield className="h-5 w-5" />
            </span>
          </div>
        </Card>
        <Card>
          <div className="space-y-3">
            <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">Role model</p>
            <div className="flex flex-wrap gap-2">
              <Badge tone="warning">Admin</Badge>
              <Badge tone="success">Operator</Badge>
              <Badge tone="neutral">Viewer</Badge>
            </div>
            <p className="text-sm text-ink-800/75">Admins manage access, operators manage agents and policies, viewers keep read-only visibility.</p>
          </div>
        </Card>
      </div>

      {users.length ? (
        <Card className="overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm align-middle">
              <thead className="bg-surface-100 text-ink-800">
                <tr>
                  {['Username', 'Role', 'Status', 'Created', 'Actions'].map((label) => (
                    <th key={label} className="px-4 py-3.5 font-medium align-middle">
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {users.map((user) => {
                  const isCurrent = isSameUser(currentUser, user)
                  const isBusy = roleMutation.isPending || statusMutation.isPending

                  return (
                    <tr key={user.id} className="border-t border-surface-200">
                      <td className="px-4 py-4 align-middle">
                        <div className="flex min-h-11 items-center gap-2">
                          <span className="font-medium text-ink-950">{user.username}</span>
                          {isCurrent ? <Badge tone="neutral">Current session</Badge> : null}
                        </div>
                      </td>
                      <td className="px-4 py-4 align-middle">
                        <div className="flex min-h-11 items-center">
                          <Badge tone={getRoleTone(user.role)}>{formatRole(user.role)}</Badge>
                        </div>
                      </td>
                      <td className="px-4 py-4 align-middle">
                        <div className="flex min-h-11 items-center">
                          <Badge tone={user.isActive ? 'success' : 'neutral'}>
                            {user.isActive ? 'Active' : 'Disabled'}
                          </Badge>
                        </div>
                      </td>
                      <td className="px-4 py-4 align-middle text-ink-800">
                        <div className="flex min-h-11 items-center">{formatDateTime(user.createdAtUtc)}</div>
                      </td>
                      <td className="px-4 py-4 align-middle">
                        <div className="flex min-h-11 items-center">
                          {isCurrent ? (
                            <div className="flex flex-wrap items-center gap-2">
                              <Badge tone="neutral">Protected account</Badge>
                              <span className="text-xs text-ink-800/65">Use the Account page to change your own password.</span>
                            </div>
                          ) : (
                            <div className="flex flex-wrap items-center gap-2">
                              <Select
                                value={user.role}
                                disabled={isBusy}
                                onChange={(event) =>
                                  roleMutation.mutate({
                                    userId: user.id,
                                    role: event.target.value as UserRole,
                                  })
                                }
                                className="min-w-[146px]"
                              >
                                <option value="viewer">Viewer</option>
                                <option value="operator">Operator</option>
                                <option value="admin">Admin</option>
                              </Select>
                              <Button
                                size="sm"
                                variant="secondary"
                                onClick={() => setPasswordUser(user)}
                              >
                                Password
                              </Button>
                              <Button
                                size="sm"
                                variant={user.isActive ? 'ghost' : 'secondary'}
                                disabled={isBusy}
                                onClick={() =>
                                  statusMutation.mutate({
                                    userId: user.id,
                                    isActive: !user.isActive,
                                  })
                                }
                              >
                                {user.isActive ? 'Disable' : 'Enable'}
                              </Button>
                              <Button
                                size="sm"
                                variant="danger"
                                onClick={() => setDeletingUser(user)}
                              >
                                Delete
                              </Button>
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </Card>
      ) : (
        <EmptyState
          title="No user accounts found"
          description="Create the first operator, viewer or administrator account for the secured control plane."
        />
      )}

      <CreateUserDialog open={isDialogOpen} onClose={() => setIsDialogOpen(false)} />
      <SetUserPasswordDialog
        open={Boolean(passwordUser)}
        user={passwordUser}
        onClose={() => setPasswordUser(null)}
        onSuccess={() => setPasswordUser(null)}
      />
      <DeleteUserDialog
        open={Boolean(deletingUser)}
        user={deletingUser}
        onClose={() => setDeletingUser(null)}
        onSuccess={() => {
          setDeletingUser(null)
          void queryClient.invalidateQueries({ queryKey: queryKeys.users })
        }}
      />
    </div>
  )
}
