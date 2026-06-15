import { request } from '../lib/http'
import type { ProfileResponse, UpdateProfileRequest } from '../lib/types'

type PayDebtResponse = {
  paidPoints: number
  profile: ProfileResponse
}

export const profileApi = {
  getMine(accessToken: string): Promise<ProfileResponse> {
    return request<ProfileResponse>('/api/profile/me', {
      accessToken,
    })
  },

  updateMine(accessToken: string, payload: UpdateProfileRequest): Promise<ProfileResponse> {
    return request<ProfileResponse>('/api/profile/me', {
      method: 'PUT',
      accessToken,
      body: payload,
    })
  },

  payDebt(accessToken: string, amount: number): Promise<PayDebtResponse> {
    return request<PayDebtResponse>('/api/profile/pay-debt', {
      method: 'POST',
      accessToken,
      body: { amount },
    })
  },
}
