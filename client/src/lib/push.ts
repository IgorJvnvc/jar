import { Capacitor } from '@capacitor/core'
import {
  PushNotifications,
  type PushNotificationSchema,
  type Token,
} from '@capacitor/push-notifications'
import type { DevicePlatform } from './types'

export type PushRegistrationResult = {
  token: string
  platform: DevicePlatform
}

export const pushForegroundEventName = 'jar:push-foreground'

type PushMessagePayload = {
  title?: string
  body?: string
  data?: Record<string, unknown>
}

export function isPushAvailable(): boolean {
  return Capacitor.isNativePlatform()
}

export async function requestPushRegistration(
  onForegroundMessage?: (payload: PushMessagePayload) => void,
  onNavigate?: (route: string) => void,
): Promise<PushRegistrationResult | null> {
  if (!Capacitor.isNativePlatform()) {
    return null
  }

  return new Promise((resolve) => {
    void (async () => {
      const permission = await PushNotifications.requestPermissions()
      if (permission.receive !== 'granted') {
        resolve(null)
        return
      }

      PushNotifications.removeAllListeners()

      PushNotifications.addListener('registration', (token: Token) => {
        resolve({
          token: token.value,
          platform: nativePlatform(),
        })
      })

      PushNotifications.addListener('registrationError', () => {
        resolve(null)
      })

      if (onForegroundMessage) {
        PushNotifications.addListener('pushNotificationReceived', (notification: PushNotificationSchema) => {
          const payload = {
            title: notification.title,
            body: notification.body,
            data: notification.data,
          }

          onForegroundMessage(payload)
          emitForeground(payload)
        })
      } else {
        PushNotifications.addListener('pushNotificationReceived', (notification: PushNotificationSchema) => {
          emitForeground({
            title: notification.title,
            body: notification.body,
            data: notification.data,
          })
        })
      }

      PushNotifications.addListener('pushNotificationActionPerformed', (event) => {
        const route = asRoute(event.notification.data?.route)
        if (route) {
          if (onNavigate) {
            onNavigate(route)
            return
          }

          window.location.href = route
        }
      })

      await PushNotifications.register()
    })()
  })
}

export async function addPushNavigationListener(
  onNavigate: (route: string) => void,
): Promise<void> {
  if (!Capacitor.isNativePlatform()) {
    return
  }

  await PushNotifications.addListener('pushNotificationActionPerformed', (event) => {
    const route = asRoute(event.notification.data?.route)
    if (route) {
      onNavigate(route)
    }
  })
}

export async function removePushRegistration(token: string): Promise<void> {
  void token

  if (!Capacitor.isNativePlatform()) {
    return
  }

  PushNotifications.removeAllListeners()

  try {
    await PushNotifications.removeAllDeliveredNotifications()
  } catch {
    // ignore cleanup failures
  }
}

function nativePlatform(): DevicePlatform {
  const platform = Capacitor.getPlatform()
  if (platform === 'android') {
    return 'Android'
  }

  return 'Ios'
}

function asRoute(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null
  }

  if (!value.startsWith('/')) {
    return null
  }

  return value
}

function emitForeground(payload: PushMessagePayload): void {
  window.dispatchEvent(
    new CustomEvent<PushMessagePayload>(pushForegroundEventName, {
      detail: payload,
    }),
  )
}
