import { ApiClient } from './client'
import { getOidcAccessToken } from './token-provider'

export * from './client'
export * from './token-provider'

export const apiClient = new ApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL,
  tokenProvider: { getAccessToken: getOidcAccessToken },
})
