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
