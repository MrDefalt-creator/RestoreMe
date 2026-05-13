export interface User {
  id: string
  username: string
  role: 'admin' | 'operator' | 'viewer'
  enabled: boolean
}

export interface AuthResponse {
  token: string
  user: User
}
