import apiClient from './client'

export interface LoginRequest {
  username: string
  password: string
}

export interface User {
  id: string
  username: string
  role: 'admin' | 'operator' | 'viewer'
  isActive: boolean
}

export interface AuthResponse {
  token: string
  user: User
}

type ApiUser = Omit<User, 'role'> & {
  role: string
}

export function normalizeAuthRole(role: string | undefined): User['role'] {
  switch (role?.toLowerCase()) {
    case 'admin':
      return 'admin'
    case 'operator':
      return 'operator'
    default:
      return 'viewer'
  }
}

export function normalizeAuthUser(user: ApiUser): User {
  return {
    ...user,
    role: normalizeAuthRole(user.role),
  }
}

export async function login(data: LoginRequest): Promise<AuthResponse> {
  const response = await apiClient.post<{ accessToken: string; token?: string; user: ApiUser }>('/api/auth/login', data)
  return {
    token: response.data.accessToken ?? response.data.token ?? '',
    user: normalizeAuthUser(response.data.user),
  }
}

export async function getAuthUser(): Promise<User> {
  const response = await apiClient.get<ApiUser>('/api/auth/me')
  return normalizeAuthUser(response.data)
}

export async function logout(): Promise<void> {
  await Promise.resolve()
}

export async function changeOwnPassword(currentPassword: string, newPassword: string): Promise<void> {
  await apiClient.post('/api/auth/change-password', {
    currentPassword,
    newPassword,
  })
}
