import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  CheckCircle2,
  Clipboard,
  ClipboardCheck,
  MonitorSmartphone,
  TerminalSquare,
} from 'lucide-react'
import { toast } from 'sonner'

import { createInstallToken, getAgents, type CreateInstallTokenResponse } from '@/shared/api/agents'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Spinner } from '@/shared/ui/Spinner'
import { useI18n } from '@/shared/i18n'
import { buildInstallCommand, isLocalishUrl, resolveServerUrl, type InstallOs } from './buildInstallCommand'

type InstallAgentDialogProps = {
  open: boolean
  onClose: () => void
}

type Phase = 'form' | 'command'

export function InstallAgentDialog({ open, onClose }: InstallAgentDialogProps) {
  const { t } = useI18n()
  const [phase, setPhase] = useState<Phase>('form')
  const [os, setOs] = useState<InstallOs>('linux')
  const [preApprovedName, setPreApprovedName] = useState('')
  const [copied, setCopied] = useState(false)
  const [token, setToken] = useState<CreateInstallTokenResponse | null>(null)
  const [now, setNow] = useState(() => Date.now())
  // Editable Server URL — defaults to the smart resolver, which picks the
  // browser's hostname when the build-time apiBaseUrl is localhost. The
  // operator can override here if they're behind a reverse proxy or want
  // a different hostname baked into the install command.
  const [serverUrl, setServerUrl] = useState(() => resolveServerUrl())

  // Reset everything when the dialog closes (useState-tracked prev value
  // matches React's "adjusting state on a prop change" pattern).
  const [prevOpen, setPrevOpen] = useState(open)
  if (prevOpen !== open) {
    setPrevOpen(open)
    if (!open) {
      setPhase('form')
      setToken(null)
      setPreApprovedName('')
      setCopied(false)
      setServerUrl(resolveServerUrl())
    }
  }

  // Once the token exists, tick a 1s clock so the expiry countdown updates.
  useEffect(() => {
    if (phase !== 'command' || !token) return
    const id = setInterval(() => setNow(Date.now()), 1_000)
    return () => clearInterval(id)
  }, [phase, token])

  const mutation = useMutation({
    mutationFn: createInstallToken,
    onSuccess: (result) => {
      setToken(result)
      setNow(Date.now())
      setPhase('command')
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Could not generate install token.'))
    },
  })

  // After the wizard moves to phase=command, poll the Agents list every 3s
  // looking for a new approved agent matching the pre-approved name or any
  // newly-arrived one. When found, flip to "Connected ✓".
  const enteredAt = useState(() => Date.now())[0]
  const agentsQuery = useQuery({
    queryKey: ['agents', 'install-watch'],
    queryFn: getAgents,
    enabled: phase === 'command' && Boolean(token),
    refetchInterval: 3_000,
    staleTime: 0,
  })

  const connectedAgent = (agentsQuery.data ?? []).find((a) => {
    const created = a.createdAt ? Date.parse(a.createdAt) : NaN
    if (!Number.isFinite(created) || created < enteredAt - 2_000) return false
    if (preApprovedName && a.name.trim() === preApprovedName.trim()) return true
    // No PreApprovedName supplied — any newly-created agent is the one.
    return !preApprovedName
  })

  const trimmedServerUrl = serverUrl.trim().replace(/\/$/, '')
  const serverLooksLocal = isLocalishUrl(trimmedServerUrl)
  const installCommand = token
    ? buildInstallCommand(os, trimmedServerUrl, token.token)
    : ''

  async function copy() {
    if (!installCommand) return
    try {
      await navigator.clipboard.writeText(installCommand)
      setCopied(true)
      toast.success(t('Copied'))
      setTimeout(() => setCopied(false), 1500)
    } catch {
      toast.error(t('Could not copy. Select the command manually.'))
    }
  }

  const expiresAtMs = token ? Date.parse(token.expiresAt) : 0
  const secondsLeft = token ? Math.max(0, Math.floor((expiresAtMs - now) / 1000)) : 0
  const minutesLeft = Math.floor(secondsLeft / 60)
  const tokenExpired = token !== null && secondsLeft === 0

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Install new agent')}
      description={
        phase === 'form'
          ? t('Generate a single-use install command. Run it on the machine you want to back up — the agent connects and shows up here automatically.')
          : t('Run this command on the target machine. The token is single-use and expires shortly.')
      }
      footer={
        phase === 'form' ? (
          <>
            <Button variant="secondary" onClick={onClose}>
              {t('Cancel')}
            </Button>
            <Button onClick={() => mutation.mutate({ preApprovedName: preApprovedName.trim() || undefined, ttlMinutes: 15 })} disabled={mutation.isPending}>
              {mutation.isPending ? t('Generating...') : t('Generate install command')}
            </Button>
          </>
        ) : (
          <>
            <Button variant="secondary" onClick={onClose}>
              {t('Close')}
            </Button>
            <Button onClick={copy} disabled={!installCommand || tokenExpired} className="gap-2">
              {copied ? <ClipboardCheck className="h-4 w-4" /> : <Clipboard className="h-4 w-4" />}
              {copied ? t('Copied') : t('Copy command')}
            </Button>
          </>
        )
      }
    >
      {phase === 'form' ? (
        <div className="space-y-4">
          <div>
            <label className="text-sm font-medium text-foreground" htmlFor="install-agent-server">
              {t('Backend URL (as reachable from the agent machine)')}
            </label>
            <Input
              id="install-agent-server"
              value={serverUrl}
              onChange={(event) => setServerUrl(event.target.value)}
              placeholder="http://restoreme.lan:8080"
              className="mt-2"
              spellCheck={false}
            />
            {serverLooksLocal ? (
              <p className="mt-1 text-xs text-warning">
                {t('This URL points at localhost. It will only work if you run the install command on the same machine as the backend. For a different host, replace it with a LAN-reachable hostname or IP.')}
              </p>
            ) : (
              <p className="mt-1 text-xs text-muted-foreground">
                {t('The install script downloads the agent binary from this URL and the agent uses it for every API call thereafter.')}
              </p>
            )}
          </div>
          <div>
            <label className="text-sm font-medium text-foreground" htmlFor="install-agent-name">
              {t('Agent name (optional)')}
            </label>
            <Input
              id="install-agent-name"
              value={preApprovedName}
              onChange={(event) => setPreApprovedName(event.target.value)}
              placeholder={t('Accounting workstation')}
              className="mt-2"
            />
            <p className="mt-1 text-xs text-muted-foreground">
              {t('Leave empty to use the host machine name as the agent name.')}
            </p>
          </div>
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
        </div>
      ) : (
        <div className="space-y-4">
          {tokenExpired ? (
            <div className="rounded-lg border border-warning/30 bg-warning/8 p-3 text-sm text-warning">
              {t('This install token has expired. Generate a new one to retry.')}
            </div>
          ) : (
            <div className="flex items-center justify-between rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs">
              <span className="text-muted-foreground">
                {minutesLeft >= 1
                  ? t('Expires in {minutes}m {seconds}s', { minutes: minutesLeft, seconds: secondsLeft % 60 })
                  : t('Expires in {seconds}s', { seconds: secondsLeft })}
              </span>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => {
                  setPhase('form')
                  setToken(null)
                }}
              >
                {t('Regenerate')}
              </Button>
            </div>
          )}

          <pre className="overflow-x-auto rounded-lg border border-border bg-card p-4 text-xs leading-6 text-foreground">
            <code>{installCommand}</code>
          </pre>

          <div className="rounded-lg border border-border bg-secondary/30 p-3 text-sm">
            {connectedAgent ? (
              <div className="flex items-center gap-2 text-success">
                <CheckCircle2 className="h-4 w-4 text-success" />
                <span>
                  {t('Connected: {name}', { name: connectedAgent.name })}
                </span>
              </div>
            ) : (
              <div className="flex items-center gap-2 text-muted-foreground">
                <Spinner />
                <span>{t('Waiting for agent to connect...')}</span>
              </div>
            )}
          </div>

          <p className="text-xs text-muted-foreground">
            {t('Backend URL used: {url}. To regenerate against a different host, click Regenerate and edit the field.', {
              url: trimmedServerUrl,
            })}
          </p>
          {serverLooksLocal ? (
            <div className="rounded-lg border border-warning/40 bg-warning/8 p-3 text-xs text-warning">
              {t('Heads-up: this command targets localhost. Running it on a machine other than the backend host will fail. Regenerate with a LAN-reachable URL if you are installing on a different machine.')}
            </div>
          ) : null}
        </div>
      )}
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
