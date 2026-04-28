export type UserRole = 'viewer' | 'operator' | 'admin'

export type AdminUser = {
  id: string
  username: string
  role: UserRole
  isActive: boolean
  createdAtUtc: string
}

export type CreateUserInput = {
  username: string
  password: string
  role: UserRole
}
