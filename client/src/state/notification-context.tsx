import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from 'react'
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { config } from '../lib/config'
import { useAuth } from './use-auth'
import {
  NotificationContext,
  type NotificationEvent,
} from './notification-store'
import type { DuelStatusResponse } from '../lib/types'
import {
  isPushAvailable,
  pushForegroundEventName,
} from '../lib/push'

const maxNotificationCount = 5

export function NotificationProvider({ children }: PropsWithChildren) {
  const { accessToken, isAuthenticated, getValidAccessToken } = useAuth()
  const [isConnected, setIsConnected] = useState(false)
  const [latestDuelEvent, setLatestDuelEvent] = useState<DuelStatusResponse | null>(null)
  const [notifications, setNotifications] = useState<NotificationEvent[]>([])
  const connectionRef = useRef<HubConnection | null>(null)

  const addNotification = useCallback((title: string, message: string) => {
    const item: NotificationEvent = {
      id: crypto.randomUUID(),
      title,
      message,
      createdAtUtc: new Date().toISOString(),
    }

    setNotifications((current) => [item, ...current].slice(0, maxNotificationCount))
  }, [])

  const clearNotification = useCallback((id: string) => {
    setNotifications((current) => current.filter((item) => item.id !== id))
  }, [])

  useEffect(() => {
    const handler = (event: Event) => {
      const customEvent = event as CustomEvent<{ title?: string; body?: string }>
      const title = customEvent.detail?.title ?? 'Notification'
      const message = customEvent.detail?.body ?? 'New app activity.'
      addNotification(title, message)
    }

    window.addEventListener(pushForegroundEventName, handler)
    return () => {
      window.removeEventListener(pushForegroundEventName, handler)
    }
  }, [addNotification])

  const pushEnabled = isAuthenticated && isPushAvailable()

  useEffect(() => {
    if (!isAuthenticated || !accessToken) {
      if (connectionRef.current) {
        void connectionRef.current.stop()
        connectionRef.current = null
      }

      return
    }

    let isMounted = true

    const connection = new HubConnectionBuilder()
      .withUrl(`${config.signalrBaseUrl}/hubs/notifications`, {
        accessTokenFactory: async () => {
          const token = await getValidAccessToken()
          return token ?? ''
        },
        withCredentials: false,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()

    connectionRef.current = connection

    connection.onclose(() => {
      if (isMounted) {
        setIsConnected(false)
      }
    })

    connection.onreconnecting(() => {
      if (isMounted) {
        setIsConnected(false)
      }
    })

    connection.onreconnected(() => {
      if (isMounted) {
        setIsConnected(true)
      }
    })

    connection.on('DuelChallengeReceived', (payload: DuelStatusResponse) => {
      setLatestDuelEvent(payload)
      addNotification('New challenge', `${payload.challengerDisplayName} challenged you to a duel.`)
    })

    connection.on('DuelResponseReceived', (payload: DuelStatusResponse) => {
      setLatestDuelEvent(payload)
      const accepted = payload.status === 'Accepted'
      addNotification('Duel response', accepted ? 'Challenge accepted.' : 'Challenge declined.')
    })

    connection.on('DuelResultPending', (payload: DuelStatusResponse) => {
      setLatestDuelEvent(payload)
      if (payload.status === 'AwaitingSecondReview') {
        addNotification('Result mismatch', 'Choose result again to resolve the duel.')
        return
      }

      if (payload.status === 'CoinFlipInProgress') {
        addNotification('Coin flip', 'Coin flip is ready. Pick your side when prompted.')
        return
      }

      addNotification('Duel update', 'A duel result was submitted.')
    })

    connection.on('DuelCompleted', (payload: DuelStatusResponse) => {
      setLatestDuelEvent(payload)
      addNotification('Duel completed', 'A duel has been resolved.')
    })

    connection.on('SessionStarted', (payload: { displayName?: string }) => {
      addNotification('Session activity', `${payload.displayName ?? 'A player'} started a session.`)
    })

    connection.on('SessionEnded', (payload: { displayName?: string }) => {
      addNotification('Session activity', `${payload.displayName ?? 'A player'} ended a session.`)
    })

    void connection
      .start()
      .then(() => {
        if (isMounted && connection.state === HubConnectionState.Connected) {
          setIsConnected(true)
        }
      })
      .catch(() => {
        if (isMounted) {
          setIsConnected(false)
        }
      })

    return () => {
      isMounted = false
      if (connectionRef.current === connection) {
        connectionRef.current = null
      }
      void connection.stop()
    }
  }, [accessToken, addNotification, getValidAccessToken, isAuthenticated])

  const value = useMemo(
    () => ({
      isConnected: isAuthenticated ? isConnected : false,
      latestDuelEvent: isAuthenticated ? latestDuelEvent : null,
      notifications: isAuthenticated ? notifications : [],
      clearNotification,
      pushEnabled,
    }),
    [clearNotification, isAuthenticated, isConnected, latestDuelEvent, notifications, pushEnabled],
  )

  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>
}
