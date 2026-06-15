import { request } from '../lib/http'
import type { DevicePlatform } from '../lib/types'

type RegisterDeviceTokenRequest = {
  token: string
  platform: DevicePlatform
}

type UnregisterDeviceTokenRequest = {
  token: string
}

export const notificationsApi = {
  registerDevice(accessToken: string, payload: RegisterDeviceTokenRequest): Promise<void> {
    return request<void>('/api/notifications/register-device', {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  unregisterDevice(accessToken: string, payload: UnregisterDeviceTokenRequest): Promise<void> {
    return request<void>('/api/notifications/unregister-device', {
      method: 'DELETE',
      accessToken,
      body: payload,
    })
  },
}
