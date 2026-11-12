import axios from 'axios'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { roleHome } from '../auth/roleHome'

function errorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string; title?: string; detail?: string; errors?: Record<string, string[]> } | undefined
    return data?.message ?? data?.detail ?? data?.title ?? Object.values(data?.errors ?? {}).flat().join(' ') ?? 'Could not create the account.'
  }
  return 'Could not create the account. Try again.'
}

export default function RegisterPage() {
  const { register, user, isLoading } = useAuth()
  const navigate = useNavigate()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (!isLoading && user) return <Navigate to={roleHome(user.role)} replace state={null} />

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      await register({ fullName, email, phone: phone || undefined, password })
      navigate(roleHome('Customer'), { replace: true, state: null })
    } catch (submitError) {
      setError(errorMessage(submitError))
    } finally {
      setBusy(false)
    }
  }

  return <section className="auth-card">
    <p className="eyebrow">JOIN Z SQUARE</p>
    <h1>Create account</h1>
    <form onSubmit={submit} className="auth-form">
      <label>Full name<input autoComplete="name" required maxLength={200} value={fullName} onChange={event => setFullName(event.target.value)} /></label>
      <label>Email<input type="email" autoComplete="email" required maxLength={256} value={email} onChange={event => setEmail(event.target.value)} /></label>
      <label>Phone <span className="optional-label">optional</span><input type="tel" autoComplete="tel" maxLength={30} value={phone} onChange={event => setPhone(event.target.value)} /></label>
      <label>Password<input type="password" autoComplete="new-password" required minLength={8} value={password} onChange={event => setPassword(event.target.value)} /></label>
      {error && <p className="error" role="alert">{error}</p>}
      <button className="search-button" disabled={busy || isLoading}>{busy ? 'Creating account…' : 'Create account'}</button>
    </form>
    <p className="auth-alternate">Already have an account? <Link to="/login">Sign in</Link></p>
  </section>
}
