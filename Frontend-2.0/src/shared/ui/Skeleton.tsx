import type { HTMLAttributes } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * Pulsing placeholder block. Use Tailwind to size it: `<Skeleton className="h-4 w-32" />`.
 * Larger compositions (SkeletonRow / SkeletonCard) wrap several of these to match the
 * shape of the content that's about to load — better perceived speed than a spinner
 * because the page reserves the right amount of vertical space up-front.
 */
export function Skeleton({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('animate-pulse rounded-md bg-muted/70', className)}
      aria-hidden="true"
      {...props}
    />
  )
}

/** Three-line text block that mimics a paragraph header + two lines of body. */
export function SkeletonText({ lines = 3, className }: { lines?: number; className?: string }) {
  return (
    <div className={cn('space-y-2', className)} aria-hidden="true">
      {Array.from({ length: lines }).map((_, idx) => (
        <Skeleton
          key={idx}
          className={cn('h-3', idx === lines - 1 ? 'w-2/3' : 'w-full')}
        />
      ))}
    </div>
  )
}

/**
 * Card-shaped placeholder. Used in metric grids while the live counts load —
 * keeps the layout from jumping when the real values arrive.
 */
export function SkeletonCard({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-xl border border-border bg-card/80 p-5',
        className,
      )}
      aria-hidden="true"
    >
      <Skeleton className="h-3 w-24" />
      <Skeleton className="mt-4 h-7 w-32" />
      <Skeleton className="mt-3 h-3 w-20" />
    </div>
  )
}

/**
 * Single row in a list/table skeleton. `columns` picks how many bar widths to
 * show; defaults to 4 (matches most list pages here).
 */
export function SkeletonRow({ columns = 4, className }: { columns?: number; className?: string }) {
  return (
    <div
      className={cn('flex items-center gap-4 rounded-lg border border-border bg-card/60 px-4 py-3', className)}
      aria-hidden="true"
    >
      <Skeleton className="h-10 w-10 shrink-0 rounded-lg" />
      <div className="flex-1 space-y-2">
        <Skeleton className="h-3 w-1/3" />
        <Skeleton className="h-3 w-1/2" />
      </div>
      {Array.from({ length: Math.max(0, columns - 2) }).map((_, idx) => (
        <Skeleton key={idx} className="hidden h-3 w-20 md:block" />
      ))}
    </div>
  )
}

/** Stack of `count` skeleton rows. Convenience wrapper for list-page loading. */
export function SkeletonList({ count = 4, columns, className }: { count?: number; columns?: number; className?: string }) {
  return (
    <div className={cn('space-y-3', className)} aria-hidden="true">
      {Array.from({ length: count }).map((_, idx) => (
        <SkeletonRow key={idx} columns={columns} />
      ))}
    </div>
  )
}
