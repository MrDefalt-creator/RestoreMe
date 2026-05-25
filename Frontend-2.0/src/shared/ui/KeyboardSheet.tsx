import { useEffect } from 'react'
import * as RadixDialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'

import { useUiStore } from '@/app/store/ui-store'
import { Button } from '@/shared/ui/Button'
import { useI18n } from '@/shared/i18n'

const SECTIONS = [
  {
    heading: 'Navigation',
    shortcuts: [
      { keys: ['g', 'a'], description: 'Go to Agents' },
      { keys: ['g', 'p'], description: 'Go to Policies' },
      { keys: ['g', 'j'], description: 'Go to Jobs' },
      { keys: ['g', 'b'], description: 'Go to Backups' },
      { keys: ['g', 'd'], description: 'Go to Dashboard' },
      { keys: ['⌘', 'K'], description: 'Open command palette' },
    ],
  },
  {
    heading: 'Actions',
    shortcuts: [
      { keys: ['⌘', 'K'], description: 'Install agent (via palette)' },
      { keys: ['⌘', 'K'], description: 'Create policy (via palette)' },
      { keys: ['⌘', 'K'], description: 'Toggle theme (via palette)' },
      { keys: ['⌘', 'K'], description: 'Sign out (via palette)' },
    ],
  },
  {
    heading: 'Lists & dialogs',
    shortcuts: [
      { keys: ['ESC'], description: 'Close drawer or dialog' },
      { keys: ['?'], description: 'Open this keyboard reference' },
    ],
  },
]

export function KeyboardSheet() {
  const { t } = useI18n()
  const isOpen = useUiStore((state) => state.keyboardSheetOpen)
  const setOpen = useUiStore((state) => state.setKeyboardSheetOpen)

  useEffect(() => {
    function handler(event: KeyboardEvent) {
      if (event.key !== '?') return
      const target = event.target as HTMLElement
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) return
      event.preventDefault()
      setOpen(true)
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [setOpen])

  return (
    <RadixDialog.Root open={isOpen} onOpenChange={(open) => !open && setOpen(false)}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <RadixDialog.Content className="fixed left-1/2 top-[15vh] z-50 w-full max-w-[520px] -translate-x-1/2 overflow-hidden rounded-xl border border-border bg-card shadow-[var(--shadow-xl)] focus:outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95">
          <RadixDialog.Title className="sr-only">{t('Keyboard shortcuts')}</RadixDialog.Title>
          <div className="flex items-center justify-between border-b border-border px-5 py-4">
            <p className="font-semibold text-foreground">{t('Keyboard shortcuts')}</p>
            <Button variant="ghost" size="icon" onClick={() => setOpen(false)} aria-label={t('Close')}>
              <X className="h-4 w-4" />
            </Button>
          </div>
          <div className="max-h-[60vh] overflow-y-auto p-5 space-y-5">
            {SECTIONS.map((section) => (
              <section key={section.heading}>
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  {t(section.heading)}
                </h3>
                <dl className="space-y-1.5">
                  {section.shortcuts.map((s, i) => (
                    <div key={i} className="flex items-center justify-between gap-4 text-sm">
                      <dd className="text-muted-foreground">{t(s.description)}</dd>
                      <dt className="flex shrink-0 items-center gap-1">
                        {s.keys.map((k, j) => (
                          <kbd
                            key={j}
                            className="rounded border border-border bg-secondary px-1.5 py-0.5 font-mono text-xs text-foreground"
                          >
                            {k}
                          </kbd>
                        ))}
                      </dt>
                    </div>
                  ))}
                </dl>
              </section>
            ))}
          </div>
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  )
}
