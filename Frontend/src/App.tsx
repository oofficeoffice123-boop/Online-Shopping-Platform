import { Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import ProtectedRoute, { isPageAllowed } from './auth/ProtectedRoute'
import LoginPage from './pages/LoginPage'
import ManagePage from './pages/ManagePage'
import HomePage from './pages/HomePage'
import ProductDetailsPage from './pages/ProductDetailsPage'
import ProductListPage from './pages/ProductListPage'
import RegisterPage from './pages/RegisterPage'
import CartPage from './pages/CartPage'
import CheckoutPage from './pages/CheckoutPage'
import MyOrdersPage from './pages/MyOrdersPage'
import OrderDetailsPage from './pages/OrderDetailsPage'
import OrderConfirmationPage from './pages/OrderConfirmationPage'
import ManageReviewsPage from './pages/ManageReviewsPage'
import ManagerDashboardPage from './pages/ManagerDashboardPage'
import InventoryPage from './pages/InventoryPage'
import AdminUsersPage from './pages/AdminUsersPage'
import AdminAuditLogPage from './pages/AdminAuditLogPage'
import AccessDeniedPage from './pages/AccessDeniedPage'
import { getCart, type Cart } from './pages/shoppingApi'
import { api } from './auth/apiClient'
import type { AllowedPage } from './auth/ProtectedRoute'
import { useEffect, useState } from 'react'

export default function App() {
  const { user, isLoading, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [cart, setCart] = useState<Cart | null>(null)
  const [allowedPages, setAllowedPages] = useState<AllowedPage[]>([])
  const [pagesLoadedForUserId, setPagesLoadedForUserId] = useState<number | null>(null)
  const [pagesError, setPagesError] = useState('')
  const pagesLoading = Boolean(user && pagesLoadedForUserId !== user.userId)

  useEffect(() => {
    let active = true
    if (!user) {
      setAllowedPages([])
      setPagesLoadedForUserId(null)
      setPagesError('')
      return () => { active = false }
    }

    setAllowedPages([])
    setPagesError('')
    void api.get<AllowedPage[]>('/api/pages/my-pages')
      .then(({ data }) => {
        if (active) {
          setAllowedPages(data)
          setPagesLoadedForUserId(user.userId)
        }
      })
      .catch(() => {
        if (active) {
          setAllowedPages([])
          setPagesError('Page access could not be loaded. Refresh and try again.')
          setPagesLoadedForUserId(user.userId)
        }
      })
    return () => { active = false }
  }, [user])

  useEffect(() => {
    if (location.pathname !== '/access-denied' || !user || pagesLoading) return
    const requestedPath = (location.state as { requestedPath?: string } | null)?.requestedPath
    if (requestedPath && isPageAllowed(requestedPath, allowedPages)) {
      navigate(requestedPath, { replace: true })
    }
  }, [allowedPages, location.pathname, location.state, navigate, pagesLoading, user])

  useEffect(() => {
    window.scrollTo(0, 0)
  }, [location.pathname, location.search])

  useEffect(() => {
    let active = true
    async function refreshCart() {
      if (user?.role !== 'Customer') {
        setCart(null)
        return
      }
      try {
        const current = await getCart()
        if (active) setCart(current)
      } catch {
        if (active) setCart(null)
      }
    }
    void refreshCart()
    const update = () => void refreshCart()
    window.addEventListener('z-square-cart-updated', update)
    return () => {
      active = false
      window.removeEventListener('z-square-cart-updated', update)
    }
  }, [user])

  async function signOut() {
    await logout()
    navigate('/', { replace: true })
  }

  return <>
    <header className="auth-nav">
      <Link to="/" className="auth-brand">Z SQUARE</Link>
      <nav aria-label="Main navigation">
        <Link to="/">Home</Link>
        {!user && <Link to="/products">Shop</Link>}
        {allowedPages.filter(page => page.route !== '/checkout').map(page => <Link key={page.pageId} to={page.route} className={page.route === '/cart' ? 'cart-nav-link' : undefined}>
          {page.name === 'Cart' ? <>Bag{cart?.items.length ? <span>{cart.items.reduce((sum, item) => sum + item.quantity, 0)}</span> : null}</> : page.name}
        </Link>)}
      </nav>
      <div className="auth-account">
        {isLoading ? <span className="auth-nav-muted">Restoring session…</span> : user
          ? <><span className="auth-nav-muted">{user.fullName} · {user.role}</span><button type="button" onClick={() => void signOut()}>Sign out</button></>
          : <><Link to="/login">Sign in</Link><Link className="register-link" to="/register">Join</Link></>}
      </div>
    </header>

    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/products" element={<ProductListPage />} />
      <Route path="/products/:id" element={<ProductDetailsPage />} />
      <Route path="/cart" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><CartPage /></ProtectedRoute>} />
      <Route path="/checkout" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><CheckoutPage /></ProtectedRoute>} />
      <Route path="/orders" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><MyOrdersPage /></ProtectedRoute>} />
      <Route path="/orders/:id/confirmation" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><OrderConfirmationPage /></ProtectedRoute>} />
      <Route path="/orders/:id" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><OrderDetailsPage /></ProtectedRoute>} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/manage" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><ManagePage /></ProtectedRoute>} />
      <Route path="/manage/dashboard" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><ManagerDashboardPage /></ProtectedRoute>} />
      <Route path="/manage/inventory" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><InventoryPage /></ProtectedRoute>} />
      <Route path="/manage/reviews" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><ManageReviewsPage /></ProtectedRoute>} />
      <Route path="/admin/users" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><AdminUsersPage /></ProtectedRoute>} />
      <Route path="/admin/audit-logs" element={<ProtectedRoute allowedPages={allowedPages} pagesLoading={pagesLoading} pagesError={pagesError}><AdminAuditLogPage /></ProtectedRoute>} />
      <Route path="/access-denied" element={<AccessDeniedPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  </>
}
import './App.css'
import './pages/Browse.css'
