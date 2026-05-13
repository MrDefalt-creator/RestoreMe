import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Shield, Trash2, UserCog } from 'lucide-react'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { CreateUserDialog } from '@/features/user-management/CreateUserDialog'
import { DeleteUserDialog } from '@/features/user-management/DeleteUserDialog'
import { SetUserPasswordDialog } from '@/features/user-management/SetUserPasswordDialog'
import {
  getUsers,
  updateUserRole,
  updateUserStatus,
  type User,
  type UserRole,
} from '@/shared/api/users'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
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

function isSameUser(currentUser: { id?: string; username?: string } | null | undefined, user: User) {
  return Boolean(
    (currentUser?.id && currentUser.id.toLowerCase() === user.id.toLowerCase()) ||
      (currentUser?.username && currentUser.username.toLowerCase() === user.username.toLowerCase()),
  )
}

export function UsersPage() {
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [passwordUser, setPasswordUser] = useState<User | null>(null)
  const [deletingUser, setDeletingUser] = useState<User | null>(null)
  const currentUser = useAuthStore((state) => state.user)
  const isAdmin = currentUser?.role === 'admin'
  const queryClient = useQueryClient()
  const usersQuery = useQuery({
    queryKey: queryKeys.users,
    queryFn: getUsers,
    enabled: isAdmin,
  })

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

  if (!isAdmin) {
    return (
      <div className="space-y-8">
        <SectionHeading
          eyebrow="Security"
          title="User access"
          description="Only administrators can create users, rotate passwords and change access roles."
        />
        <EmptyState
          icon={<Shield className="h-7 w-7 text-muted-foreground" />}
          title="Administrator access required"
          description="Sign in as an administrator to manage platform users."
        />
      </div>
    )
  }

  const users = usersQuery.data ?? []
  const isBusy = roleMutation.isPending || statusMutation.isPending

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow="Security"
        title="User access"
        description="Manage operator, viewer and administrator accounts without leaving the backup workspace."
        action={<Button onClick={() => setIsCreateOpen(true)}>Create user</Button>}
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardContent className="p-5">
            <Metric icon={UserCog} label="Accounts" value={users.length} />
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <Metric icon={Shield} label="Active users" value={users.filter((user) => user.isActive).length} />
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <div className="space-y-3">
              <p className="text-sm font-medium text-foreground">Role model</p>
              <div className="flex flex-wrap gap-2">
                <Badge variant="warning">Admin</Badge>
                <Badge variant="success">Operator</Badge>
                <Badge variant="neutral">Viewer</Badge>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {users.length ? (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-secondary text-muted-foreground">
                <tr>
                  {['Username', 'Role', 'Status', 'Created', 'Actions'].map((label) => (
                    <th key={label} className="px-4 py-3 font-medium">
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {users.map((user) => {
                  const isCurrent = isSameUser(currentUser, user)

                  return (
                    <tr key={user.id}>
                      <td className="px-4 py-3 font-medium text-foreground">
                        <div className="flex items-center gap-2">
                          {user.username}
                          {isCurrent ? <Badge variant="neutral">Current</Badge> : null}
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant={user.role === 'admin' ? 'warning' : user.role === 'operator' ? 'success' : 'neutral'}>
                          {formatRole(user.role)}
                        </Badge>
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant={user.isActive ? 'success' : 'neutral'}>
                          {user.isActive ? 'Active' : 'Disabled'}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-muted-foreground">
                        {user.createdAtUtc ? formatDateTime(user.createdAtUtc) : 'Unknown'}
                      </td>
                      <td className="px-4 py-3">
                        {isCurrent ? (
                          <span className="text-xs text-muted-foreground">Use Account to change your own password</span>
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
                              className="min-w-[136px]"
                            >
                              <option value="viewer">Viewer</option>
                              <option value="operator">Operator</option>
                              <option value="admin">Admin</option>
                            </Select>
                            <Button size="sm" variant="secondary" onClick={() => setPasswordUser(user)}>
                              <KeyRound className="h-4 w-4" />
                              Password
                            </Button>
                            <Button
                              size="sm"
                              variant="secondary"
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
                            <Button size="sm" variant="danger" onClick={() => setDeletingUser(user)}>
                              <Trash2 className="h-4 w-4" />
                              Delete
                            </Button>
                          </div>
                        )}
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
          description="Create the first operator, viewer or administrator account."
        />
      )}

      <CreateUserDialog open={isCreateOpen} onClose={() => setIsCreateOpen(false)} />
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

function Metric({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof UserCog
  label: string
  value: number
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <div>
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className="mt-2 text-3xl font-semibold tracking-tight text-foreground">{value}</p>
      </div>
      <span className="flex h-11 w-11 items-center justify-center rounded-lg bg-secondary text-primary">
        <Icon className="h-5 w-5" />
      </span>
    </div>
  )
}
