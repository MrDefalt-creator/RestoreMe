import { create } from 'zustand'
import { normalizeAuthUser, type User } from '@/shared/api/auth'

interface AuthStore {
  accessToken: string | null
  user: User | null
  setSession: (token: string | null, user: User | null) => void
  clearSession: () => void
}

const getStoredState = (): { accessToken: string | null; user: User | null } => {
  const stored = localStorage.getItem('auth:session')
  if (!stored) return { accessToken: null, user: null }

  try {
    const data = JSON.parse(stored)
    const accessToken = data.accessToken ?? data.token ?? null

    if (!accessToken) {
      return { accessToken: null, user: null }
    }

    return { accessToken, user: data.user ? normalizeAuthUser(data.user) : null }
  } catch {
    return { accessToken: null, user: null }
  }
}

export const useAuthStore = create<AuthStore>((set) => ({
  accessToken: null,
  user: null,
  setSession: (token, user) => {
    localStorage.setItem('auth:session', JSON.stringify({ accessToken: token, user }))
    set({ accessToken: token, user })
  },
  clearSession: () => {
    localStorage.removeItem('auth:session')
    set({ accessToken: null, user: null })
  },
}))

// Restore persisted state on mount
useAuthStore.setState(getStoredState())
