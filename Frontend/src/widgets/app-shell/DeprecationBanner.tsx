import { AlertTriangle, ArrowUpRight } from 'lucide-react'

import { useI18n } from '@/shared/i18n'

// The legacy v1 UI is in deprecation; show a thin banner pointing operators
// to Frontend-2.0 on the standard :5173 port of the same host. If the user
// is already on 5173 the banner hides itself — safer than guessing.
const NEW_UI_PORT = '5173'

function resolveNewUiUrl(): string | null {
  if (typeof window === 'undefined') return null
  const { protocol, hostname, port } = window.location
  if (port === NEW_UI_PORT) return null
  return `${protocol}//${hostname}:${NEW_UI_PORT}/`
}

export function DeprecationBanner() {
  const { t } = useI18n()
  const newUi = resolveNewUiUrl()
  if (!newUi) return null

  return (
    <div className="border-b border-amber-300/60 bg-amber-50 px-5 py-2 text-sm text-amber-900 md:px-8">
      <div className="flex flex-wrap items-center gap-2">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        <span className="font-medium">{t('Deprecated UI.')}</span>
        <span className="text-amber-800">
          {t('This frontend is no longer the recommended version. The new RestoreMe admin panel is at:')}
        </span>
        <a
          href={newUi}
          className="inline-flex items-center gap-1 font-medium text-amber-950 underline underline-offset-2 hover:text-amber-700"
        >
          {newUi}
          <ArrowUpRight className="h-3.5 w-3.5" />
        </a>
      </div>
    </div>
  )
}
