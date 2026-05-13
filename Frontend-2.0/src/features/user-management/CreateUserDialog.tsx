import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { createUser, type UserRole } from '@/shared/api/users'
import { queryKeys } from '@/shared/lib/query'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'

const createUserSchema = z.object({
  username: z.string().trim().min(3, 'Username must contain at least 3 characters').max(64, 'Username is too long'),
  password: z.string().min(8, 'Password must contain at least 8 characters').max(128, 'Password is too long'),
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
      form.reset()
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
      title="Create user"
      description="Add an operator, viewer or administrator account for RestoreMe."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={!form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {mutation.isPending ? 'Creating...' : 'Create user'}
          </Button>
        </>
      }
    >
      <Field label="Username" error={form.formState.errors.username?.message}>
        <Input placeholder="backup-operator" {...form.register('username')} />
      </Field>
      <Field label="Password" error={form.formState.errors.password?.message}>
        <Input type="password" placeholder="StrongPass123!" {...form.register('password')} />
      </Field>
      <Field label="Role" error={form.formState.errors.role?.message}>
        <Select {...form.register('role')}>
          <option value="viewer">Viewer</option>
          <option value="operator">Operator</option>
          <option value="admin">Admin</option>
        </Select>
      </Field>
    </Dialog>
  )
}

function Field({
  label,
  error,
  children,
}: {
  label: string
  error?: string
  children: React.ReactNode
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-medium text-foreground">{label}</label>
      {children}
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  )
}
