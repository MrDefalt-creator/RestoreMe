import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Clipboard, ClipboardCheck, MonitorSmartphone, TerminalSquare } from 'lucide-react'
import { toast } from 'sonner'

import { getEnrollmentInfo } from '@/shared/api/agents'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Spinner } from '@/shared/ui/Spinner'
import { useI18n } from '@/shared/i18n'
import { buildInstallCommand, resolveServerUrl, type InstallOs } from './buildInstallCommand'

type InstallAgentDialogProps = {
  open: boolean
  onClose: () => void
}

export function InstallAgentDialog({ open, onClose }: InstallAgentDialogProps) {
  const { t } = useI18n()
  const [os, setOs] = useState<InstallOs>('linux')
  const [copied, setCopied] = useState(false)

  const enrollmentQuery = useQuery({
    queryKey: ['agents', 'enrollment-info'],
    queryFn: getEnrollmentInfo,
    enabled: open,
    // Token is a secret — drop it from cache shortly after the dialog
    // closes so it doesn't sit in memory for the rest of the session.
    staleTime: 0,
    gcTime: 60_000,
  })

  // Reset the copied-flag whenever the dialog closes — useState-tracked
  // prev value matches the React docs' "adjusting state on a prop change"
  // pattern.
  const [prevOpen, setPrevOpen] = useState(open)
  if (prevOpen !== open) {
    setPrevOpen(open)
    if (!open && copied) setCopied(false)
  }

  const serverUrl = resolveServerUrl()
  const token = enrollmentQuery.data?.enrollmentToken ?? '<enrollment-token>'
  const command = buildInstallCommand(os, serverUrl, token)
  const canCopy = enrollmentQuery.isSuccess && !enrollmentQuery.isError

  async function copy() {
    try {
      await navigator.clipboard.writeText(command)
      setCopied(true)
      toast.success(t('Copied'))
      setTimeout(() => setCopied(false), 1500)
    } catch {
      toast.error(t('Could not copy. Select the command manually.'))
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Install new agent')}
      description={t('Run this command on the machine you want to back up. The agent will appear under Pending agents after enrollment.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Close')}
          </Button>
          <Button onClick={copy} disabled={!canCopy} className="gap-2">
            {copied ? <ClipboardCheck className="h-4 w-4" /> : <Clipboard className="h-4 w-4" />}
            {copied ? t('Copied') : t('Copy command')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-2 rounded-lg border border-border bg-secondary/40 p-1">
          <OsButton
            active={os === 'linux'}
            icon={<TerminalSquare className="h-4 w-4" />}
            label="Linux"
            onClick={() => setOs('linux')}
          />
          <OsButton
            active={os === 'windows'}
            icon={<MonitorSmartphone className="h-4 w-4" />}
            label="Windows"
            onClick={() => setOs('windows')}
          />
        </div>

        {enrollmentQuery.isLoading ? (
          <div className="flex items-center gap-2 rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
            <Spinner /> {t('Loading enrollment token...')}
          </div>
        ) : enrollmentQuery.isError ? (
          <div className="rounded-lg border border-destructive/20 bg-destructive/8 p-4 text-sm text-destructive">
            {enrollmentQuery.error instanceof Error
              ? enrollmentQuery.error.message
              : t('Could not load the enrollment token.')}
          </div>
        ) : (
          <pre className="overflow-x-auto rounded-lg border border-border bg-card p-4 text-xs leading-6 text-foreground">
            <code>{command}</code>
          </pre>
        )}

        <p className="text-xs text-muted-foreground">
          {t('Server URL is taken from this panel ({url}). To install against a different backend, edit the command before running it.', {
            url: serverUrl,
          })}
        </p>
      </div>
    </Dialog>
  )
}

function OsButton({
  active,
  icon,
  label,
  onClick,
}: {
  active: boolean
  icon: React.ReactNode
  label: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        active
          ? 'flex items-center justify-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground shadow-sm'
          : 'flex items-center justify-center gap-2 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-secondary'
      }
    >
      {icon}
      {label}
    </button>
  )
}
