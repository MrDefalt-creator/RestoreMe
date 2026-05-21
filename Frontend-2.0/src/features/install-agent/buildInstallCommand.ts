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

export function resolveServerUrl(): string {
  const base = env.apiBaseUrl
  if (base.startsWith('http://') || base.startsWith('https://')) {
    // The agent's --server flag expects the backend root, not the /api
    // prefix used by axios. Strip a trailing /api and any trailing slash
    // so the rendered URL is something like http://host:8080.
    return base.replace(/\/api\/?$/, '').replace(/\/$/, '')
  }
  // Relative apiBaseUrl (default '/api' or '' on a reverse-proxied
  // deployment) — frontend and backend share an origin.
  return window.location.origin.replace(/\/$/, '')
}

export function buildInstallCommand(os: InstallOs, serverUrl: string, token: string): string {
  if (os === 'linux') {
    return [
      `sudo curl -fsSL \\`,
      `  ${serverUrl}/installers/install-agent.sh \\`,
      `  -o /tmp/install-agent.sh`,
      `sudo bash /tmp/install-agent.sh \\`,
      `  --server ${serverUrl} \\`,
      `  --token  ${token}`,
    ].join('\n')
  }

  return [
    `$installer = "$env:TEMP\\install-agent.ps1"`,
    `Invoke-WebRequest \``,
    `  -Uri ${serverUrl}/installers/install-agent.ps1 \``,
    `  -OutFile $installer -UseBasicParsing`,
    `& $installer -Server ${serverUrl} -Token ${token}`,
  ].join('\n')
}
