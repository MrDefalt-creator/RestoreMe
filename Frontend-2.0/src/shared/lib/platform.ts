/**
 * Returns the label for the platform's primary modifier key:
 * `⌘` on macOS, `Ctrl` everywhere else. Used in keyboard hints so
 * Windows / Linux users see the key they actually press.
 */
export function getModKeyLabel(): string {
  if (typeof navigator === 'undefined') return 'Ctrl'
  const platform = (navigator.userAgentData?.platform || navigator.platform || '').toLowerCase()
  return platform.includes('mac') ? '⌘' : 'Ctrl'
}

declare global {
  interface Navigator {
    userAgentData?: { platform: string }
  }
}
