import { http } from '@/shared/api/http'
import type { AuthUser } from '@/app/store/auth-store'
import { toAuthUser } from '@/app/store/auth-store'

type LoginResponse = {
  accessToken: string
  expiresAtUtc: string
  user: {
    id: string
    username: string
    role: string
  }
}

export async function login(username: string, password: string): Promise<{ accessToken: string; user: AuthUser }> {
  const response = await http.post<LoginResponse>('/api/auth/login', {
    username,
    password,
  })

  return {
    accessToken: response.data.accessToken,
    user: toAuthUser(response.data.user),
  }
}

export async function changeOwnPassword(currentPassword: string, newPassword: string) {
  await http.post('/api/auth/change-password', {
    currentPassword,
    newPassword,
  })
}
