import { createContext } from 'react'
import type { DuelStatusResponse } from '../lib/types'

export type NotificationEvent = {
  id: string
  title: string
  message: string
  createdAtUtc: string
}

export type NotificationContextValue = {
  isConnected: boolean
  latestDuelEvent: DuelStatusResponse | null
  notifications: NotificationEvent[]
  clearNotification: (id: string) => void
  pushEnabled: boolean
}

export const NotificationContext = createContext<NotificationContextValue | null>(null)
