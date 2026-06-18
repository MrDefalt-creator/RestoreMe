import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useUiStore } from '@/app/store/ui-store'

const VIM_ROUTES: Record<string, string> = {
  a: '/agents',
  p: '/policies',
  j: '/jobs',
  b: '/backups',
  d: '/',
}

export function useCommandPalette() {
  const navigate = useNavigate()
  const isOpen = useUiStore((state) => state.commandPaletteOpen)
  const setOpen = useUiStore((state) => state.setCommandPaletteOpen)

  useEffect(() => {
    let pendingG = false
    let gTimer: ReturnType<typeof setTimeout> | null = null

    function handler(event: KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key === 'k') {
        event.preventDefault()
        setOpen(true)
        return
      }

      const target = event.target as HTMLElement
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) {
        return
      }

      if (pendingG) {
        if (gTimer) clearTimeout(gTimer)
        pendingG = false
        const dest = VIM_ROUTES[event.key]
        if (dest) {
          event.preventDefault()
          navigate(dest)
        }
        return
      }

      if (event.key === 'g' && !event.metaKey && !event.ctrlKey && !event.altKey) {
        pendingG = true
        gTimer = setTimeout(() => { pendingG = false }, 700)
      }
    }

    window.addEventListener('keydown', handler)
    return () => {
      window.removeEventListener('keydown', handler)
      if (gTimer) clearTimeout(gTimer)
    }
  }, [navigate, setOpen])

  return {
    isOpen,
    open: () => setOpen(true),
    close: () => setOpen(false),
    toggle: () => setOpen(!isOpen),
  }
}
