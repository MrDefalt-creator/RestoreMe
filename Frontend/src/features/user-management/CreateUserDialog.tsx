import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { createUser } from '@/entities/user/api'
import type { UserRole } from '@/entities/user/model/types'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'

const createUserSchema = z.object({
  username: z
    .string()
    .trim()
    .min(3, 'Username must contain at least 3 characters')
    .max(64, 'Username is too long'),
  password: z
    .string()
    .min(8, 'Password must contain at least 8 characters')
    .max(128, 'Password is too long'),
  role: z.enum(['viewer', 'operator', 'admin']),
})

type CreateUserValues = z.infer<typeof createUserSchema>

type CreateUserDialogProps = {
  open: boolean
  onClose: () => void
}

export function CreateUserDialog({ open, onClose }: CreateUserDialogProps) {
  const queryClient = useQueryClient()
  const form = useForm<CreateUserValues>({
    resolver: zodResolver(createUserSchema),
    mode: 'onChange',
    defaultValues: {
      username: '',
      password: '',
      role: 'viewer',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CreateUserValues) =>
      createUser({
        username: values.username,
        password: values.password,
        role: values.role as UserRole,
      }),
    onSuccess: () => {
      toast.success('User created')
      void queryClient.invalidateQueries({ queryKey: queryKeys.users })
      form.reset({
        username: '',
        password: '',
        role: 'viewer',
      })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to create user')
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Create platform user"
      description="Add an operator, viewer or administrator account for the secure RestoreMe control plane."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={!form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            Create user
          </Button>
        </>
      }
    >
      <div className="space-y-2">
        <label className="text-sm font-medium text-ink-900" htmlFor="create-user-username">
          Username
        </label>
        <Input id="create-user-username" placeholder="backup-operator" {...form.register('username')} />
        {form.formState.errors.username ? (
          <p className="text-sm text-danger-500">{form.formState.errors.username.message}</p>
        ) : null}
      </div>

      <div className="space-y-2">
        <label className="text-sm font-medium text-ink-900" htmlFor="create-user-password">
          Password
        </label>
        <Input id="create-user-password" type="password" placeholder="StrongPass123!" {...form.register('password')} />
        {form.formState.errors.password ? (
          <p className="text-sm text-danger-500">{form.formState.errors.password.message}</p>
        ) : null}
      </div>

      <div className="space-y-2">
        <label className="text-sm font-medium text-ink-900" htmlFor="create-user-role">
          Role
        </label>
        <Select id="create-user-role" {...form.register('role')}>
          <option value="viewer">Viewer</option>
          <option value="operator">Operator</option>
          <option value="admin">Admin</option>
        </Select>
        {form.formState.errors.role ? (
          <p className="text-sm text-danger-500">{form.formState.errors.role.message}</p>
        ) : null}
      </div>
    </Dialog>
  )
}
