import { request } from '../lib/http'
import type {
  CoinSide,
  DuelListResponse,
  DuelResultChoice,
  DuelStatus,
  DuelStatusResponse,
} from '../lib/types'

type CreateDuelRequest = {
  opponentUserId: string
}

type RespondDuelRequest = {
  accept: boolean
}

type SubmitDuelResultRequest = {
  choice: DuelResultChoice
}

type ChooseCoinSideRequest = {
  side: CoinSide
}

export const duelsApi = {
  list(accessToken: string, status?: DuelStatus): Promise<DuelListResponse> {
    const search = status ? `?status=${encodeURIComponent(status)}` : ''
    return request<DuelListResponse>(`/api/duels${search}`, {
      accessToken,
    })
  },

  getById(accessToken: string, duelId: string): Promise<DuelStatusResponse> {
    return request<DuelStatusResponse>(`/api/duels/${duelId}`, {
      accessToken,
    })
  },

  create(accessToken: string, payload: CreateDuelRequest): Promise<DuelStatusResponse> {
    return request<DuelStatusResponse>('/api/duels', {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  respond(accessToken: string, duelId: string, payload: RespondDuelRequest): Promise<DuelStatusResponse> {
    return request<DuelStatusResponse>(`/api/duels/${duelId}/respond`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  submitResult(accessToken: string, duelId: string, payload: SubmitDuelResultRequest): Promise<DuelStatusResponse> {
    return request<DuelStatusResponse>(`/api/duels/${duelId}/submit-result`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  chooseCoinSide(accessToken: string, duelId: string, payload: ChooseCoinSideRequest): Promise<DuelStatusResponse> {
    return request<DuelStatusResponse>(`/api/duels/${duelId}/coin-flip/choose`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },
}
