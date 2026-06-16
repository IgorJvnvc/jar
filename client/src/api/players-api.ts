import { request } from '../lib/http'
import type {
  ActiveSessionPlayerResponse,
  DuelLeaderboardEntryResponse,
  LeaderboardEntryResponse,
  PlayerListItemResponse,
} from '../lib/types'

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

  leaderboard(accessToken: string): Promise<LeaderboardEntryResponse[]> {
    return request<LeaderboardEntryResponse[]>('/api/players/leaderboard', {
      accessToken,
    })
  },

  duelLeaderboard(accessToken: string): Promise<DuelLeaderboardEntryResponse[]> {
    return request<DuelLeaderboardEntryResponse[]>('/api/players/duel-leaderboard', {
      accessToken,
    })
  },
}
