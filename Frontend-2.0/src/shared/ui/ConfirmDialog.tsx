import { useState, type ReactNode } from 'react'

import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { useI18n } from '@/shared/i18n'

type ConfirmDialogProps = {
  open: boolean
  onClose: () => void
  onConfirm: () => void
  title: string
  description?: string
  body?: ReactNode
  confirmLabel: string
  cancelLabel?: string
  variant?: 'danger' | 'default'
  isLoading?: boolean
  requireTypeName?: string
}

export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  description,
  body,
  confirmLabel,
  cancelLabel,
  variant = 'default',
  isLoading = false,
  requireTypeName,
}: ConfirmDialogProps) {
  const { t } = useI18n()
  const typeNameRequired = typeof requireTypeName === 'string' && requireTypeName.length > 0

  if (!open) return null

  return (
    <ConfirmDialogBody
      key={requireTypeName ?? ''}
      onClose={onClose}
      onConfirm={onConfirm}
      title={title}
      description={description}
      body={body}
      confirmLabel={confirmLabel}
      cancelLabel={cancelLabel ?? t('Cancel')}
      variant={variant}
      isLoading={isLoading}
      requireTypeName={typeNameRequired ? requireTypeName : undefined}
    />
  )
}

type BodyProps = Omit<ConfirmDialogProps, 'open' | 'cancelLabel' | 'variant' | 'isLoading'> & {
  cancelLabel: string
  variant: 'danger' | 'default'
  isLoading: boolean
}

function ConfirmDialogBody({
  onClose,
  onConfirm,
  title,
  description,
  body,
  confirmLabel,
  cancelLabel,
  variant,
  isLoading,
  requireTypeName,
}: BodyProps) {
  const { t } = useI18n()
  const [typed, setTyped] = useState('')

  const typeNameRequired = typeof requireTypeName === 'string' && requireTypeName.length > 0
  const typeNameSatisfied = !typeNameRequired || typed.trim() === requireTypeName

  return (
    <Dialog
      open
      title={title}
      description={description}
      onClose={isLoading ? () => {} : onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isLoading}>
            {cancelLabel}
          </Button>
          <Button
            variant={variant === 'danger' ? 'danger' : 'primary'}
            onClick={onConfirm}
            disabled={isLoading || !typeNameSatisfied}
          >
            {confirmLabel}
          </Button>
        </>
      }
    >
      {body}
      {typeNameRequired ? (
        <div className="space-y-2">
          <label className="text-sm text-muted-foreground" htmlFor="confirm-type-name">
            {t('Type {name} to confirm', { name: requireTypeName! })}
          </label>
          <Input
            id="confirm-type-name"
            value={typed}
            onChange={(event) => setTyped(event.target.value)}
            autoComplete="off"
            spellCheck={false}
            disabled={isLoading}
          />
        </div>
      ) : null}
    </Dialog>
  )
}
