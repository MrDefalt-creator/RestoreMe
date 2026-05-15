import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { useTheme } from '@/app/providers/ThemeProvider'
import { changeOwnPassword } from '@/shared/api/auth'
import { env } from '@/shared/config/env'
import {
  availableLanguages,
  availableRefreshIntervals,
  formatRoleLabel,
  useI18n,
  type DateStyle,
  type Language,
  type RefreshInterval,
} from '@/shared/i18n'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'

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

export function AccountPage() {
  const { dateStyle, language, refreshInterval, setDateStyle, setLanguage, setRefreshInterval, t } = useI18n()
  const { theme, setTheme } = useTheme()
  const user = useAuthStore((state) => state.user)
  const formError = (message?: string) => (message ? t(message) : undefined)
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
      toast.success(t('Password updated'))
      form.reset()
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
        description={t('Review the active console identity and rotate your own password safely.')}
      />

      <div className="grid gap-5 xl:grid-cols-[360px_minmax(0,1fr)_360px]">
        <Card>
          <CardHeader>
            <CardTitle>{t('Current session')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <p className="text-2xl font-semibold tracking-tight text-foreground">
                {user?.username ?? t('Unknown user')}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                {t('Signed in to RestoreMe')}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="success">{t('Signed in')}</Badge>
              <Badge variant={user?.role === 'admin' ? 'warning' : user?.role === 'operator' ? 'success' : 'neutral'}>
                {formatRoleLabel(user?.role, t)}
              </Badge>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('Change password')}</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
              <Field label={t('Current password')} error={formError(form.formState.errors.currentPassword?.message)}>
                <Input type="password" placeholder={t('Enter current password')} {...form.register('currentPassword')} />
              </Field>
              <Field label={t('New password')} error={formError(form.formState.errors.newPassword?.message)}>
                <Input type="password" placeholder={t('Choose a stronger password')} {...form.register('newPassword')} />
              </Field>
              <Field label={t('Confirm new password')} error={formError(form.formState.errors.confirmPassword?.message)}>
                <Input type="password" placeholder={t('Repeat the new password')} {...form.register('confirmPassword')} />
              </Field>
              <Button type="submit" disabled={!form.formState.isValid || mutation.isPending}>
                {mutation.isPending ? t('Updating...') : t('Update password')}
              </Button>
            </form>
          </CardContent>
        </Card>

        <div className="space-y-5">
          <Card>
            <CardHeader>
              <CardTitle>{t('Interface preferences')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-muted-foreground">
                {t('Choose how RestoreMe looks and formats operational timestamps on this device.')}
              </p>

              <Field label={t('Language')}>
                <Select value={language} onChange={(event) => setLanguage(event.target.value as Language)}>
                  {availableLanguages.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </Select>
              </Field>

              <Field label={t('Appearance')}>
                <Select value={theme} onChange={(event) => setTheme(event.target.value as 'light' | 'dark')}>
                  <option value="light">{t('Light theme')}</option>
                  <option value="dark">{t('Dark theme')}</option>
                </Select>
              </Field>

              <Field label={t('Date and time format')}>
                <Select value={dateStyle} onChange={(event) => setDateStyle(event.target.value as DateStyle)}>
                  <option value="regional">{t('Regional format')}</option>
                  <option value="compact">{t('Compact format')}</option>
                </Select>
              </Field>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t('Data refresh')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-muted-foreground">
                {t('Choose how often live pages ask the API for fresh data.')}
              </p>

              <Field label={t('Auto-refresh cadence')}>
                <Select
                  value={refreshInterval}
                  onChange={(event) => setRefreshInterval(event.target.value as RefreshInterval)}
                  disabled={!env.isLive}
                >
                  {availableRefreshIntervals.map((option) => (
                    <option key={option.value} value={option.value}>
                      {t(option.label)}
                    </option>
                  ))}
                </Select>
              </Field>

              <div className="mt-1 rounded-lg border border-border bg-background/60 p-4 text-sm text-muted-foreground">
                <p>{t('Applies to dashboard, agents, approvals, policies, jobs, and backups.')}</p>
                <p className="mt-3 font-semibold text-foreground">
                  {env.isLive ? t('Live data') : t('Demo data')}
                </p>
              </div>
            </CardContent>
          </Card>
        </div>
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
