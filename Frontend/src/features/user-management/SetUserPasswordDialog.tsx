import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { setUserPassword } from '@/entities/user/api'
import type { AdminUser } from '@/entities/user/model/types'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { useI18n } from '@/shared/i18n'

const setPasswordSchema = z
  .object({
    newPassword: z.string().min(8, 'Password must contain at least 8 characters').max(128, 'Password is too long'),
    confirmPassword: z.string().min(8, 'Confirm the new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })

type SetPasswordValues = z.infer<typeof setPasswordSchema>

type SetUserPasswordDialogProps = {
  open: boolean
  user: AdminUser | null
  onClose: () => void
  onSuccess: () => void
}

export function SetUserPasswordDialog({ open, user, onClose, onSuccess }: SetUserPasswordDialogProps) {
  const { t } = useI18n()
  const formError = (message?: string) => (message ? t(message) : undefined)
  const form = useForm<SetPasswordValues>({
    resolver: zodResolver(setPasswordSchema),
    mode: 'onChange',
    defaultValues: {
      newPassword: '',
      confirmPassword: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: SetPasswordValues) => {
      if (!user) {
        throw new Error('User is required')
      }

      return setUserPassword(user.id, values.newPassword)
    },
    onSuccess: () => {
      toast.success(t('Password updated'))
      form.reset({
        newPassword: '',
        confirmPassword: '',
      })
      onSuccess()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to update password'))
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Change user password')}
      description={user ? t('Set a new password for {username}.', { username: user.username }) : t('Set a new password for the selected user.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button
            disabled={!form.formState.isValid || mutation.isPending}
            onClick={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {mutation.isPending ? t('Saving...') : t('Save password')}
          </Button>
        </>
      }
    >
      <div className="space-y-2">
        <label className="text-sm font-medium text-ink-900" htmlFor="set-user-password-new">
          {t('New password')}
        </label>
        <Input id="set-user-password-new" type="password" placeholder="StrongPass123!" {...form.register('newPassword')} />
        {form.formState.errors.newPassword ? (
          <p className="text-sm text-danger-500">{formError(form.formState.errors.newPassword.message)}</p>
        ) : null}
      </div>
      <div className="space-y-2">
        <label className="text-sm font-medium text-ink-900" htmlFor="set-user-password-confirm">
          {t('Confirm password')}
        </label>
        <Input id="set-user-password-confirm" type="password" placeholder={t('Repeat the new password')} {...form.register('confirmPassword')} />
        {form.formState.errors.confirmPassword ? (
          <p className="text-sm text-danger-500">{formError(form.formState.errors.confirmPassword.message)}</p>
        ) : null}
      </div>
    </Dialog>
  )
}
