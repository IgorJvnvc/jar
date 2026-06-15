import { request } from '../lib/http'
import type {
  EndSessionRequest,
  SessionResponse,
  StartSessionRequest,
} from '../lib/types'

export const sessionsApi = {
  getActive(accessToken: string): Promise<SessionResponse> {
    return request<SessionResponse>('/api/sessions/active', {
      accessToken,
    })
  },

  start(accessToken: string, payload: StartSessionRequest): Promise<SessionResponse> {
    return request<SessionResponse>('/api/sessions/start', {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  end(accessToken: string, sessionId: string, payload: EndSessionRequest): Promise<SessionResponse> {
    return request<SessionResponse>(`/api/sessions/${sessionId}/end`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  recent(accessToken: string): Promise<SessionResponse[]> {
    return request<SessionResponse[]>('/api/sessions/recent', {
      accessToken,
    })
  },
}
