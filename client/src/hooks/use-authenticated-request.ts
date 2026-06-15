import { useCallback } from 'react'
import { useAuth } from '../state/use-auth'

export function useAuthenticatedRequest() {
  const { getValidAccessToken } = useAuth()

  return useCallback(
    async <T>(executor: (accessToken: string) => Promise<T>) => {
      const accessToken = await getValidAccessToken()

      if (!accessToken) {
        throw new Error('You are not authenticated.')
      }

      return executor(accessToken)
    },
    [getValidAccessToken],
  )
}
