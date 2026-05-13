import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { Lock, Mail, ShieldCheck } from 'lucide-react'

import { login } from '@/shared/api/auth'
import { useAuthStore } from '@/app/store/auth-store'
import { BrandMark } from '@/shared/ui/BrandMark'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'

type LoginFormData = {
  username: string
  password: string
  rememberMe: boolean
}

export function LoginPage() {
  const navigate = useNavigate()
  const { setSession } = useAuthStore()
  const form = useForm<LoginFormData>({
    mode: 'onChange',
    defaultValues: {
      username: '',
      password: '',
      rememberMe: false,
    },
  })

  const mutation = useMutation({
    mutationFn: async (data: LoginFormData) => {
      const response = await login({
        username: data.username,
        password: data.password,
      })
      if (data.rememberMe) {
        localStorage.setItem('remember-me', 'true')
      }
      return response
    },
    onSuccess: (response) => {
      setSession(response.token, response.user)
      toast.success(`Welcome, ${response.user.username}!`)
      navigate('/', { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Sign in failed')
    },
  })

  return (
    <div className="min-h-screen px-4 py-10">
      <div className="mx-auto grid min-h-[calc(100vh-5rem)] w-full max-w-5xl items-center gap-10 lg:grid-cols-[1fr_440px]">
        <section className="hidden space-y-6 lg:block">
          <BrandMark subtitle="RestoreMe" />
          <div className="max-w-xl space-y-4">
            <p className="text-sm font-medium uppercase tracking-[0.16em] text-muted-foreground">
              Calm backup confidence
            </p>
            <h1 className="text-5xl font-semibold leading-tight tracking-tight text-foreground">
              Know your data is protected before you need it.
            </h1>
            <p className="text-base leading-7 text-muted-foreground">
              A focused workspace for agents, policies, backup jobs and recovery artifacts.
            </p>
          </div>
          <div className="inline-flex items-center gap-3 rounded-lg border border-border bg-card px-4 py-3 text-sm text-muted-foreground shadow-[var(--shadow-md)]">
            <ShieldCheck className="h-4 w-4 text-success" />
            Designed for quiet, deliberate backup operations.
          </div>
        </section>

        <section className="mx-auto w-full max-w-md space-y-6">
          <div className="space-y-4 text-center lg:hidden">
            <div className="flex justify-center">
              <BrandMark compact />
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">RestoreMe</p>
              <h1 className="mt-1 text-3xl font-semibold tracking-tight text-foreground">
                Backup console
              </h1>
            </div>
          </div>

          <div className="rounded-lg border border-border bg-card p-6 shadow-[var(--shadow-xl)]">
            <div className="space-y-6">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                  Sign in
                </p>
                <h2 className="mt-3 text-3xl font-semibold tracking-tight text-foreground">
                  Access the console
                </h2>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  Use your operator account to manage backup protection.
                </p>
              </div>

              <form
                className="space-y-5"
                onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
              >
                <div className="space-y-2">
                  <label className="text-sm font-medium text-foreground" htmlFor="login-username">
                    Username
                  </label>
                  <div className="relative">
                    <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      id="login-username"
                      placeholder="admin"
                      className="pl-10"
                      {...form.register('username', { required: 'Username is required' })}
                    />
                  </div>
                  {form.formState.errors.username && (
                    <p className="text-sm text-destructive">{form.formState.errors.username.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium text-foreground" htmlFor="login-password">
                    Password
                  </label>
                  <div className="relative">
                    <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      id="login-password"
                      type="password"
                      placeholder="Enter password"
                      className="pl-10"
                      {...form.register('password', { required: 'Password is required' })}
                    />
                  </div>
                  {form.formState.errors.password && (
                    <p className="text-sm text-destructive">{form.formState.errors.password.message}</p>
                  )}
                </div>

                <label className="flex items-center gap-3 rounded-lg border border-border bg-background/70 px-4 py-3 text-sm text-muted-foreground">
                  <input
                    type="checkbox"
                    className="h-4 w-4 rounded border-border accent-[hsl(var(--primary))]"
                    checked={form.watch('rememberMe')}
                    onChange={(event) => form.setValue('rememberMe', event.target.checked)}
                  />
                  <span>Remember me on this device</span>
                </label>

                <Button
                  type="submit"
                  className="h-11 w-full justify-center"
                  disabled={!form.formState.isValid || mutation.isPending}
                >
                  {mutation.isPending ? 'Signing in...' : 'Sign in'}
                </Button>
              </form>
            </div>
          </div>
        </section>
      </div>
    </div>
  )
}
