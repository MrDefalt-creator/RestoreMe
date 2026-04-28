import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { changeOwnPassword } from '@/entities/auth/api'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'

const accountPasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Current password is required'),
    newPassword: z.string().min(8, 'Password must contain at least 8 characters').max(128, 'Password is too long'),
    confirmPassword: z.string().min(8, 'Confirm the new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })

type AccountPasswordValues = z.infer<typeof accountPasswordSchema>

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
  const form = useForm<AccountPasswordValues>({
    resolver: zodResolver(accountPasswordSchema),
    mode: 'onChange',
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: AccountPasswordValues) => changeOwnPassword(values.currentPassword, values.newPassword),
    onSuccess: () => {
      toast.success('Password updated')
      form.reset({
        currentPassword: '',
        newPassword: '',
        confirmPassword: '',
      })
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
        description="Review the active console identity and change the current password without involving another administrator."
      />

      <div className="grid gap-4 lg:grid-cols-[360px_minmax(0,1fr)]">
        <Card className="space-y-4">
          <div>
            <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">Current session</p>
            <h2 className="mt-3 text-2xl font-semibold text-ink-950">{user?.username ?? 'Unknown user'}</h2>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge tone="neutral">Signed in</Badge>
            <Badge tone={user?.role === 'admin' ? 'warning' : user?.role === 'operator' ? 'success' : 'neutral'}>
              {formatRole(user?.role)}
            </Badge>
          </div>
          <p className="text-sm text-ink-800/75">
            This view is available to administrators, operators and viewers. Only the current password is accepted for self-service password rotation.
          </p>
        </Card>

        <Card className="space-y-5">
          <div>
            <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">Password</p>
            <h3 className="mt-3 text-2xl font-semibold text-ink-950">Change your password</h3>
          </div>

          <form className="space-y-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="account-current-password">
                Current password
              </label>
              <Input id="account-current-password" type="password" placeholder="Enter current password" {...form.register('currentPassword')} />
              {form.formState.errors.currentPassword ? (
                <p className="text-sm text-danger-500">{form.formState.errors.currentPassword.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="account-new-password">
                New password
              </label>
              <Input id="account-new-password" type="password" placeholder="Choose a stronger password" {...form.register('newPassword')} />
              {form.formState.errors.newPassword ? (
                <p className="text-sm text-danger-500">{form.formState.errors.newPassword.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="account-confirm-password">
                Confirm new password
              </label>
              <Input id="account-confirm-password" type="password" placeholder="Repeat the new password" {...form.register('confirmPassword')} />
              {form.formState.errors.confirmPassword ? (
                <p className="text-sm text-danger-500">{form.formState.errors.confirmPassword.message}</p>
              ) : null}
            </div>

            <Button type="submit" disabled={!form.formState.isValid || mutation.isPending}>
              Update password
            </Button>
          </form>
        </Card>
      </div>
    </div>
  )
}
