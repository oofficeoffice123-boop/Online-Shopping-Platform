import axios from 'axios'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

function errorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string; title?: string; detail?: string } | undefined
    return data?.message ?? data?.detail ?? data?.title ?? 'Could not sign in. Check your details and try again.'
  }
  return 'Could not sign in. Try again.'
}

export default function LoginPage() {
  const { login, logout, user, isLoading } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (!isLoading && user) return <section className="auth-card">
    <p className="eyebrow">ACCOUNT ALREADY SIGNED IN</p>
    <h1>{user.fullName}</h1>
    <p>You are signed in as {user.email} ({user.role}). Sign out to use another account.</p>
    <button className="search-button" type="button" onClick={() => void logout()}>Sign out and switch account</button>
  </section>

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      const signedInUser = await login({ email, password })
      const landingPage = signedInUser.role === 'Customer'
        ? '/products'
        : signedInUser.role === 'Manager' ? '/manage/dashboard' : '/admin/users'
      navigate(landingPage, { replace: true, state: null })
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
