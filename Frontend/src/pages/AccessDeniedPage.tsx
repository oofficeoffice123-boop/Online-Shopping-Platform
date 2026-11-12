import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { roleHome } from '../auth/roleHome'

export default function AccessDeniedPage() {
  const { user } = useAuth()

  return <main className="admin-shell">
    <section className="admin-panel" role="status">
      <span className="admin-kicker">ACCESS RESTRICTED</span>
      <h1>You don't have access to this page</h1>
      <p>Your account role does not include this page.</p>
      <Link className="admin-primary" to={roleHome(user?.role)}>Return to your home page</Link>
    </section>
  </main>
}
