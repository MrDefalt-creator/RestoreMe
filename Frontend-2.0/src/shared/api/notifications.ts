import apiClient from './client'

export type NotificationChannelType = 'Webhook' | 'Telegram' | 'Slack' | 'Discord'

export type NotificationEventType =
  | 'BackupFailed'
  | 'RestoreFailed'
  | 'BackupCompleted'
  | 'AgentOffline'
  | 'AgentBackOnline'

export interface NotificationChannel {
  id: string
  name: string
  type: NotificationChannelType
  isEnabled: boolean
  subscribedEvents: NotificationEventType[]
  createdAt: string
  updatedAt: string | null
}

export interface CreateNotificationChannelRequest {
  name: string
  type: NotificationChannelType
  isEnabled: boolean
  settings: string
  subscribedEvents: NotificationEventType[] | null
}

export interface UpdateNotificationChannelRequest {
  name: string
  isEnabled: boolean
  settings: string | null
  subscribedEvents: NotificationEventType[] | null
}

export interface TestNotificationChannelResponse {
  success: boolean
  error: string | null
}

export async function getNotificationChannels(): Promise<NotificationChannel[]> {
  const response = await apiClient.get('/api/notification-channels')
  return response.data
}

export async function createNotificationChannel(request: CreateNotificationChannelRequest): Promise<NotificationChannel> {
  const response = await apiClient.post('/api/notification-channels', request)
  return response.data
}

export async function updateNotificationChannel(channelId: string, request: UpdateNotificationChannelRequest): Promise<NotificationChannel> {
  const response = await apiClient.put(`/api/notification-channels/${channelId}`, request)
  return response.data
}

export async function deleteNotificationChannel(channelId: string): Promise<void> {
  await apiClient.delete(`/api/notification-channels/${channelId}`)
}

export async function testNotificationChannel(channelId: string): Promise<TestNotificationChannelResponse> {
  const response = await apiClient.post(`/api/notification-channels/${channelId}/test`)
  return response.data
}
