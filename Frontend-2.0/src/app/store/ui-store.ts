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
  activeRestoreJobId: string | null
  setActiveRestoreJobId: (id: string | null) => void
  firstRunDismissed: boolean
  setFirstRunDismissed: (dismissed: boolean) => void
  keyboardSheetOpen: boolean
  setKeyboardSheetOpen: (open: boolean) => void
  onboardingSeen: boolean
  setOnboardingSeen: (seen: boolean) => void
  onboardingDone: boolean
  setOnboardingDone: (done: boolean) => void
  onboardingModalOpen: boolean
  setOnboardingModalOpen: (open: boolean) => void
  onboardingWidgetExpanded: boolean
  setOnboardingWidgetExpanded: (expanded: boolean) => void
  reopenOnboardingTour: () => void
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

const getStoredFirstRunDismissed = (): boolean => {
  return localStorage.getItem('ui:firstRunDismissed') === 'true'
}

const getStoredOnboardingSeen = (): boolean => {
  return localStorage.getItem('onboarding_seen') === 'true'
}

const getStoredOnboardingDone = (): boolean => {
  return localStorage.getItem('onboarding_done') === 'true'
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
  activeRestoreJobId: null,
  setActiveRestoreJobId: (id) => set({ activeRestoreJobId: id }),
  firstRunDismissed: getStoredFirstRunDismissed(),
  setFirstRunDismissed: (dismissed) => set({ firstRunDismissed: dismissed }),
  keyboardSheetOpen: false,
  setKeyboardSheetOpen: (open) => set({ keyboardSheetOpen: open }),
  onboardingSeen: getStoredOnboardingSeen(),
  setOnboardingSeen: (seen) => set({ onboardingSeen: seen }),
  onboardingDone: getStoredOnboardingDone(),
  setOnboardingDone: (done) => set({ onboardingDone: done }),
  onboardingModalOpen: false,
  setOnboardingModalOpen: (open) => set({ onboardingModalOpen: open }),
  onboardingWidgetExpanded: false,
  setOnboardingWidgetExpanded: (expanded) => set({ onboardingWidgetExpanded: expanded }),
  reopenOnboardingTour: () =>
    set({ onboardingModalOpen: true, onboardingDone: false, onboardingSeen: false, onboardingWidgetExpanded: false }),
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

// Persist firstRunDismissed
useUiStore.subscribe((state) => {
  localStorage.setItem('ui:firstRunDismissed', String(state.firstRunDismissed))
})

// Persist onboarding flags
useUiStore.subscribe((state) => {
  localStorage.setItem('onboarding_seen', String(state.onboardingSeen))
})

useUiStore.subscribe((state) => {
  localStorage.setItem('onboarding_done', String(state.onboardingDone))
})
