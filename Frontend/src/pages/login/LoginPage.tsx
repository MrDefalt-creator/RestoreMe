import { Navigate, useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { login } from '@/entities/auth/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'

const loginSchema = z.object({
  username: z.string().trim().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
  rememberMe: z.boolean(),
})

type LoginValues = z.infer<typeof loginSchema>

export function LoginPage() {
  const navigate = useNavigate()
  const accessToken = useAuthStore((state) => state.accessToken)
  const setSession = useAuthStore((state) => state.setSession)
  const rememberMe = useAuthStore((state) => state.rememberMe)
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    mode: 'onChange',
    defaultValues: {
      username: '',
      password: '',
      rememberMe,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: LoginValues) => login(values.username, values.password),
    onSuccess: (result, variables) => {
      setSession(result.accessToken, result.user, variables.rememberMe)
      toast.success('Signed in successfully')
      navigate('/', { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Sign in failed')
    },
  })

  if (accessToken) {
    return <Navigate to="/" replace />
  }

  return (
    <div className="min-h-screen bg-[linear-gradient(180deg,rgba(248,251,253,0.94),rgba(237,244,250,0.98))] px-4 py-12">
      <div className="mx-auto flex min-h-[calc(100vh-6rem)] max-w-5xl items-center justify-center">
        <Card className="grid w-full max-w-4xl gap-8 overflow-hidden border-white/70 bg-white/88 p-0 shadow-[0_32px_80px_rgba(16,32,51,0.12)] md:grid-cols-[1.05fr_0.95fr]">
          <div className="bg-[linear-gradient(180deg,rgba(11,24,39,0.98),rgba(16,46,72,0.96))] px-8 py-10 text-white md:px-10 md:py-12">
            <p className="text-xs font-semibold uppercase tracking-[0.3em] text-sky-200/85">
              RestoreMe
            </p>
            <h1 className="mt-4 text-3xl font-semibold tracking-tight">
              Backup Console
            </h1>
            <p className="mt-4 max-w-md text-sm leading-7 text-sky-100/78">
              Secure operator access for agents, backup policies, jobs and stored artifacts.
            </p>
            <div className="mt-8 space-y-3 text-sm text-sky-100/74">
              <p>Use your assigned operator or admin account to enter the console.</p>
              <p>The development profile can bootstrap demo users through backend configuration.</p>
            </div>
          </div>

          <div className="px-6 py-8 md:px-10 md:py-12">
            <div className="max-w-md space-y-6">
              <div>
                <p className="text-sm font-semibold uppercase tracking-[0.28em] text-ink-800/55">
                  Sign in
                </p>
                <h2 className="mt-3 text-3xl font-semibold tracking-tight text-ink-950">
                  Access the workspace
                </h2>
              </div>

              <form
                className="space-y-5"
                onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
              >
                <div className="space-y-2">
                  <label className="text-sm font-medium text-ink-900" htmlFor="login-username">
                    Username
                  </label>
                  <Input id="login-username" placeholder="admin" {...form.register('username')} />
                  {form.formState.errors.username ? (
                    <p className="text-sm text-danger-500">{form.formState.errors.username.message}</p>
                  ) : null}
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium text-ink-900" htmlFor="login-password">
                    Password
                  </label>
                  <Input id="login-password" type="password" placeholder="Enter password" {...form.register('password')} />
                  {form.formState.errors.password ? (
                    <p className="text-sm text-danger-500">{form.formState.errors.password.message}</p>
                  ) : null}
                </div>

                <label className="flex items-center gap-3 rounded-2xl border border-surface-200 bg-surface-50/70 px-4 py-3 text-sm text-ink-900">
                  <input
                    type="checkbox"
                    className="h-4 w-4 rounded border-surface-300 text-[#102033] focus:ring-[#102033]"
                    {...form.register('rememberMe')}
                  />
                  <span>Remember me on this device</span>
                </label>

                <Button type="submit" className="w-full justify-center" disabled={!form.formState.isValid || mutation.isPending}>
                  Sign in
                </Button>
              </form>
            </div>
          </div>
        </Card>
      </div>
    </div>
  )
}
