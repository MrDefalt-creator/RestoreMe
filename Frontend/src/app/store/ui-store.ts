import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

type SidebarState = 'expanded' | 'collapsed'
type PolicyFilter = 'all' | 'enabled' | 'disabled'
type JobFilter = 'all' | 'running' | 'completed' | 'failed'

type UiStore = {
  sidebarState: SidebarState
  selectedAgentId: string | null
  selectedJobId: string | null
  policyFilter: PolicyFilter
  jobFilter: JobFilter
  toggleSidebar: () => void
  setSelectedAgentId: (agentId: string | null) => void
  setSelectedJobId: (jobId: string | null) => void
  setPolicyFilter: (filter: PolicyFilter) => void
  setJobFilter: (filter: JobFilter) => void
}

export const useUiStore = create<UiStore>()(
  persist(
    (set) => ({
      sidebarState: 'expanded',
      selectedAgentId: null,
      selectedJobId: null,
      policyFilter: 'all',
      jobFilter: 'all',
      toggleSidebar: () =>
        set((state) => ({
          sidebarState:
            state.sidebarState === 'expanded' ? 'collapsed' : 'expanded',
        })),
      setSelectedAgentId: (selectedAgentId) => set({ selectedAgentId }),
      setSelectedJobId: (selectedJobId) => set({ selectedJobId }),
      setPolicyFilter: (policyFilter) => set({ policyFilter }),
      setJobFilter: (jobFilter) => set({ jobFilter }),
    }),
    {
      name: 'restoreme-ui-store',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        sidebarState: state.sidebarState,
        policyFilter: state.policyFilter,
        jobFilter: state.jobFilter,
      }),
    },
  ),
)
