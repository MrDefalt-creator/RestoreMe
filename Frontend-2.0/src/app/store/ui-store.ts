import { create } from 'zustand'

type SidebarState = 'collapsed' | 'expanded'

interface UiStore {
  sidebarState: SidebarState
  setSidebarState: (state: SidebarState) => void
  toggleSidebar: () => void
  policyFilter: 'all' | 'enabled' | 'disabled'
  setPolicyFilter: (filter: 'all' | 'enabled' | 'disabled') => void
}

const getStoredState = (): SidebarState => {
  const stored = localStorage.getItem('ui:sidebar')
  return stored ? (stored as SidebarState) : 'expanded'
}

const getStoredFilter = (): 'all' | 'enabled' | 'disabled' => {
  const stored = localStorage.getItem('ui:policyFilter')
  return stored ? (stored as 'all' | 'enabled' | 'disabled') : 'all'
}

export const useUiStore = create<UiStore>((set) => ({
  sidebarState: getStoredState(),
  setSidebarState: (state) => set({ sidebarState: state }),
  toggleSidebar: () =>
    set((state) => ({
      sidebarState: state.sidebarState === 'expanded' ? 'collapsed' : 'expanded',
    })),
  policyFilter: getStoredFilter(),
  setPolicyFilter: (filter) => set({ policyFilter: filter }),
}))

// Persist sidebar state
useUiStore.subscribe((state) => {
  localStorage.setItem('ui:sidebar', state.sidebarState)
})

// Persist policy filter
useUiStore.subscribe((state) => {
  localStorage.setItem('ui:policyFilter', state.policyFilter)
})
