import axios, { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios'

const apiBase = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5264'

export const api = axios.create({
  baseURL: apiBase,
  withCredentials: true,
})

type RetriableRequest = InternalAxiosRequestConfig & { authRetry?: boolean }

let accessToken: string | null = null
let refreshInFlight: Promise<string> | null = null
let onAccessTokenChanged: (token: string | null) => void = () => undefined
let onSessionExpired: () => void = () => undefined

export function setAuthInterceptors(
  tokenChanged: (token: string | null) => void,
  sessionExpired: () => void,
) {
  onAccessTokenChanged = tokenChanged
  onSessionExpired = sessionExpired
}

export function setAccessToken(token: string | null) {
  accessToken = token
  onAccessTokenChanged(token)
}

api.interceptors.request.use((request) => {
  if (accessToken) {
    request.headers.Authorization = `Bearer ${accessToken}`
  }
  return request
})

api.interceptors.response.use(
  response => response,
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 401 || !error.config) {
      return Promise.reject(error)
    }

    const original = error.config as RetriableRequest
    const url = original.url ?? ''
    if (/\/api\/auth\/(login|register|refresh|logout)/.test(url)) {
      return Promise.reject(error)
    }
    if (original.authRetry) {
      accessToken = null
      onAccessTokenChanged(null)
      onSessionExpired()
      return Promise.reject(error)
    }

    original.authRetry = true
    if (!refreshInFlight) {
      refreshInFlight = axios.post<{ accessToken: string }>(`${apiBase}/api/auth/refresh`, {}, {
        withCredentials: true,
      }).then(({ data }) => {
        if (!data.accessToken) throw new Error('Refresh response did not contain an access token.')
        accessToken = data.accessToken
        onAccessTokenChanged(accessToken)
        return accessToken
      }).catch((refreshError: unknown) => {
        accessToken = null
        onAccessTokenChanged(null)
        onSessionExpired()
        return Promise.reject(refreshError)
      }).finally(() => {
        refreshInFlight = null
      })
    }

    try {
      const refreshedToken = await refreshInFlight
      original.headers = AxiosHeaders.from(original.headers)
      original.headers.set('Authorization', `Bearer ${refreshedToken}`)
      return await api(original)
    } catch {
      return Promise.reject(error)
    }
  },
)
