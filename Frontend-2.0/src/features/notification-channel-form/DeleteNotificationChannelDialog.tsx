import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { deleteNotificationChannel, type NotificationChannel } from '@/shared/api/notifications'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { queryKeys } from '@/shared/lib/query'
import { useI18n } from '@/shared/i18n'

type Props = {
  channel: NotificationChannel | null
  onClose: () => void
}

export function DeleteNotificationChannelDialog({ channel, onClose }: Props) {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: (id: string) => deleteNotificationChannel(id),
    onSuccess: () => {
      toast.success(t('Notification channel deleted'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.notificationChannels })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to delete notification channel'))
    },
  })

  return (
    <Dialog
      open={channel !== null}
      onClose={mutation.isPending ? () => {} : onClose}
      title={t('Delete notification channel')}
      description={
        channel
          ? t("Delete '{name}'? Notifications will stop reaching this destination immediately.", {
              name: channel.name,
            })
          : ''
      }
      footer={
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            {t('Cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={mutation.isPending || !channel}
            onClick={() => channel && mutation.mutate(channel.id)}
          >
            {t('Delete channel')}
          </Button>
        </div>
      }
    >
      <p className="text-sm leading-6 text-muted-foreground">
        {t('Existing audit log entries for past deliveries are preserved.')}
      </p>
    </Dialog>
  )
}
