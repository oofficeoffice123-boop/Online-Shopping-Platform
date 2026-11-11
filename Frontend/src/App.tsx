import { Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import ProtectedRoute from './auth/ProtectedRoute'
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
import { getCart, type Cart } from './pages/shoppingApi'
import { useEffect, useState } from 'react'

export default function App() {
  const { user, isLoading, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [cart, setCart] = useState<Cart | null>(null)

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
        <Link to="/products">Shop</Link>
        {user?.role === 'Customer' && <><Link to="/orders">My orders</Link><Link to="/cart" className="cart-nav-link">Bag{cart?.items.length ? <span>{cart.items.reduce((sum, item) => sum + item.quantity, 0)}</span> : null}</Link></>}
        {user && (user.role === 'Manager' || user.role === 'Admin') && <><Link to="/manage">Manage</Link><Link to="/manage/reviews">Review moderation</Link></>}
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
      <Route path="/cart" element={<ProtectedRoute allowedRoles={['Customer']}><CartPage /></ProtectedRoute>} />
      <Route path="/checkout" element={<ProtectedRoute allowedRoles={['Customer']}><CheckoutPage /></ProtectedRoute>} />
      <Route path="/orders" element={<ProtectedRoute allowedRoles={['Customer']}><MyOrdersPage /></ProtectedRoute>} />
      <Route path="/orders/:id/confirmation" element={<ProtectedRoute allowedRoles={['Customer']}><OrderConfirmationPage /></ProtectedRoute>} />
      <Route path="/orders/:id" element={<ProtectedRoute allowedRoles={['Customer']}><OrderDetailsPage /></ProtectedRoute>} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/manage" element={<ProtectedRoute allowedRoles={['Manager', 'Admin']}><ManagePage /></ProtectedRoute>} />
      <Route path="/manage/reviews" element={<ProtectedRoute allowedRoles={['Manager', 'Admin']}><ManageReviewsPage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  </>
}
import './App.css'
import './pages/Browse.css'
