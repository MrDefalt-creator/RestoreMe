import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { deleteNotificationChannel, type NotificationChannel } from '@/shared/api/notifications'
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog'
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
    <ConfirmDialog
      open={channel !== null}
      onClose={onClose}
      onConfirm={() => channel && mutation.mutate(channel.id)}
      title={t('Delete notification channel')}
      description={
        channel
          ? t("Delete '{name}'? Notifications will stop reaching this destination immediately.", { name: channel.name })
          : ''
      }
      body={
        <p className="text-sm leading-6 text-muted-foreground">
          {t('Existing audit log entries for past deliveries are preserved.')}
        </p>
      }
      confirmLabel={t('Delete channel')}
      variant="danger"
      isLoading={mutation.isPending}
    />
  )
}
