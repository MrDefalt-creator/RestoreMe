import { createContext, useContext, useEffect, useMemo, useState } from 'react'

export type Theme = 'light' | 'dark'
export type ThemeMode = 'light' | 'dark' | 'system'

type ThemeProviderProps = {
  children: React.ReactNode
  defaultMode?: ThemeMode
  storageKey?: string
}

type ThemeProviderState = {
  /** Effective theme actually applied to the DOM. Always concrete. */
  theme: Theme
  /** User preference. `'system'` follows the OS preference. */
  themeMode: ThemeMode
  /** Set the preference. Pass `'system'` to follow the OS. */
  setThemeMode: (mode: ThemeMode) => void
}

const initialState: ThemeProviderState = {
  theme: 'light',
  themeMode: 'system',
  setThemeMode: () => null,
}

const ThemeProviderContext = createContext<ThemeProviderState>(initialState)

function readSystemPreference(): Theme {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return 'light'
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function readStoredMode(storageKey: string, fallback: ThemeMode): ThemeMode {
  if (typeof window === 'undefined') return fallback
  const stored = window.localStorage.getItem(storageKey)
  if (stored === 'light' || stored === 'dark' || stored === 'system') {
    return stored
  }
  return fallback
}

export function ThemeProvider({
  children,
  defaultMode = 'system',
  storageKey = 'theme-preference',
  ...props
}: ThemeProviderProps) {
  const [themeMode, setThemeMode] = useState<ThemeMode>(() => readStoredMode(storageKey, defaultMode))
  const [systemTheme, setSystemTheme] = useState<Theme>(() => readSystemPreference())

  // Subscribe to OS theme changes while mode is 'system'. Listener attaches
  // unconditionally — it's cheap, and the mode dependency would otherwise
  // miss the transition back to 'system' if the user toggles modes
  // mid-session.
  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = (event: MediaQueryListEvent) => setSystemTheme(event.matches ? 'dark' : 'light')
    media.addEventListener('change', handler)
    return () => media.removeEventListener('change', handler)
  }, [])

  const effectiveTheme: Theme = themeMode === 'system' ? systemTheme : themeMode

  useEffect(() => {
    const root = window.document.documentElement
    root.classList.remove('light', 'dark')
    root.classList.add(effectiveTheme)
    root.setAttribute('data-theme', effectiveTheme)
    window.localStorage.setItem(storageKey, themeMode)
  }, [effectiveTheme, themeMode, storageKey])

  const value = useMemo<ThemeProviderState>(() => ({
    theme: effectiveTheme,
    themeMode,
    setThemeMode,
  }), [effectiveTheme, themeMode])

  return (
    <ThemeProviderContext.Provider {...props} value={value}>
      {children}
    </ThemeProviderContext.Provider>
  )
}

export const useTheme = () => {
  const context = useContext(ThemeProviderContext)

  if (context === undefined)
    throw new Error('useTheme must be used within a ThemeProvider')

  return context
}
