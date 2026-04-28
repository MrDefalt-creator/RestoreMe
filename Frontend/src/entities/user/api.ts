import { http } from '@/shared/api/http'
import type { CreateUserInput, AdminUser, UserRole } from '@/entities/user/model/types'

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
  createdAtUtc: string
}): AdminUser {
  return {
    id: user.id,
    username: user.username,
    role: normalizeRole(user.role),
    isActive: user.isActive,
    createdAtUtc: user.createdAtUtc,
  }
}

export async function getUsers() {
  const response = await http.get<
    Array<{
      id: string
      username: string
      role: string
      isActive: boolean
      createdAtUtc: string
    }>
  >('/api/users')

  return response.data.map(normalizeUser)
}

export async function createUser(input: CreateUserInput) {
  const response = await http.post<{
    id: string
    username: string
    role: string
    isActive: boolean
    createdAtUtc: string
  }>('/api/users', {
    username: input.username,
    password: input.password,
    role: input.role,
  })

  return normalizeUser(response.data)
}

export async function updateUserRole(userId: string, role: UserRole) {
  const response = await http.patch<{
    id: string
    username: string
    role: string
    isActive: boolean
    createdAtUtc: string
  }>(`/api/users/${userId}/role`, {
    role,
  })

  return normalizeUser(response.data)
}

export async function updateUserStatus(userId: string, isActive: boolean) {
  const response = await http.patch<{
    id: string
    username: string
    role: string
    isActive: boolean
    createdAtUtc: string
  }>(`/api/users/${userId}/status`, {
    isActive,
  })

  return normalizeUser(response.data)
}

export async function setUserPassword(userId: string, newPassword: string) {
  await http.patch(`/api/users/${userId}/password`, {
    newPassword,
  })
}

export async function deleteUser(userId: string) {
  await http.delete(`/api/users/${userId}`)
}
