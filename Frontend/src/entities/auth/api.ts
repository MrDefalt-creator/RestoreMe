import { http } from '@/shared/api/http'
import type { AuthUser } from '@/app/store/auth-store'
import { toAuthUser } from '@/app/store/auth-store'

type LoginResponse = {
  user: {
    id: string
    username: string
    role: string
    mustChangePassword?: boolean
  }
}

export async function login(
  username: string,
  password: string,
  rememberMe: boolean,
): Promise<{ user: AuthUser }> {
  const response = await http.post<LoginResponse>('/api/auth/login', {
    username,
    password,
    rememberMe,
  })

  return {
    user: toAuthUser(response.data.user),
  }
}

export async function logout(): Promise<void> {
  await http.post('/api/auth/logout')
}

export async function changeOwnPassword(currentPassword: string, newPassword: string) {
  await http.post('/api/auth/change-password', {
    currentPassword,
    newPassword,
  })
}
