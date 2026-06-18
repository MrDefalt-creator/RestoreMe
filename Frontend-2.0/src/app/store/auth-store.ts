import { create } from 'zustand'
import { normalizeAuthUser, type User } from '@/shared/api/auth'

interface AuthStore {
  user: User | null
  setSession: (user: User | null, rememberMe: boolean) => void
  updateUser: (user: User) => void
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

// Updates the cached user payload while keeping the existing storage location
// (localStorage for "remember me", sessionStorage otherwise) — used after
// password change so that flags like `mustChangePassword` toggle off without
// nuking the session.
const updateStoredUser = (user: User) => {
  const inLocal = localStorage.getItem(STORAGE_KEY)
  const inSession = sessionStorage.getItem(STORAGE_KEY)
  const payload = JSON.stringify({ user })
  if (inLocal) {
    localStorage.setItem(STORAGE_KEY, payload)
  } else if (inSession) {
    sessionStorage.setItem(STORAGE_KEY, payload)
  } else {
    sessionStorage.setItem(STORAGE_KEY, payload)
  }
}

export const useAuthStore = create<AuthStore>((set) => ({
  user: null,
  setSession: (user, rememberMe) => {
    writeStoredState(user, rememberMe)
    set({ user })
  },
  updateUser: (user) => {
    updateStoredUser(user)
    set({ user })
  },
  clearSession: () => {
    clearStoredState()
    set({ user: null })
  },
}))

useAuthStore.setState(readStoredState())
