import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'

import { deleteUser } from '@/entities/user/api'
import type { AdminUser } from '@/entities/user/model/types'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { useI18n } from '@/shared/i18n'

type DeleteUserDialogProps = {
  open: boolean
  user: AdminUser | null
  onClose: () => void
  onSuccess: () => void
}

export function DeleteUserDialog({ open, user, onClose, onSuccess }: DeleteUserDialogProps) {
  const { t } = useI18n()
  const mutation = useMutation({
    mutationFn: () => {
      if (!user) {
        throw new Error('User is required')
      }

      return deleteUser(user.id)
    },
    onSuccess: () => {
      toast.success(t('User deleted'))
      onSuccess()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to delete user'))
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('Delete user')}
      description={user ? t('Delete {username}. This removes their RestoreMe access.', { username: user.username }) : t('Delete the selected user.')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button variant="danger" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
            {t('Delete user')}
          </Button>
        </>
      }
    >
      <p className="text-sm text-ink-800">
        {t('Use deletion for accounts that should no longer exist. For temporary suspension, disable the user instead.')}
      </p>
    </Dialog>
  )
}
