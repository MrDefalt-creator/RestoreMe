import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { changeOwnPassword } from '@/shared/api/auth'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'

const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Current password is required'),
    newPassword: z.string().min(8, 'Password must contain at least 8 characters').max(128, 'Password is too long'),
    confirmPassword: z.string().min(8, 'Confirm the new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })

type PasswordValues = z.infer<typeof passwordSchema>

function formatRole(role: string | undefined) {
  switch (role) {
    case 'admin':
      return 'Admin'
    case 'operator':
      return 'Operator'
    default:
      return 'Viewer'
  }
}

export function AccountPage() {
  const user = useAuthStore((state) => state.user)
  const form = useForm<PasswordValues>({
    resolver: zodResolver(passwordSchema),
    mode: 'onChange',
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: PasswordValues) =>
      changeOwnPassword(values.currentPassword, values.newPassword),
    onSuccess: () => {
      toast.success('Password updated')
      form.reset()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to update password')
    },
  })

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow="Account"
        title="Identity and password"
        description="Review the active console identity and rotate your own password safely."
      />

      <div className="grid gap-5 lg:grid-cols-[360px_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Current session</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <p className="text-2xl font-semibold tracking-tight text-foreground">
                {user?.username ?? 'Unknown user'}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                Signed in to RestoreMe
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="success">Signed in</Badge>
              <Badge variant={user?.role === 'admin' ? 'warning' : user?.role === 'operator' ? 'success' : 'neutral'}>
                {formatRole(user?.role)}
              </Badge>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Change password</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
              <Field label="Current password" error={form.formState.errors.currentPassword?.message}>
                <Input type="password" placeholder="Enter current password" {...form.register('currentPassword')} />
              </Field>
              <Field label="New password" error={form.formState.errors.newPassword?.message}>
                <Input type="password" placeholder="Choose a stronger password" {...form.register('newPassword')} />
              </Field>
              <Field label="Confirm new password" error={form.formState.errors.confirmPassword?.message}>
                <Input type="password" placeholder="Repeat the new password" {...form.register('confirmPassword')} />
              </Field>
              <Button type="submit" disabled={!form.formState.isValid || mutation.isPending}>
                {mutation.isPending ? 'Updating...' : 'Update password'}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
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
