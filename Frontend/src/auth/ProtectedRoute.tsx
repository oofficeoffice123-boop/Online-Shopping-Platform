import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from './AuthContext'

export type AllowedPage = { pageId: number; name: string; route: string; sortOrder: number }

export function isPageAllowed(pathname: string, pages: AllowedPage[]) {
  const currentRoute = pathname.replace(/\/+$/, '') || '/'
  return pages.some(page => {
    const pageRoute = page.route.replace(/\/+$/, '') || '/'
    return currentRoute === pageRoute ||
      (['/products', '/orders'].includes(pageRoute) && currentRoute.startsWith(`${pageRoute}/`))
  })
}

export default function ProtectedRoute({
  children,
  allowedPages = [],
  pagesLoading = false,
  pagesError = '',
}: {
  children: ReactNode
  allowedPages?: AllowedPage[]
  pagesLoading?: boolean
  pagesError?: string
}) {
  const { user, isLoading } = useAuth()
  const location = useLocation()
  if (isLoading || (user && pagesLoading)) return <p className="auth-loading">Restoring your session...</p>
  if (!user) return <Navigate to="/login" replace state={{ returnTo: `${location.pathname}${location.search}` }} />
  if (pagesError) return <main className="admin-shell"><div className="admin-alert" role="alert">{pagesError} <button type="button" className="admin-secondary" onClick={() => window.location.reload()}>Retry</button></div></main>

  if (!isPageAllowed(location.pathname, allowedPages)) return <Navigate to="/access-denied" replace state={{ requestedPath: location.pathname }} />
  return children
}
