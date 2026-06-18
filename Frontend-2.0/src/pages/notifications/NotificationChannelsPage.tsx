import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  Bell,
  MessageSquare,
  Plug,
  Plus,
  Send,
  Shield,
  Trash2,
  Webhook,
} from 'lucide-react'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { DeleteNotificationChannelDialog } from '@/features/notification-channel-form/DeleteNotificationChannelDialog'
import { NotificationChannelDialog } from '@/features/notification-channel-form/NotificationChannelDialog'
import {
  getNotificationChannels,
  testNotificationChannel,
  type NotificationChannel,
  type NotificationChannelType,
} from '@/shared/api/notifications'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { SkeletonList } from '@/shared/ui/Skeleton'
import { useI18n } from '@/shared/i18n'

export function NotificationChannelsPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const currentUser = useAuthStore((state) => state.user)
  const isAdmin = currentUser?.role === 'admin'
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<NotificationChannel | null>(null)
  const [deleting, setDeleting] = useState<NotificationChannel | null>(null)

  const channelsQuery = useQuery({
    queryKey: queryKeys.notificationChannels,
    queryFn: getNotificationChannels,
    ...liveQueryOptions,
    enabled: isAdmin,
  })

  const testMutation = useMutation({
    mutationFn: (channelId: string) => testNotificationChannel(channelId),
    onSuccess: (response) => {
      if (response.success) {
        toast.success(t('Test message delivered'))
      } else {
        toast.error(response.error ?? t('Delivery failed'))
      }
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Delivery failed'))
    },
  })

  if (!isAdmin) {
    return (
      <div className="space-y-8">
        <SectionHeading
          eyebrow={t('Security')}
          title={t('Notification channels')}
          description={t('Only administrators can configure backup notification destinations.')}
        />
        <EmptyState
          icon={<Shield className="h-7 w-7 text-muted-foreground" />}
          title={t('Administrator access required')}
          description={t('Sign in as an administrator to manage notification channels.')}
        />
      </div>
    )
  }

  const channels = channelsQuery.data ?? []

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow={t('Security')}
        title={t('Notification channels')}
        description={t('Send backup, restore and agent-health alerts to Telegram, Slack, Discord or a custom webhook.')}
        action={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4" />
            {t('Add channel')}
          </Button>
        }
      />

      {channelsQuery.isLoading ? (
        <SkeletonList count={3} />
      ) : channels.length === 0 ? (
        <EmptyState
          icon={<Bell className="h-7 w-7 text-muted-foreground" />}
          title={t('No notification channels yet')}
          description={t('Add a destination so RestoreMe can let you know when something needs attention.')}
          action={
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" />
              {t('Add channel')}
            </Button>
          }
        />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {channels.map((channel) => (
            <ChannelCard
              key={channel.id}
              channel={channel}
              onEdit={() => setEditing(channel)}
              onDelete={() => setDeleting(channel)}
              onTest={() => testMutation.mutate(channel.id)}
              testing={testMutation.isPending && testMutation.variables === channel.id}
            />
          ))}
        </div>
      )}

      <NotificationChannelDialog
        key={editing?.id ?? (createOpen ? 'new' : 'closed')}
        open={createOpen || editing !== null}
        channel={editing}
        onClose={() => {
          setCreateOpen(false)
          setEditing(null)
        }}
      />
      <DeleteNotificationChannelDialog channel={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}

function ChannelCard({
  channel,
  onEdit,
  onDelete,
  onTest,
  testing,
}: {
  channel: NotificationChannel
  onEdit: () => void
  onDelete: () => void
  onTest: () => void
  testing: boolean
}) {
  const { t } = useI18n()

  return (
    <Card>
      <CardContent className="flex flex-col gap-4 p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
              <ChannelIcon type={channel.type} />
            </span>
            <div className="min-w-0">
              <p className="truncate text-base font-semibold tracking-tight text-foreground">{channel.name}</p>
              <p className="text-sm text-muted-foreground">{t(channel.type)}</p>
            </div>
          </div>
          <Badge variant={channel.isEnabled ? 'success' : 'neutral'}>
            {channel.isEnabled ? t('Enabled') : t('Disabled')}
          </Badge>
        </div>

        <div className="rounded-lg border border-border bg-background/60 p-3 text-xs text-muted-foreground">
          {channel.subscribedEvents.length === 0
            ? t('Subscribed to every event')
            : t('{count} event(s) subscribed', { count: channel.subscribedEvents.length })}
        </div>

        <p className="text-xs text-muted-foreground">
          {t('Created {date}', { date: formatDateTime(channel.createdAt) })}
        </p>

        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" size="sm" onClick={onTest} disabled={testing}>
            <Send className="h-4 w-4" />
            {testing ? t('Sending...') : t('Test channel')}
          </Button>
          <Button variant="outline" size="sm" onClick={onEdit}>
            {t('Edit')}
          </Button>
          <Button variant="ghost" size="sm" onClick={onDelete}>
            <Trash2 className="h-4 w-4" />
            {t('Delete')}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

function ChannelIcon({ type }: { type: NotificationChannelType }) {
  switch (type) {
    case 'Telegram':
    case 'Slack':
    case 'Discord':
      return <MessageSquare className="h-5 w-5" />
    case 'Webhook':
      return <Webhook className="h-5 w-5" />
    default:
      return <Plug className="h-5 w-5" />
  }
}
