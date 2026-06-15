import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  isPushAvailable,
  pushForegroundEventName,
  removePushRegistration,
  requestPushRegistration,
} from './push'

const state = vi.hoisted(() => {
  const listeners = new Map<string, Array<(payload: unknown) => void>>()

  return {
    isNative: true,
    platform: 'android',
    permissionReceive: 'granted' as 'granted' | 'denied',
    tokenValue: 'token-123',
    listeners,
    removeAllListenersCalls: 0,
    removeAllDeliveredCalls: 0,
    registerCalls: 0,
    registrationError: false,
  }
})

vi.mock('@capacitor/core', () => ({
  Capacitor: {
    isNativePlatform: () => state.isNative,
    getPlatform: () => state.platform,
  },
}))

vi.mock('@capacitor/push-notifications', () => ({
  PushNotifications: {
    requestPermissions: vi.fn(async () => ({ receive: state.permissionReceive })),
    removeAllListeners: vi.fn(() => {
      state.removeAllListenersCalls += 1
      state.listeners.clear()
    }),
    addListener: vi.fn((event: string, listener: (payload: unknown) => void) => {
      const current = state.listeners.get(event) ?? []
      current.push(listener)
      state.listeners.set(event, current)
      return Promise.resolve({ remove: vi.fn() })
    }),
    register: vi.fn(async () => {
      state.registerCalls += 1

      if (state.registrationError) {
        for (const listener of state.listeners.get('registrationError') ?? []) {
          listener({})
        }
        return
      }

      for (const listener of state.listeners.get('registration') ?? []) {
        listener({ value: state.tokenValue })
      }
    }),
    removeAllDeliveredNotifications: vi.fn(async () => {
      state.removeAllDeliveredCalls += 1
    }),
  },
}))

function emitListener(event: string, payload: unknown): void {
  for (const listener of state.listeners.get(event) ?? []) {
    listener(payload)
  }
}

describe('push', () => {
  beforeEach(() => {
    state.isNative = true
    state.platform = 'android'
    state.permissionReceive = 'granted'
    state.tokenValue = 'token-123'
    state.removeAllListenersCalls = 0
    state.removeAllDeliveredCalls = 0
    state.registerCalls = 0
    state.registrationError = false
    state.listeners.clear()
  })

  it('isPushAvailable returns true only for native platform', () => {
    state.isNative = true
    expect(isPushAvailable()).toBe(true)

    state.isNative = false
    expect(isPushAvailable()).toBe(false)
  })

  it('requestPushRegistration returns null when not running natively', async () => {
    state.isNative = false

    const result = await requestPushRegistration()

    expect(result).toBeNull()
    expect(state.registerCalls).toBe(0)
  })

  it('requestPushRegistration returns null when permission is denied', async () => {
    state.permissionReceive = 'denied'

    const result = await requestPushRegistration()

    expect(result).toBeNull()
    expect(state.registerCalls).toBe(0)
  })

  it('requestPushRegistration returns Android token and platform', async () => {
    state.platform = 'android'
    state.tokenValue = 'android-token'

    const result = await requestPushRegistration()

    expect(result).toEqual({
      token: 'android-token',
      platform: 'Android',
    })
    expect(state.registerCalls).toBe(1)
    expect(state.removeAllListenersCalls).toBe(1)
  })

  it('requestPushRegistration returns iOS platform for non-android native platform', async () => {
    state.platform = 'ios'
    state.tokenValue = 'ios-token'

    const result = await requestPushRegistration()

    expect(result).toEqual({
      token: 'ios-token',
      platform: 'Ios',
    })
  })

  it('requestPushRegistration resolves null on registration error', async () => {
    state.registrationError = true

    const result = await requestPushRegistration()

    expect(result).toBeNull()
    expect(state.registerCalls).toBe(1)
  })

  it('emits foreground events and invokes callback on pushNotificationReceived', async () => {
    const callback = vi.fn()
    const windowListener = vi.fn()
    window.addEventListener(pushForegroundEventName, windowListener)

    await requestPushRegistration(callback)

    emitListener('pushNotificationReceived', {
      title: 'High Noon challenge',
      body: 'Opponent challenged you.',
      data: { route: '/duels', duelId: '123' },
    })

    expect(callback).toHaveBeenCalledTimes(1)
    expect(callback).toHaveBeenCalledWith({
      title: 'High Noon challenge',
      body: 'Opponent challenged you.',
      data: { route: '/duels', duelId: '123' },
    })

    expect(windowListener).toHaveBeenCalledTimes(1)
    const event = windowListener.mock.calls[0]?.[0] as CustomEvent
    expect(event.detail).toEqual({
      title: 'High Noon challenge',
      body: 'Opponent challenged you.',
      data: { route: '/duels', duelId: '123' },
    })
  })

  it('navigates to route from push action payload when route is valid', async () => {
    const onNavigate = vi.fn()
    await requestPushRegistration(undefined, onNavigate)

    emitListener('pushNotificationActionPerformed', {
      notification: {
        data: {
          route: '/duels',
        },
      },
    })

    expect(onNavigate).toHaveBeenCalledWith('/duels')
  })

  it('ignores push action route when route is invalid', async () => {
    const onNavigate = vi.fn()
    await requestPushRegistration(undefined, onNavigate)

    emitListener('pushNotificationActionPerformed', {
      notification: {
        data: {
          route: 'duels',
        },
      },
    })

    expect(onNavigate).not.toHaveBeenCalled()
  })

  it('removePushRegistration cleans up listeners and delivered notifications on native', async () => {
    state.isNative = true

    await removePushRegistration('token-any')

    expect(state.removeAllListenersCalls).toBe(1)
    expect(state.removeAllDeliveredCalls).toBe(1)
  })

  it('removePushRegistration is a no-op on non-native', async () => {
    state.isNative = false

    await removePushRegistration('token-any')

    expect(state.removeAllListenersCalls).toBe(0)
    expect(state.removeAllDeliveredCalls).toBe(0)
  })
})
