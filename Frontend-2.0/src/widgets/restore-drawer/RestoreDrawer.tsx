import * as RadixDialog from '@radix-ui/react-dialog'
import { useQueryClient } from '@tanstack/react-query'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { useEffect, useRef } from 'react'
import { X } from 'lucide-react'

import { getRestoreStatus } from '@/shared/api/artifacts'
import { queryKeys } from '@/shared/lib/query'
import { formatFileSize } from '@/shared/lib/format'
import { useUiStore } from '@/app/store/ui-store'
import { Button } from '@/shared/ui/Button'
import { useI18n } from '@/shared/i18n'

export function RestoreDrawer() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const activeRestoreJobId = useUiStore((state) => state.activeRestoreJobId)
  const setActiveRestoreJobId = useUiStore((state) => state.setActiveRestoreJobId)
  const isOpen = activeRestoreJobId !== null
  const completedRef = useRef(false)

  const statusQuery = useQuery({
    queryKey: queryKeys.restoreStatus(activeRestoreJobId ?? ''),
    queryFn: () => getRestoreStatus(activeRestoreJobId!),
    enabled: !!activeRestoreJobId,
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status === 'completed' || status === 'failed' ? false : 2000
    },
  })

  const status = statusQuery.data

  useEffect(() => {
    if (!status) return
    if (completedRef.current) return
    if (status.status === 'completed') {
      completedRef.current = true
      toast.success(t('Restore completed successfully'))
      queryClient.invalidateQueries({ queryKey: queryKeys.artifacts })
      queryClient.invalidateQueries({ queryKey: queryKeys.jobs })
      const timer = setTimeout(() => setActiveRestoreJobId(null), 5000)
      return () => clearTimeout(timer)
    }
    if (status.status === 'failed') {
      completedRef.current = true
      toast.error(t('Restore job failed'))
    }
  }, [status?.status, queryClient, setActiveRestoreJobId, t])

  useEffect(() => {
    if (!activeRestoreJobId) completedRef.current = false
  }, [activeRestoreJobId])

  const pct = status?.progress != null ? status.progress : null
  const logLines = status?.logTail ? status.logTail.split('\n').slice(-10) : []

  const statusColor: Record<string, string> = {
    pending: 'text-muted-foreground',
    running: 'text-warning',
    completed: 'text-success',
    failed: 'text-destructive',
  }

  return (
    <RadixDialog.Root open={isOpen} onOpenChange={(open) => { if (!open) setActiveRestoreJobId(null) }}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-50 bg-background/60 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <RadixDialog.Content className="fixed right-0 top-0 z-50 h-full w-full max-w-[440px] border-l border-border bg-card shadow-[var(--shadow-xl)] focus:outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:slide-out-to-right data-[state=open]:slide-in-from-right">
          <RadixDialog.Title className="sr-only">{t('Restore job status')}</RadixDialog.Title>
          <div className="flex h-full flex-col">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <div>
                <p className="font-semibold text-foreground">{t('Restore job')}</p>
                {status && (
                  <p className={`text-xs font-medium capitalize ${statusColor[status.status] ?? 'text-muted-foreground'}`}>
                    {t(status.status)}
                  </p>
                )}
              </div>
              <Button variant="ghost" size="icon" onClick={() => setActiveRestoreJobId(null)} aria-label={t('Close')}>
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="flex-1 overflow-y-auto p-5 space-y-5">
              {statusQuery.isLoading && (
                <div className="h-2 w-full animate-pulse rounded-full bg-secondary" />
              )}

              {status && (
                <>
                  <div className="space-y-1.5">
                    <div className="flex justify-between text-xs text-muted-foreground">
                      <span>{t('Progress')}</span>
                      <span>{pct != null ? `${pct}%` : t('In progress...')}</span>
                    </div>
                    <div className="h-2 w-full overflow-hidden rounded-full bg-secondary">
                      {pct != null ? (
                        <div
                          className="h-full rounded-full bg-primary transition-all duration-500"
                          style={{ width: `${pct}%` }}
                        />
                      ) : (
                        <div className="h-full w-1/3 animate-[slide-right_1.5s_ease-in-out_infinite] rounded-full bg-primary/60" />
                      )}
                    </div>
                  </div>

                  {(status.bytesTotal != null || status.bytesDone != null) && (
                    <div className="grid grid-cols-2 gap-3 text-sm">
                      {status.bytesDone != null && (
                        <div>
                          <p className="text-xs text-muted-foreground">{t('Done')}</p>
                          <p className="font-medium">{formatFileSize(status.bytesDone)}</p>
                        </div>
                      )}
                      {status.bytesTotal != null && (
                        <div>
                          <p className="text-xs text-muted-foreground">{t('Total')}</p>
                          <p className="font-medium">{formatFileSize(status.bytesTotal)}</p>
                        </div>
                      )}
                      {status.etaSeconds != null && (
                        <div>
                          <p className="text-xs text-muted-foreground">{t('ETA')}</p>
                          <p className="font-medium">{status.etaSeconds}s</p>
                        </div>
                      )}
                    </div>
                  )}

                  {logLines.length > 0 && (
                    <div className="space-y-1.5">
                      <p className="text-xs font-medium text-muted-foreground">{t('Log')}</p>
                      <pre className="max-h-[300px] overflow-y-auto rounded-md border border-border bg-secondary/40 p-3 font-mono text-xs text-foreground leading-relaxed">
                        {logLines.join('\n')}
                      </pre>
                    </div>
                  )}

                  <div className="space-y-0.5">
                    <p className="text-xs text-muted-foreground">{t('Job ID')}</p>
                    <p className="font-mono text-xs text-foreground">{status.id}</p>
                  </div>
                </>
              )}
            </div>
          </div>
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  )
}
