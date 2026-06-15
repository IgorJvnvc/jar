const fallbackApiBaseUrl = 'https://localhost:7253'

export const config = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? fallbackApiBaseUrl,
  signalrBaseUrl: import.meta.env.VITE_SIGNALR_BASE_URL ?? import.meta.env.VITE_API_BASE_URL ?? fallbackApiBaseUrl,
} as const
