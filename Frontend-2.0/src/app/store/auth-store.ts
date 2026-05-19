import { create } from 'zustand'
import { normalizeAuthUser, type User } from '@/shared/api/auth'

interface AuthStore {
  accessToken: string | null
  user: User | null
  setSession: (token: string | null, user: User | null, rememberMe: boolean) => void
  clearSession: () => void
}

const STORAGE_KEY = 'auth:session'

const readStoredState = (): { accessToken: string | null; user: User | null } => {
  const stored = localStorage.getItem(STORAGE_KEY) ?? sessionStorage.getItem(STORAGE_KEY)
  if (!stored) return { accessToken: null, user: null }
  try {
    const data = JSON.parse(stored)
    const accessToken = data.accessToken ?? data.token ?? null
    if (!accessToken) return { accessToken: null, user: null }
    return { accessToken, user: data.user ? normalizeAuthUser(data.user) : null }
  } catch {
    return { accessToken: null, user: null }
  }
}

const writeStoredState = (token: string | null, user: User | null, rememberMe: boolean) => {
  const payload = JSON.stringify({ accessToken: token, user })
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
  accessToken: null,
  user: null,
  setSession: (token, user, rememberMe) => {
    writeStoredState(token, user, rememberMe)
    set({ accessToken: token, user })
  },
  clearSession: () => {
    clearStoredState()
    set({ accessToken: null, user: null })
  },
}))

useAuthStore.setState(readStoredState())
