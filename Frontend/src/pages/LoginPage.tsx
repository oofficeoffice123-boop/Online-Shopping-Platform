import axios from 'axios'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

function errorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string; title?: string; detail?: string } | undefined
    return data?.message ?? data?.detail ?? data?.title ?? 'Could not sign in. Check your details and try again.'
  }
  return 'Could not sign in. Try again.'
}

export default function LoginPage() {
  const { login, user, isLoading } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const returnTo = (location.state as { returnTo?: string } | null)?.returnTo
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (!isLoading && user) return <Navigate to={user.role === 'Customer' ? '/' : '/manage'} replace />

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      const signedInUser = await login({ email, password })
      navigate(signedInUser.role === 'Customer' ? (returnTo?.startsWith('/') ? returnTo : '/') : '/manage', { replace: true })
    } catch (submitError) {
      setError(errorMessage(submitError))
    } finally {
      setBusy(false)
    }
  }

  return <section className="auth-card">
    <p className="eyebrow">WELCOME BACK</p>
    <h1>Sign in</h1>
    <form onSubmit={submit} className="auth-form">
      <label>Email<input type="email" autoComplete="email" required maxLength={256} value={email} onChange={event => setEmail(event.target.value)} /></label>
      <label>Password<input type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /></label>
      {error && <p className="error" role="alert">{error}</p>}
      <button className="search-button" disabled={busy || isLoading}>{busy ? 'Signing in…' : 'Sign in'}</button>
    </form>
    <p className="auth-alternate">New to Z SQUARE? <Link to="/register">Create an account</Link></p>
  </section>
}
