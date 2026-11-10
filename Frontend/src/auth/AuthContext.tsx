import axios from 'axios'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAccessToken as setApiAccessToken, setAuthInterceptors } from './apiClient'

export type AuthUser = { userId: number; fullName: string; email: string; role: 'Customer' | 'Manager' | 'Admin'; mustChangePassword?: boolean }
type AuthResult = { accessToken: string; accessTokenExpiresAt: string; user: AuthUser }
type Credentials = { email: string; password: string }
type Registration = Credentials & { fullName: string; phone?: string }

type AuthContextValue = {
  user: AuthUser | null
  accessToken: string | null
  isLoading: boolean
  login: (credentials: Credentials) => Promise<AuthUser>
  register: (request: Registration) => Promise<AuthUser>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)
let initialSessionRequest: Promise<AuthResult | null> | null = null

function restoreInitialSession() {
  initialSessionRequest ??= api.post<AuthResult>('/api/auth/refresh')
    .then(({ data }) => data)
    .catch(() => null)
  return initialSessionRequest
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [accessToken, setAccessTokenState] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const applySession = useCallback((result: AuthResult) => {
    setUser(result.user)
    setApiAccessToken(result.accessToken)
  }, [])

  const clearSession = useCallback(() => {
    setUser(null)
    setApiAccessToken(null)
  }, [])

  useEffect(() => {
    setAuthInterceptors(setAccessTokenState, () => {
      clearSession()
      if (window.location.pathname !== '/login') window.location.assign('/login')
    })

    let mounted = true
    void restoreInitialSession().then(result => {
      if (mounted && result) applySession(result)
      else if (mounted) clearSession()
    }).finally(() => {
      if (mounted) setIsLoading(false)
    })

    return () => {
      mounted = false
    }
  }, [applySession, clearSession])

  const login = useCallback(async (credentials: Credentials) => {
    const { data } = await api.post<AuthResult>('/api/auth/login', credentials)
    applySession(data)
    return data.user
  }, [applySession])

  const register = useCallback(async (request: Registration) => {
    const { data } = await api.post<AuthResult>('/api/auth/register', request)
    applySession(data)
    return data.user
  }, [applySession])

  const logout = useCallback(async () => {
    try {
      await api.post('/api/auth/logout')
    } catch (error) {
      if (!axios.isAxiosError(error)) throw error
    } finally {
      clearSession()
    }
  }, [clearSession])

  const value = useMemo(() => ({ user, accessToken, isLoading, login, register, logout }),
    [user, accessToken, isLoading, login, register, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider.')
  return context
}
