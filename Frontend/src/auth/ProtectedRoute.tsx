import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth, type AuthUser } from './AuthContext'

export default function ProtectedRoute({
  children,
  allowedRoles,
}: {
  children: ReactNode
  allowedRoles?: AuthUser['role'][]
}) {
  const { user, isLoading } = useAuth()
  const location = useLocation()
  if (isLoading) return <p className="auth-loading">Restoring your session…</p>
  if (!user) return <Navigate to="/login" replace state={{ returnTo: `${location.pathname}${location.search}` }} />
  if (allowedRoles && !allowedRoles.includes(user.role)) return <Navigate to="/" replace />
  return children
}
