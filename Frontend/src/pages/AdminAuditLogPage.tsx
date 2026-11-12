import { useCallback, useEffect, useState } from 'react'
import { api } from '../auth/apiClient'
import './AdminWorkspace.css'

type AuditRow = { auditLogId: number; action: string; entityType: string; entityId: string | null; detailsJson: string | null; createdAt: string; actorUserId: number | null; actorName: string; actorEmail: string | null }
type AuditPage = { items: AuditRow[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export default function AdminAuditLogPage() {
  const [data, setData] = useState<AuditPage | null>(null)
  const [page, setPage] = useState(1)
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState('')
  const [entityType, setEntityType] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const response = await api.get<AuditPage>('/api/admin/audit-logs', { params: { page, pageSize: 30, search: search || undefined, entityType: entityType || undefined } })
      setData(response.data)
    } catch {
      setError('Audit history could not be loaded. Refresh or sign in again.')
    } finally { setLoading(false) }
  }, [page, search, entityType])

  useEffect(() => { void load() }, [load])

  return <main className="admin-shell">
    <header className="admin-page-heading"><div><p className="admin-kicker">Z SQUARE / ADMINISTRATION</p><h1>Audit history</h1></div><button className="admin-secondary" type="button" disabled={loading} onClick={() => void load()}>Refresh</button></header>
    {error && <div className="admin-alert" role="alert">{error}</div>}
    <section className="admin-panel">
      <div className="admin-panel-heading"><div><span className="admin-kicker">RECENT ACTIVITY</span><h2>{data?.totalCount ?? 0} recorded actions</h2></div></div>
      <div className="admin-filters"><form onSubmit={event => { event.preventDefault(); setPage(1); setSearch(searchDraft.trim()) }}><input aria-label="Search audit history" placeholder="Search user, action, or record ID" value={searchDraft} onChange={event => setSearchDraft(event.target.value)} /><button className="admin-secondary">Search</button></form><label>Record type<select value={entityType} onChange={event => { setPage(1); setEntityType(event.target.value) }}><option value="">All records</option><option value="User">Users</option><option value="Review">Reviews</option></select></label></div>
      <div className="admin-table-scroll"><table className="admin-table audit-table"><thead><tr><th>Action</th><th>Record</th><th>Changed by</th><th>Date and time</th><th>Details</th></tr></thead><tbody>
        {loading && !data ? <tr><td colSpan={5} className="admin-empty">Loading audit history…</td></tr> : data?.items.length ? data.items.map(entry => <tr key={entry.auditLogId}><td><span className="audit-action">{entry.action.replaceAll('.', ' ')}</span><small>Log #{entry.auditLogId}</small></td><td>{entry.entityType}{entry.entityId ? ` #${entry.entityId}` : ''}</td><td><strong>{entry.actorName}</strong><small>{entry.actorEmail ?? 'System action'}</small></td><td>{new Date(entry.createdAt).toLocaleString()}</td><td>{entry.detailsJson ? <details className="audit-details"><summary>View</summary><pre>{JSON.stringify(JSON.parse(entry.detailsJson), null, 2)}</pre></details> : '—'}</td></tr>) : !error && <tr><td colSpan={5} className="admin-empty">No audit events have been recorded yet.</td></tr>}
      </tbody></table></div>
      {data && data.totalPages > 1 && <footer className="admin-pagination"><span>{data.totalCount} recorded actions</span><div><button className="admin-secondary" disabled={page <= 1 || loading} onClick={() => setPage(value => value - 1)}>Previous</button><span>Page {page} of {data.totalPages}</span><button className="admin-secondary" disabled={page >= data.totalPages || loading} onClick={() => setPage(value => value + 1)}>Next</button></div></footer>}
    </section>
  </main>
}
