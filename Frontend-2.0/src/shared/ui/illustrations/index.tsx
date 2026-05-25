type IllustrationProps = { className?: string }

export function NoAgents({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 80 60" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden>
      <rect x="20" y="18" width="40" height="28" rx="4" />
      <circle cx="40" cy="32" r="6" />
      <path d="M28 46v4M52 46v4M34 14h12" strokeOpacity=".4" />
      <path d="M60 10l8 8M68 10l-8 8" strokeOpacity=".3" />
    </svg>
  )
}

export function AllClear({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 80 60" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden>
      <circle cx="40" cy="30" r="18" strokeOpacity=".3" />
      <path d="M30 30l7 7 13-14" strokeWidth="2" />
      <path d="M18 14l4 4M58 14l4-4M18 46l4-4M58 46l4 4" strokeOpacity=".25" />
    </svg>
  )
}

export function NoBackups({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 80 60" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden>
      <path d="M24 44V28l16-12 16 12v16" />
      <path d="M34 44v-8h12v8" />
      <path d="M40 16v-6M14 30H8M72 30h-6" strokeOpacity=".3" />
      <circle cx="40" cy="34" r="3" strokeOpacity=".4" />
    </svg>
  )
}

export function WaitingJobs({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 80 60" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden>
      <circle cx="40" cy="30" r="16" />
      <path d="M40 20v10l6 4" strokeWidth="2" />
      <path d="M22 12l3 3M55 12l3-3" strokeOpacity=".3" />
    </svg>
  )
}

export function NoMatches({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 80 60" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden>
      <circle cx="35" cy="28" r="14" />
      <path d="M45 38l12 12" strokeWidth="2.5" />
      <path d="M29 22l12 12M41 22L29 34" strokeOpacity=".4" />
    </svg>
  )
}

export function CannotLoad({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 80 60" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden>
      <path d="M40 14l22 38H18L40 14z" />
      <path d="M40 28v10" strokeWidth="2" />
      <circle cx="40" cy="43" r="1.5" fill="currentColor" stroke="none" />
    </svg>
  )
}
