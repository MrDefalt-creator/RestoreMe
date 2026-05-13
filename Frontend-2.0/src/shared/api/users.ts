import apiClient from './client'

export type UserRole = 'admin' | 'operator' | 'viewer'

export interface User {
  id: string
  username: string
  role: UserRole
  isActive: boolean
  createdAtUtc?: string
  lastSeenAt?: string | null
}

export type CreateUserInput = {
  username: string
  password: string
  role: UserRole
}

function normalizeRole(role: string): UserRole {
  const normalized = role.trim().toLowerCase()
  if (normalized === 'admin' || normalized === 'operator' || normalized === 'viewer') {
    return normalized
  }
  return 'viewer'
}

function normalizeUser(user: {
  id: string
  username: string
  role: string
  isActive: boolean
  createdAtUtc?: string
  lastSeenAt?: string | null
}): User {
  return {
    ...user,
    role: normalizeRole(user.role),
  }
}

export async function getUsers(): Promise<User[]> {
  const response = await apiClient.get<User[]>('/api/users')
  return response.data.map(normalizeUser)
}

export async function getUserById(userId: string): Promise<User> {
  const response = await apiClient.get<User>(`/api/users/${userId}`)
  return normalizeUser(response.data)
}

export async function createUser(user: CreateUserInput): Promise<User> {
  const response = await apiClient.post<User>('/api/users', user)
  return normalizeUser(response.data)
}

export async function updateUserRole(userId: string, role: UserRole): Promise<User> {
  const response = await apiClient.patch<User>(`/api/users/${userId}/role`, { role })
  return normalizeUser(response.data)
}

export async function updateUserStatus(userId: string, isActive: boolean): Promise<User> {
  const response = await apiClient.patch<User>(`/api/users/${userId}/status`, { isActive })
  return normalizeUser(response.data)
}

export async function updateUser(userId: string, user: Partial<User>): Promise<User> {
  if (typeof user.role !== 'undefined') {
    return updateUserRole(userId, user.role)
  }
  if (typeof user.isActive !== 'undefined') {
    return updateUserStatus(userId, user.isActive)
  }
  throw new Error('No supported user fields were provided.')
}

export async function setUserPassword(userId: string, newPassword: string): Promise<void> {
  await apiClient.patch(`/api/users/${userId}/password`, { newPassword })
}

export async function deleteUser(userId: string): Promise<void> {
  await apiClient.delete(`/api/users/${userId}`)
}
