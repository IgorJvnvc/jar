import { createContext } from 'react'
import type { DevicePlatform, UserSummary } from '../lib/types'

export const authStorageKey = 'pool-tracker.auth.v1'
export const pushStorageKey = 'pool-tracker.push.v1'

export type StoredAuthState = {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAtUtc: string
  user: UserSummary
}

export type StoredPushState = {
  token: string
  platform: DevicePlatform
}

export type AuthContextValue = {
  user: UserSummary | null
  accessToken: string | null
  isInitializing: boolean
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (displayName: string, email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  getValidAccessToken: () => Promise<string | null>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
