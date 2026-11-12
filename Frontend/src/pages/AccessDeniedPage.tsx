import { Link, useLocation } from 'react-router-dom'

export default function AccessDeniedPage() {
  const location = useLocation()
  const requestedPath = (location.state as { requestedPath?: string } | null)?.requestedPath
  const retryPath = requestedPath?.startsWith('/') && !requestedPath.startsWith('//') ? requestedPath : null

  return <main className="admin-shell">
    <section className="admin-panel" role="status">
      <span className="admin-kicker">ACCESS RESTRICTED</span>
      <h1>You don't have access to this page</h1>
      <p>Your account role does not include this page.</p>
      {retryPath && <Link className="admin-primary" to={retryPath}>Try the requested page again</Link>}
      <Link className="admin-primary" to="/">Return home</Link>
    </section>
  </main>
}
