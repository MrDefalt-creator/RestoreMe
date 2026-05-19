import { create } from 'zustand'
import { normalizeAuthUser, type User } from '@/shared/api/auth'

interface AuthStore {
  user: User | null
  setSession: (user: User | null, rememberMe: boolean) => void
  clearSession: () => void
}

const STORAGE_KEY = 'auth:session'

const readStoredState = (): { user: User | null } => {
  const stored = localStorage.getItem(STORAGE_KEY) ?? sessionStorage.getItem(STORAGE_KEY)
  if (!stored) return { user: null }
  try {
    const data = JSON.parse(stored)
    return { user: data.user ? normalizeAuthUser(data.user) : null }
  } catch {
    return { user: null }
  }
}

const writeStoredState = (user: User | null, rememberMe: boolean) => {
  const payload = JSON.stringify({ user })
  if (rememberMe) {
    localStorage.setItem(STORAGE_KEY, payload)
    sessionStorage.removeItem(STORAGE_KEY)
  } else {
    sessionStorage.setItem(STORAGE_KEY, payload)
    localStorage.removeItem(STORAGE_KEY)
  }
}

const clearStoredState = () => {
  localStorage.removeItem(STORAGE_KEY)
  sessionStorage.removeItem(STORAGE_KEY)
}

export const useAuthStore = create<AuthStore>((set) => ({
  user: null,
  setSession: (user, rememberMe) => {
    writeStoredState(user, rememberMe)
    set({ user })
  },
  clearSession: () => {
    clearStoredState()
    set({ user: null })
  },
}))

useAuthStore.setState(readStoredState())
