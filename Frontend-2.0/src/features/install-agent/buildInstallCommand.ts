import { env } from '@/shared/config/env'

export type InstallOs = 'linux' | 'windows'

// The token passed in is single-use and TTL-bound — minted on demand by
// the install-agent wizard from POST /api/agents/install-tokens. A leak
// of one install command compromises at most one agent slot.
//
// Installer scripts are served by the backend itself at /installers/* —
// see Backup.Server.Api/Dockerfile (copies installers/*.{ps1,sh} into
// wwwroot) and docker-compose/README.md → "Building agent binaries".
// Self-hosted by design: no external GitHub dependency.

const LOCALISH_HOSTS = new Set(['localhost', '127.0.0.1', '::1', '[::1]'])

export function isLocalishUrl(url: string): boolean {
  try {
    const host = new URL(url).hostname.toLowerCase()
    return LOCALISH_HOSTS.has(host)
  } catch {
    return false
  }
}

// Best-effort guess at "what URL is reachable from an arbitrary agent
// host on the LAN". The wizard hands this to the operator as the default
// they can edit. Logic:
//
//  - If VITE_API_BASE_URL is absolute AND points at localhost while the
//    browser itself is on a real LAN/host name, swap in the browser's
//    hostname (keep scheme + port from the configured apiBaseUrl). This
//    is the common case for our docker-compose default
//    (apiBaseUrl=http://localhost:8080) when admin opens the panel from
//    another machine on the network — wizard would otherwise hand them
//    a self-referential URL.
//  - If VITE_API_BASE_URL is absolute and not localhost, trust it.
//  - If apiBaseUrl is relative, fall back to window.location.origin
//    (frontend and backend share an origin behind a reverse proxy).
export function resolveServerUrl(): string {
  const base = env.apiBaseUrl
  if (base.startsWith('http://') || base.startsWith('https://')) {
    try {
      const apiUrl = new URL(base)
      const apiHost = apiUrl.hostname.toLowerCase()
      const apiIsLocalish = LOCALISH_HOSTS.has(apiHost)
      if (apiIsLocalish && typeof window !== 'undefined') {
        const browserHost = window.location.hostname.toLowerCase()
        if (!LOCALISH_HOSTS.has(browserHost)) {
          // Browser sees the backend on the same machine as the frontend.
          // Substitute the browser's view of the host while keeping the
          // configured backend port + scheme.
          const port = apiUrl.port || (apiUrl.protocol === 'https:' ? '443' : '80')
          return `${apiUrl.protocol}//${window.location.hostname}:${port}`
        }
      }
    } catch {
      // fall through to the literal env value
    }
    return base.replace(/\/api\/?$/, '').replace(/\/$/, '')
  }
  return window.location.origin.replace(/\/$/, '')
}

export function buildInstallCommand(os: InstallOs, serverUrl: string, token: string): string {
  const server = serverUrl.replace(/\/$/, '')
  if (os === 'linux') {
    return [
      `sudo curl -fsSL \\`,
      `  ${server}/installers/install-agent.sh \\`,
      `  -o /tmp/install-agent.sh`,
      `sudo bash /tmp/install-agent.sh \\`,
      `  --server ${server} \\`,
      `  --token  ${token}`,
    ].join('\n')
  }

  return [
    `$installer = "$env:TEMP\\install-agent.ps1"`,
    `Invoke-WebRequest \``,
    `  -Uri ${server}/installers/install-agent.ps1 \``,
    `  -OutFile $installer -UseBasicParsing`,
    `& $installer -Server ${server} -Token ${token}`,
  ].join('\n')
}
