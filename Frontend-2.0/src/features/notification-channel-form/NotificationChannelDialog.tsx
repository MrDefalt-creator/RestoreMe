import { useMemo, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import {
  createNotificationChannel,
  updateNotificationChannel,
  type NotificationChannel,
  type NotificationChannelType,
  type NotificationEventType,
} from '@/shared/api/notifications'
import { Button } from '@/shared/ui/Button'
import { Dialog } from '@/shared/ui/Dialog'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'
import { Switch } from '@/shared/ui/Switch'
import { queryKeys } from '@/shared/lib/query'
import { useI18n } from '@/shared/i18n'

const CHANNEL_TYPES: NotificationChannelType[] = ['Telegram', 'Slack', 'Discord', 'Webhook']
const ALL_EVENTS: NotificationEventType[] = [
  'BackupFailed',
  'RestoreFailed',
  'BackupCompleted',
  'AgentOffline',
  'AgentBackOnline',
]

type FormState = {
  name: string
  type: NotificationChannelType
  isEnabled: boolean
  events: Set<NotificationEventType>
  // per-type fields
  url: string
  secret: string
  botToken: string
  chatId: string
}

const emptyState: FormState = {
  name: '',
  type: 'Telegram',
  isEnabled: true,
  events: new Set(ALL_EVENTS),
  url: '',
  secret: '',
  botToken: '',
  chatId: '',
}

type NotificationChannelDialogProps = {
  open: boolean
  channel: NotificationChannel | null
  onClose: () => void
}

export function NotificationChannelDialog({ open, channel, onClose }: NotificationChannelDialogProps) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const isEdit = Boolean(channel)
  // Parent passes a unique `key` per channel id so the component re-mounts
  // when the operator picks a different row to edit. Initial state comes
  // straight from props — no Effect, no sync-render setState.
  const [form, setForm] = useState<FormState>(() => deriveInitialState(channel))

  const settingsLabel = useMemo(() => buildSettingsLabel(form.type, t), [form.type, t])

  const createMutation = useMutation({
    mutationFn: createNotificationChannel,
    onSuccess: () => {
      toast.success(t('Notification channel created'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.notificationChannels })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to save notification channel'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ channelId, ...payload }: Parameters<typeof updateNotificationChannel>[1] & { channelId: string }) =>
      updateNotificationChannel(channelId, payload),
    onSuccess: () => {
      toast.success(t('Notification channel updated'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.notificationChannels })
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to save notification channel'))
    },
  })

  const isBusy = createMutation.isPending || updateMutation.isPending
  const subscribedEvents = Array.from(form.events)

  function toggleEvent(event: NotificationEventType) {
    setForm((prev) => {
      const next = new Set(prev.events)
      if (next.has(event)) {
        next.delete(event)
      } else {
        next.add(event)
      }
      return { ...prev, events: next }
    })
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const trimmedName = form.name.trim()
    if (!trimmedName) {
      toast.error(t('Channel name is required'))
      return
    }

    const settingsString = buildSettings(form)
    if (settingsString === null && !isEdit) {
      toast.error(t('Fill out the channel-specific fields'))
      return
    }

    const eventsList = subscribedEvents.length === ALL_EVENTS.length ? null : subscribedEvents

    if (isEdit && channel) {
      updateMutation.mutate({
        channelId: channel.id,
        name: trimmedName,
        isEnabled: form.isEnabled,
        // Empty per-type fields mean "keep what's already encrypted" so
        // operators can rename without retyping bot tokens.
        settings: settingsString,
        subscribedEvents: eventsList,
      })
    } else {
      createMutation.mutate({
        name: trimmedName,
        type: form.type,
        isEnabled: form.isEnabled,
        settings: settingsString ?? '{}',
        subscribedEvents: eventsList,
      })
    }
  }

  return (
    <Dialog
      open={open}
      onClose={isBusy ? () => {} : onClose}
      title={isEdit ? t('Edit notification channel') : t('Add notification channel')}
      description={t('Channels receive alerts about backup failures and agent health changes.')}
      footer={
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={isBusy}>
            {t('Cancel')}
          </Button>
          <Button type="submit" form="notification-channel-form" disabled={isBusy}>
            {isEdit ? t('Save changes') : t('Add channel')}
          </Button>
        </div>
      }
    >
      <form id="notification-channel-form" onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2">
          <label className="text-sm font-medium text-foreground" htmlFor="channel-name">
            {t('Channel name')}
          </label>
          <Input
            id="channel-name"
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            placeholder="e.g. Ops Telegram"
            disabled={isBusy}
            maxLength={150}
            required
          />
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium text-foreground" htmlFor="channel-type">
            {t('Channel type')}
          </label>
          <Select
            id="channel-type"
            value={form.type}
            onChange={(e) => setForm({ ...form, type: e.target.value as NotificationChannelType })}
            disabled={isBusy || isEdit}
          >
            {CHANNEL_TYPES.map((type) => (
              <option key={type} value={type}>
                {t(type)}
              </option>
            ))}
          </Select>
          {isEdit ? (
            <p className="text-xs text-muted-foreground">
              {t('Channel type cannot be changed after creation.')}
            </p>
          ) : null}
        </div>

        <ChannelSettingsFields form={form} setForm={setForm} disabled={isBusy} isEdit={isEdit} t={t} settingsLabel={settingsLabel} />

        <div className="space-y-2">
          <p className="text-sm font-medium text-foreground">{t('Subscribed events')}</p>
          <div className="grid gap-2 sm:grid-cols-2">
            {ALL_EVENTS.map((event) => {
              const switchId = `channel-event-${event}`
              return (
                <label
                  key={event}
                  htmlFor={switchId}
                  className="flex cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background/60 px-3 py-2 text-sm"
                >
                  <span className="text-foreground">{t(eventLabelKey(event))}</span>
                  <Switch
                    id={switchId}
                    size="sm"
                    checked={form.events.has(event)}
                    onCheckedChange={() => toggleEvent(event)}
                    disabled={isBusy}
                  />
                </label>
              )
            })}
          </div>
        </div>

        <label
          htmlFor="channel-enabled"
          className="flex cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background/60 px-3 py-2 text-sm"
        >
          <span className="text-foreground">{t('Enabled (delivers notifications)')}</span>
          <Switch
            id="channel-enabled"
            checked={form.isEnabled}
            onCheckedChange={(value) => setForm({ ...form, isEnabled: value })}
            disabled={isBusy}
          />
        </label>
      </form>
    </Dialog>
  )
}

function ChannelSettingsFields({
  form,
  setForm,
  disabled,
  isEdit,
  t,
  settingsLabel,
}: {
  form: FormState
  setForm: (state: FormState | ((prev: FormState) => FormState)) => void
  disabled: boolean
  isEdit: boolean
  t: (key: string, vars?: Record<string, string | number>) => string
  settingsLabel: string
}) {
  const placeholderHint = isEdit
    ? t('Leave blank to keep the existing secret.')
    : ''

  switch (form.type) {
    case 'Telegram':
      return (
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">{settingsLabel}</p>
          <Input
            value={form.botToken}
            onChange={(e) => setForm((prev) => ({ ...prev, botToken: e.target.value }))}
            placeholder={isEdit ? '••••••' : '123456:ABCDEF-token-from-BotFather'}
            disabled={disabled}
            autoComplete="off"
          />
          <Input
            value={form.chatId}
            onChange={(e) => setForm((prev) => ({ ...prev, chatId: e.target.value }))}
            placeholder={isEdit ? '••••••' : 'Chat ID (e.g. -1001234567890 or @your_channel)'}
            disabled={disabled}
            autoComplete="off"
          />
          {placeholderHint ? <p className="text-xs text-muted-foreground">{placeholderHint}</p> : null}
        </div>
      )
    case 'Slack':
    case 'Discord':
      return (
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">{settingsLabel}</p>
          <Input
            value={form.url}
            onChange={(e) => setForm((prev) => ({ ...prev, url: e.target.value }))}
            placeholder={
              isEdit
                ? '••••••'
                : form.type === 'Slack'
                  ? 'https://hooks.slack.com/services/...'
                  : 'https://discord.com/api/webhooks/...'
            }
            type="url"
            disabled={disabled}
            autoComplete="off"
          />
          {placeholderHint ? <p className="text-xs text-muted-foreground">{placeholderHint}</p> : null}
        </div>
      )
    case 'Webhook':
      return (
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">{settingsLabel}</p>
          <Input
            value={form.url}
            onChange={(e) => setForm((prev) => ({ ...prev, url: e.target.value }))}
            placeholder={isEdit ? '••••••' : 'https://your.server/webhooks/restoreme'}
            type="url"
            disabled={disabled}
            autoComplete="off"
          />
          <Input
            value={form.secret}
            onChange={(e) => setForm((prev) => ({ ...prev, secret: e.target.value }))}
            placeholder={isEdit ? '••••••' : 'Optional HMAC secret'}
            disabled={disabled}
            autoComplete="off"
          />
          {placeholderHint ? <p className="text-xs text-muted-foreground">{placeholderHint}</p> : null}
        </div>
      )
  }
}

function deriveInitialState(channel: NotificationChannel | null): FormState {
  if (!channel) {
    return emptyState
  }
  return {
    ...emptyState,
    name: channel.name,
    type: channel.type,
    isEnabled: channel.isEnabled,
    events: new Set(channel.subscribedEvents.length ? channel.subscribedEvents : ALL_EVENTS),
  }
}

function buildSettings(form: FormState): string | null {
  switch (form.type) {
    case 'Telegram': {
      if (!form.botToken && !form.chatId) {
        return null
      }
      return JSON.stringify({ botToken: form.botToken, chatId: form.chatId })
    }
    case 'Slack':
    case 'Discord': {
      if (!form.url) {
        return null
      }
      return JSON.stringify({ webhookUrl: form.url })
    }
    case 'Webhook': {
      if (!form.url) {
        return null
      }
      const payload: Record<string, string> = { url: form.url }
      if (form.secret) {
        payload.secret = form.secret
      }
      return JSON.stringify(payload)
    }
  }
}

function buildSettingsLabel(type: NotificationChannelType, t: (key: string) => string) {
  switch (type) {
    case 'Telegram':
      return t('Talk to @BotFather to create a bot, then grab the chat id via @userinfobot.')
    case 'Slack':
      return t('Paste the incoming-webhook URL from your Slack app configuration.')
    case 'Discord':
      return t('Paste the webhook URL from the target Discord channel settings.')
    case 'Webhook':
      return t('Generic HMAC-signed JSON webhook. Optionally set a shared secret.')
  }
}

function eventLabelKey(event: NotificationEventType): string {
  switch (event) {
    case 'BackupFailed':
      return 'Backup failed'
    case 'RestoreFailed':
      return 'Restore failed'
    case 'BackupCompleted':
      return 'Backup completed'
    case 'AgentOffline':
      return 'Agent offline'
    case 'AgentBackOnline':
      return 'Agent back online'
  }
}
