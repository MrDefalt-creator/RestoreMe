import { create } from 'zustand'

type SidebarState = 'collapsed' | 'expanded'
export type Density = 'comfy' | 'compact'

interface UiStore {
  sidebarState: SidebarState
  setSidebarState: (state: SidebarState) => void
  toggleSidebar: () => void
  mobileNavOpen: boolean
  setMobileNavOpen: (open: boolean) => void
  toggleMobileNav: () => void
  closeMobileNav: () => void
  policyFilter: 'all' | 'enabled' | 'disabled'
  setPolicyFilter: (filter: 'all' | 'enabled' | 'disabled') => void
  density: Density
  setDensity: (density: Density) => void
  commandPaletteOpen: boolean
  setCommandPaletteOpen: (open: boolean) => void
  installAgentDialogOpen: boolean
  setInstallAgentDialogOpen: (open: boolean) => void
}

const getStoredState = (): SidebarState => {
  const stored = localStorage.getItem('ui:sidebar')
  return stored ? (stored as SidebarState) : 'expanded'
}

const getStoredFilter = (): 'all' | 'enabled' | 'disabled' => {
  const stored = localStorage.getItem('ui:policyFilter')
  return stored ? (stored as 'all' | 'enabled' | 'disabled') : 'all'
}

const getStoredDensity = (): Density => {
  const stored = localStorage.getItem('ui:density')
  return stored === 'compact' ? 'compact' : 'comfy'
}

export const useUiStore = create<UiStore>((set) => ({
  sidebarState: getStoredState(),
  setSidebarState: (state) => set({ sidebarState: state }),
  toggleSidebar: () =>
    set((state) => ({
      sidebarState: state.sidebarState === 'expanded' ? 'collapsed' : 'expanded',
    })),
  mobileNavOpen: false,
  setMobileNavOpen: (open) => set({ mobileNavOpen: open }),
  toggleMobileNav: () => set((state) => ({ mobileNavOpen: !state.mobileNavOpen })),
  closeMobileNav: () => set({ mobileNavOpen: false }),
  policyFilter: getStoredFilter(),
  setPolicyFilter: (filter) => set({ policyFilter: filter }),
  density: getStoredDensity(),
  setDensity: (density) => set({ density }),
  commandPaletteOpen: false,
  setCommandPaletteOpen: (open) => set({ commandPaletteOpen: open }),
  installAgentDialogOpen: false,
  setInstallAgentDialogOpen: (open) => set({ installAgentDialogOpen: open }),
}))

// Persist sidebar state
useUiStore.subscribe((state) => {
  localStorage.setItem('ui:sidebar', state.sidebarState)
})

// Persist policy filter
useUiStore.subscribe((state) => {
  localStorage.setItem('ui:policyFilter', state.policyFilter)
})

// Persist density
useUiStore.subscribe((state) => {
  localStorage.setItem('ui:density', state.density)
})
