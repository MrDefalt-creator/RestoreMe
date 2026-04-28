import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'

import { deleteUser } from '@/entities/user/api'
import type { AdminUser } from '@/entities/user/model/types'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'

type DeleteUserDialogProps = {
  open: boolean
  user: AdminUser | null
  onClose: () => void
  onSuccess: () => void
}

export function DeleteUserDialog({ open, user, onClose, onSuccess }: DeleteUserDialogProps) {
  const mutation = useMutation({
    mutationFn: () => {
      if (!user) {
        throw new Error('User is required')
      }

      return deleteUser(user.id)
    },
    onSuccess: () => {
      toast.success('User deleted')
      onSuccess()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to delete user')
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Delete user"
      description={user ? `Delete ${user.username}. This removes their access to the RestoreMe control plane.` : 'Delete the selected user.'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="danger" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
            Delete user
          </Button>
        </>
      }
    >
      <p className="text-sm text-ink-800">
        Use deletion for accounts that should no longer exist at all. For temporary access suspension, prefer disabling the user instead.
      </p>
    </Dialog>
  )
}
