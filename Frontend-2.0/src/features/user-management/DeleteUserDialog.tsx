import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'

import { deleteUser, type User } from '@/entities/user'
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog'
import { useI18n } from '@/shared/i18n'

type DeleteUserDialogProps = {
  open: boolean
  user: User | null
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
    <ConfirmDialog
      open={open && user !== null}
      onClose={onClose}
      onConfirm={() => mutation.mutate()}
      title={t('Delete user')}
      description={user ? t('Delete {username}. This removes their RestoreMe access.', { username: user.username }) : t('Delete the selected user.')}
      body={
        <p className="text-sm leading-6 text-muted-foreground">
          {t('Use deletion for accounts that should no longer exist. For temporary suspension, disable the user instead.')}
        </p>
      }
      confirmLabel={mutation.isPending ? t('Deleting...') : t('Delete user')}
      variant="danger"
      isLoading={mutation.isPending}
    />
  )
}
