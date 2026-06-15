import { request } from '../lib/http'
import type { AuthResponse, UserSummary } from '../lib/types'

type RegisterInput = {
  displayName: string
  email: string
  password: string
}

type LoginInput = {
  email: string
  password: string
}

export const authApi = {
  register(input: RegisterInput): Promise<AuthResponse> {
    return request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: input,
    })
  },

  login(input: LoginInput): Promise<AuthResponse> {
    return request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: input,
    })
  },

  refresh(refreshToken: string): Promise<AuthResponse> {
    return request<AuthResponse>('/api/auth/refresh', {
      method: 'POST',
      body: { refreshToken },
    })
  },

  me(accessToken: string): Promise<UserSummary> {
    return request<UserSummary>('/api/auth/me', {
      accessToken,
    })
  },

  revoke(accessToken: string, refreshToken: string): Promise<void> {
    return request<void>('/api/auth/revoke', {
      method: 'POST',
      accessToken,
      body: { refreshToken },
    })
  },
}
