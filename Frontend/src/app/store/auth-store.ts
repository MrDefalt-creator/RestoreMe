import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

export type AuthRole = 'viewer' | 'operator' | 'admin'

export type AuthUser = {
  id: string
  username: string
  role: AuthRole
}

type AuthStore = {
  accessToken: string | null
  user: AuthUser | null
  rememberMe: boolean
  setSession: (accessToken: string, user: AuthUser, rememberMe: boolean) => void
  clearSession: () => void
}

const storageKey = 'restoreme-auth-store'

const authStateStorage = {
  getItem: (name: string) => {
    const localValue = localStorage.getItem(name)
    if (localValue) {
      return localValue
    }

    return sessionStorage.getItem(name)
  },
  setItem: (name: string, value: string) => {
    const parsed = JSON.parse(value) as { state?: { rememberMe?: boolean } }
    const shouldRemember = parsed.state?.rememberMe === true

    if (shouldRemember) {
      localStorage.setItem(name, value)
      sessionStorage.removeItem(name)
      return
    }

    sessionStorage.setItem(name, value)
    localStorage.removeItem(name)
  },
  removeItem: (name: string) => {
    localStorage.removeItem(name)
    sessionStorage.removeItem(name)
  },
}

function normalizeRole(role: string): AuthRole {
  const normalized = role.trim().toLowerCase()

  if (normalized === 'admin' || normalized === 'operator' || normalized === 'viewer') {
    return normalized
  }

  return 'viewer'
}

export function toAuthUser(user: { id: string; username: string; role: string }): AuthUser {
  return {
    id: user.id,
    username: user.username,
    role: normalizeRole(user.role),
  }
}

export const useAuthStore = create<AuthStore>()(
  persist(
    (set) => ({
      accessToken: null,
      user: null,
      rememberMe: true,
      setSession: (accessToken, user, rememberMe) => set({ accessToken, user, rememberMe }),
      clearSession: () => set({ accessToken: null, user: null, rememberMe: true }),
    }),
    {
      name: storageKey,
      storage: createJSONStorage(() => authStateStorage),
    },
  ),
)
