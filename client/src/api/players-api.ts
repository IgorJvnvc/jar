import { request } from '../lib/http'
import type { ActiveSessionPlayerResponse, PlayerListItemResponse } from '../lib/types'

export const playersApi = {
  activeSessions(accessToken: string): Promise<ActiveSessionPlayerResponse[]> {
    return request<ActiveSessionPlayerResponse[]>('/api/players/active-sessions', {
      accessToken,
    })
  },

  list(accessToken: string): Promise<PlayerListItemResponse[]> {
    return request<PlayerListItemResponse[]>('/api/players', {
      accessToken,
    })
  },
}
