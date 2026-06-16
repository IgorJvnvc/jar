import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from 'react'
import { useNavigate } from 'react-router-dom'
import { authApi } from '../api/auth-api'
import { notificationsApi } from '../api/notifications-api'
import { ApiError } from '../lib/http'
import {
  addPushNavigationListener,
  removePushRegistration,
  requestPushRegistration,
} from '../lib/push'
import type { AuthResponse } from '../lib/types'
import {
  AuthContext,
  authStorageKey,
  pushStorageKey,
  type StoredAuthState,
  type StoredPushState,
} from './auth-store'

export function AuthProvider({ children }: PropsWithChildren) {
  const navigate = useNavigate()
  const [state, setState] = useState<StoredAuthState | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const stateRef = useRef<StoredAuthState | null>(null)
  const pushStateRef = useRef<StoredPushState | null>(readStoredPushState())
  const navigateRef = useRef(navigate)
  const isInitializingRef = useRef(isInitializing)
  const pendingRouteRef = useRef<string | null>(null)

  useEffect(() => {
    navigateRef.current = navigate
  }, [navigate])

  useEffect(() => {
    isInitializingRef.current = isInitializing
  }, [isInitializing])

  // Routes a push tap to the in-app router. When the app is still booting or the
  // user is not authenticated yet (cold start), the route is queued and replayed
  // once auth state is ready.
  const handleDeepLink = useCallback((route: string) => {
    if (isInitializingRef.current || stateRef.current === null) {
      pendingRouteRef.current = route
      return
    }

    navigateRef.current(route)
  }, [])

  useEffect(() => {
    if (isInitializing || state === null) {
      return
    }

    const pending = pendingRouteRef.current
    if (pending) {
      pendingRouteRef.current = null
      navigateRef.current(pending)
    }
  }, [isInitializing, state])

  useEffect(() => {
    if (pushStateRef.current) {
      return
    }

    localStorage.removeItem(pushStorageKey)
  }, [])

  const writeState = useCallback((next: StoredAuthState | null) => {
    setState(next)
    stateRef.current = next

    if (next === null) {
      localStorage.removeItem(authStorageKey)
      return
    }

    localStorage.setItem(authStorageKey, JSON.stringify(next))
  }, [])

  const consumeAuthResponse = useCallback(
    (response: AuthResponse) => {
      writeState({
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
        user: response.user,
      })
    },
    [writeState],
  )

  const writePushState = useCallback((next: StoredPushState | null) => {
    pushStateRef.current = next

    if (next === null) {
      localStorage.removeItem(pushStorageKey)
      return
    }

    localStorage.setItem(pushStorageKey, JSON.stringify(next))
  }, [])

  useEffect(() => {
    const initialize = async () => {
      const raw = localStorage.getItem(authStorageKey)
      if (!raw) {
        setIsInitializing(false)
        return
      }

      try {
        const parsed = JSON.parse(raw) as StoredAuthState
        writeState(parsed)

        if (isExpired(parsed.accessTokenExpiresAtUtc)) {
          const refreshed = await authApi.refresh(parsed.refreshToken)
          consumeAuthResponse(refreshed)
        }
      } catch {
        writeState(null)
      } finally {
        setIsInitializing(false)
      }
    }

    void initialize()
  }, [consumeAuthResponse, writeState])

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await authApi.login({ email, password })
      consumeAuthResponse(response)
    },
    [consumeAuthResponse],
  )

  const register = useCallback(
    async (displayName: string, email: string, password: string) => {
      const response = await authApi.register({ displayName, email, password })
      consumeAuthResponse(response)
    },
    [consumeAuthResponse],
  )

  const logout = useCallback(async () => {
    const current = stateRef.current
    const currentPush = pushStateRef.current

    if (current?.accessToken && currentPush) {
      try {
        await notificationsApi.unregisterDevice(current.accessToken, { token: currentPush.token })
      } catch {
        // ignore device unregister errors on logout
      }
    }

    if (current?.accessToken && current.refreshToken) {
      try {
        await authApi.revoke(current.accessToken, current.refreshToken)
      } catch {
        // ignore revoke errors on logout
      }
    }

    if (currentPush) {
      try {
        await removePushRegistration(currentPush.token)
      } catch {
        // ignore local push cleanup errors
      }

      writePushState(null)
    }

    writeState(null)
  }, [writePushState, writeState])

  useEffect(() => {
    const accessToken = state?.accessToken
    if (!accessToken) {
      return
    }

    let isCanceled = false

    const syncPushToken = async () => {
      const existing = pushStateRef.current

      if (existing) {
        // Token already provisioned on a prior launch: requestPushRegistration is
        // skipped, so attach the deep-link listener here and re-sync the token.
        await addPushNavigationListener(handleDeepLink)

        try {
          await notificationsApi.registerDevice(accessToken, existing)
        } catch {
          // ignore registration sync errors, retry on next app session
        }

        return
      }

      try {
        const registration = await requestPushRegistration(undefined, handleDeepLink)
        if (!registration || isCanceled) {
          return
        }

        await notificationsApi.registerDevice(accessToken, registration)
        if (!isCanceled) {
          writePushState(registration)
        }
      } catch {
        // ignore push initialization failures
      }
    }

    void syncPushToken()

    return () => {
      isCanceled = true
    }
  }, [state?.accessToken, writePushState, handleDeepLink])

  const getValidAccessToken = useCallback(async (): Promise<string | null> => {
    if (!state) {
      return null
    }

    if (!isExpired(state.accessTokenExpiresAtUtc)) {
      return state.accessToken
    }

    try {
      const refreshed = await authApi.refresh(state.refreshToken)
      consumeAuthResponse(refreshed)
      return refreshed.accessToken
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        writeState(null)
      }

      throw error
    }
  }, [consumeAuthResponse, state, writeState])

  const value = useMemo(
    () => ({
      user: state?.user ?? null,
      accessToken: state?.accessToken ?? null,
      isInitializing,
      isAuthenticated: state !== null,
      login,
      register,
      logout,
      getValidAccessToken,
    }),
    [state, isInitializing, login, register, logout, getValidAccessToken],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function isExpired(expiresAtUtc: string): boolean {
  return new Date(expiresAtUtc).getTime() <= Date.now() + 15_000
}

function readStoredPushState(): StoredPushState | null {
  const raw = localStorage.getItem(pushStorageKey)
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as Partial<StoredPushState>
    if (
      typeof parsed.token !== 'string'
      || parsed.token.trim().length === 0
      || (parsed.platform !== 'Android' && parsed.platform !== 'Ios')
    ) {
      return null
    }

    return {
      token: parsed.token,
      platform: parsed.platform,
    }
  } catch {
    return null
  }
}
