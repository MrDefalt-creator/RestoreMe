import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'

import { deleteUser, type User } from '@/shared/api/users'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'

type DeleteUserDialogProps = {
  open: boolean
  user: User | null
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
      description={user ? `Delete ${user.username}. This removes their RestoreMe access.` : 'Delete the selected user.'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="danger" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? 'Deleting...' : 'Delete user'}
          </Button>
        </>
      }
    >
      <p className="text-sm leading-6 text-muted-foreground">
        Use deletion for accounts that should no longer exist. For temporary suspension, disable the user instead.
      </p>
    </Dialog>
  )
}
