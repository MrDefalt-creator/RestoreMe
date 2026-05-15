import { useNavigate } from 'react-router-dom'

import { useI18n } from '@/shared/i18n'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

type NotFoundPageProps = {
  compact?: boolean
}

export function NotFoundPage({ compact = false }: NotFoundPageProps) {
  const navigate = useNavigate()
  const { t } = useI18n()

  return (
    <div className={compact ? 'py-6' : 'flex min-h-screen items-center justify-center px-4 py-10'}>
      <Card className="mx-auto max-w-2xl space-y-5">
        <div className="space-y-3">
          <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">404</p>
          <h1 className="text-3xl font-semibold text-ink-950">{t('Page not found')}</h1>
          <p className="max-w-xl text-sm leading-6 text-ink-800/75">
            {t('The requested route is not available in the current RestoreMe console. Use the navigation menu or return to the main dashboard.')}
          </p>
        </div>

        <div className="flex flex-wrap gap-3">
          <Button onClick={() => navigate('/')}>{t('Go to dashboard')}</Button>
          <Button variant="secondary" onClick={() => navigate('/agents')}>
            {t('Open agents')}
          </Button>
        </div>
      </Card>
    </div>
  )
}
