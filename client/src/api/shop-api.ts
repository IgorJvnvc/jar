import { request } from '../lib/http'
import type { CueItemResponse } from '../lib/types'

export const shopApi = {
  listCues(accessToken: string): Promise<CueItemResponse[]> {
    return request<CueItemResponse[]>('/api/shop/cues', {
      accessToken,
    })
  },

  purchaseCue(accessToken: string, cueItemId: string): Promise<void> {
    return request<void>('/api/shop/cues/purchase', {
      method: 'POST',
      accessToken,
      body: { cueItemId },
    })
  },

  equipCue(accessToken: string, cueItemId: string): Promise<void> {
    return request<void>('/api/shop/cues/equip', {
      method: 'POST',
      accessToken,
      body: { cueItemId },
    })
  },
}
