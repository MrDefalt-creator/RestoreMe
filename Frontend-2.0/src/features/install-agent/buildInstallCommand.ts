import { env } from '@/shared/config/env'

export type InstallOs = 'linux' | 'windows'

// Repo holding the installer scripts on raw.githubusercontent.com.
// Kept in sync with installers/install-agent.{sh,ps1} location.
const SCRIPT_REPO = 'MrDefalt-creator/RestorMe'
const SCRIPT_BRANCH = 'main'

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
      `  https://raw.githubusercontent.com/${SCRIPT_REPO}/${SCRIPT_BRANCH}/installers/install-agent.sh \\`,
      `  -o /tmp/install-agent.sh`,
      `sudo bash /tmp/install-agent.sh \\`,
      `  --server ${serverUrl} \\`,
      `  --token  ${token}`,
    ].join('\n')
  }

  return [
    `$installer = "$env:TEMP\\install-agent.ps1"`,
    `Invoke-WebRequest \``,
    `  -Uri https://raw.githubusercontent.com/${SCRIPT_REPO}/${SCRIPT_BRANCH}/installers/install-agent.ps1 \``,
    `  -OutFile $installer -UseBasicParsing`,
    `& $installer -Server ${serverUrl} -Token ${token}`,
  ].join('\n')
}
