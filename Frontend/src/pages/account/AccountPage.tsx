import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { changeOwnPassword } from '@/entities/auth/api'
import {
  availableLanguages,
  availableRefreshIntervals,
  formatRoleLabel,
  useI18n,
  type DateStyle,
  type Language,
  type RefreshInterval,
} from '@/shared/i18n'
import { env } from '@/shared/config/env'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'

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

export function AccountPage() {
  const { dateStyle, language, refreshInterval, setDateStyle, setLanguage, setRefreshInterval, t } = useI18n()
  const user = useAuthStore((state) => state.user)
  const isLive = env.apiMode === 'live'
  const formError = (message?: string) => (message ? t(message) : undefined)
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
      toast.success(t('Password updated'))
      form.reset({
        currentPassword: '',
        newPassword: '',
        confirmPassword: '',
      })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to update password'))
    },
  })

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow={t('Account')}
        title={t('Identity and password')}
        description={t('Review the active console identity and change the current password without involving another administrator.')}
      />

      <div className="grid gap-4 xl:grid-cols-[340px_minmax(0,1fr)_360px]">
        <Card className="space-y-4">
          <div>
            <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">{t('Current session')}</p>
            <h2 className="mt-3 text-2xl font-semibold text-ink-950">{user?.username ?? t('Unknown user')}</h2>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge tone="neutral">{t('Signed in')}</Badge>
            <Badge tone={user?.role === 'admin' ? 'warning' : user?.role === 'operator' ? 'success' : 'neutral'}>
              {formatRoleLabel(user?.role, t)}
            </Badge>
          </div>
          <p className="text-sm text-ink-800/75">
            {t('This view is available to administrators, operators and viewers. Only the current password is accepted for self-service password rotation.')}
          </p>
        </Card>

        <Card className="space-y-5">
          <div>
            <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">{t('Password')}</p>
            <h3 className="mt-3 text-2xl font-semibold text-ink-950">{t('Change your password')}</h3>
          </div>

          <form className="space-y-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="account-current-password">
                {t('Current password')}
              </label>
              <Input id="account-current-password" type="password" placeholder={t('Enter current password')} {...form.register('currentPassword')} />
              {form.formState.errors.currentPassword ? (
                <p className="text-sm text-danger-500">{formError(form.formState.errors.currentPassword.message)}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="account-new-password">
                {t('New password')}
              </label>
              <Input id="account-new-password" type="password" placeholder={t('Choose a stronger password')} {...form.register('newPassword')} />
              {form.formState.errors.newPassword ? (
                <p className="text-sm text-danger-500">{formError(form.formState.errors.newPassword.message)}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-ink-900" htmlFor="account-confirm-password">
                {t('Confirm new password')}
              </label>
              <Input id="account-confirm-password" type="password" placeholder={t('Repeat the new password')} {...form.register('confirmPassword')} />
              {form.formState.errors.confirmPassword ? (
                <p className="text-sm text-danger-500">{formError(form.formState.errors.confirmPassword.message)}</p>
              ) : null}
            </div>

            <Button type="submit" disabled={!form.formState.isValid || mutation.isPending}>
              {mutation.isPending ? t('Updating...') : t('Update password')}
            </Button>
          </form>
        </Card>

        <div className="space-y-4">
          <Card className="space-y-4">
            <div>
              <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">{t('Interface preferences')}</p>
              <h3 className="mt-3 text-2xl font-semibold text-ink-950">{t('Language settings')}</h3>
              <p className="mt-2 text-sm text-ink-800/75">
                {t('Tune the console to the way you read operational data.')}
              </p>
            </div>

            <div className="space-y-3">
              <label className="block space-y-2 text-sm font-medium text-ink-900">
                <span>{t('Language')}</span>
                <Select value={language} onChange={(event) => setLanguage(event.target.value as Language)}>
                  {availableLanguages.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </Select>
              </label>

              <label className="block space-y-2 text-sm font-medium text-ink-900">
                <span>{t('Date and time format')}</span>
                <Select value={dateStyle} onChange={(event) => setDateStyle(event.target.value as DateStyle)}>
                  <option value="regional">{t('Regional format')}</option>
                  <option value="compact">{t('Compact format')}</option>
                </Select>
              </label>
            </div>
          </Card>

          <Card className="space-y-4">
            <div>
              <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">{t('Data refresh')}</p>
              <h3 className="mt-3 text-xl font-semibold text-ink-950">{t('Auto-refresh cadence')}</h3>
              <p className="mt-2 text-sm text-ink-800/75">
                {t('Choose how often live pages ask the API for fresh data.')}
              </p>
            </div>
            <label className="block space-y-2 text-sm font-medium text-ink-900">
              <span>{t('Auto-refresh cadence')}</span>
              <Select
                value={refreshInterval}
                onChange={(event) => setRefreshInterval(event.target.value as RefreshInterval)}
                disabled={!isLive}
              >
                {availableRefreshIntervals.map((option) => (
                  <option key={option.value} value={option.value}>
                    {t(option.label)}
                  </option>
                ))}
              </Select>
            </label>
            <div className="mt-2 rounded-2xl border border-slate-200/80 bg-white/70 p-4 text-sm text-ink-800/75">
              <p>{t('Applies to dashboard, agents, approvals, policies, jobs, and backups.')}</p>
              <p className="mt-3 font-semibold text-ink-950">
                {isLive ? t('Live data') : t('Demo data')}
              </p>
            </div>
          </Card>
        </div>
      </div>
    </div>
  )
}
