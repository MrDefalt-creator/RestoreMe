import { useEffect } from 'react'

import { useAuthStore } from '@/app/store/auth-store'
import { useUiStore } from '@/app/store/ui-store'
import { OnboardingModal } from './OnboardingModal'
import { OnboardingWidget } from './OnboardingWidget'
import { useOnboardingSteps } from './useOnboardingSteps'

/**
 * Mounts the onboarding modal + floating widget for the authenticated session.
 * - On first login (no `onboarding_seen` flag), opens the welcome modal once.
 * - Renders the floating widget until the tour is fully done (`onboarding_done`).
 */
export function OnboardingHost() {
  const user = useAuthStore((state) => state.user)
  const onboardingSeen = useUiStore((s) => s.onboardingSeen)
  const setOnboardingSeen = useUiStore((s) => s.setOnboardingSeen)
  const onboardingDone = useUiStore((s) => s.onboardingDone)
  const onboardingModalOpen = useUiStore((s) => s.onboardingModalOpen)
  const setOnboardingModalOpen = useUiStore((s) => s.setOnboardingModalOpen)
  const { allDone, isLoading } = useOnboardingSteps()

  // First-login modal trigger: open once when user hasn't seen onboarding,
  // skip entirely if they already completed the tour or there's nothing left to do.
  useEffect(() => {
    if (!user) return
    if (isLoading) return
    if (onboardingSeen) return
    if (onboardingDone) return
    if (allDone) {
      // Nothing to onboard against — silently mark as seen.
      setOnboardingSeen(true)
      return
    }
    setOnboardingModalOpen(true)
  }, [user, isLoading, onboardingSeen, onboardingDone, allDone, setOnboardingSeen, setOnboardingModalOpen])

  function handleModalClose() {
    setOnboardingModalOpen(false)
    setOnboardingSeen(true)
  }

  if (!user) return null

  return (
    <>
      <OnboardingModal open={onboardingModalOpen} onClose={handleModalClose} />
      {onboardingDone ? null : <OnboardingWidget />}
    </>
  )
}
