import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import { api } from '../auth/apiClient'
import { PageHeading, PanelHeading } from '../components/ui/PageHeading'
import { Feedback } from '../components/ui/Feedback'
import { Pagination } from '../components/ui/Pagination'
import { Modal } from '../components/ui/Modal'
import './AdminWorkspace.css'

type UserRow = { userId: number; fullName: string; email: string; phone: string | null; role: 'Customer' | 'Manager' | 'Admin'; isActive: boolean; isDeleted: boolean; createdAt: string }
type UserPage = { items: UserRow[]; page: number; pageSize: number; totalCount: number; totalPages: number }
type UserDraft = { fullName: string; email: string; phone: string; role: UserRow['role']; isActive: boolean }

const blankUser = { fullName: '', email: '', phone: '', role: 'Customer' as UserRow['role'], password: '' }

function errorMessage(error: any, fallback: string) {
  const data = error?.response?.data
  const fieldErrors = Object.values(data?.errors ?? {}).flat()
  return (fieldErrors[0] as string | undefined) ?? data?.detail ?? data?.title ?? fallback
}

export default function AdminUsersPage() {
  const { user: currentUser } = useAuth()
  const [data, setData] = useState<UserPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [deleted, setDeleted] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [createDraft, setCreateDraft] = useState(blankUser)
  const [editing, setEditing] = useState<UserRow | null>(null)
  const [editDraft, setEditDraft] = useState<UserDraft | null>(null)
  const [newPassword, setNewPassword] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const response = await api.get<UserPage>('/api/admin/users', {
        params: { page, pageSize: 20, search: search || undefined, role: role || undefined, deleted: deleted === '' ? undefined : deleted === 'true' },
      })
      setData(response.data)
    } catch (requestError) {
      setError(errorMessage(requestError, 'Users could not be loaded. Refresh or sign in again.'))
    } finally {
      setLoading(false)
    }
  }, [page, search, role, deleted])

  useEffect(() => { void load() }, [load])

  async function createUser(event: FormEvent) {
    event.preventDefault()
    setSaving(true); setError(''); setNotice('')
    try {
      await api.post('/api/admin/users', createDraft)
      setCreateDraft(blankUser); setCreating(false); setNotice('Account created. The user can sign in with the password you set.')
      await load()
    } catch (requestError) {
      setError(errorMessage(requestError, 'The account could not be created.'))
    } finally { setSaving(false) }
  }

  function openEditor(person: UserRow) {
    setEditing(person)
    setEditDraft({ fullName: person.fullName, email: person.email, phone: person.phone ?? '', role: person.role, isActive: person.isActive })
    setNewPassword('')
    setError(''); setNotice('')
  }

  async function saveUser(event: FormEvent) {
    event.preventDefault()
    if (!editing || !editDraft) return
    setSaving(true); setError(''); setNotice('')
    try {
      await api.put(`/api/admin/users/${editing.userId}`, { ...editDraft, password: newPassword || null })
      setEditing(null); setEditDraft(null); setNewPassword('')
      setNotice(newPassword ? 'User details and password updated.' : 'User details updated.')
      await load()
    } catch (requestError) {
      await load()
      setError(errorMessage(requestError, 'User changes could not be saved.'))
    } finally { setSaving(false) }
  }

  async function deleteOrRestore(person: UserRow) {
    setError(''); setNotice('')
    let confirmation: string | undefined
    if (!person.isDeleted) {
      if (person.role === 'Admin') {
        confirmation = window.prompt(`This is an Admin account. Enter the account email to continue:\n\n${person.email}`) ?? undefined
        if (confirmation === undefined) return
      } else if (!window.confirm(`Soft-delete ${person.fullName}? They can be restored later.`)) return
    }
    setSaving(true)
    try {
      await api.patch(`/api/admin/users/${person.userId}/deleted`, { isDeleted: !person.isDeleted, confirmation })
      setNotice(person.isDeleted ? `${person.fullName} was restored.` : `${person.fullName} was soft-deleted.`)
      await load()
    } catch (requestError) {
      setError(errorMessage(requestError, 'The account status could not be changed.'))
    } finally { setSaving(false) }
  }

  return <main className="admin-shell">
    <PageHeading className="admin-page-heading" eyebrow="Z SQUARE / ADMINISTRATION" title="User management" description="Create accounts, change access, reset passwords, and restore soft-deleted users." actions={<button className="admin-primary" type="button" onClick={() => { setCreating(true); setError(''); setNotice('') }}>+ Create user</button>} />
    {error && <Feedback>{error}</Feedback>}{notice && <Feedback kind="notice">{notice}</Feedback>}

    {creating && <Modal title="Create a user" eyebrow="NEW ACCOUNT" closeDisabled={saving} onClose={() => { setCreating(false); setCreateDraft(blankUser); setError('') }}>
      {error && <div className="admin-alert" role="alert">{error}</div>}
      <form className="admin-form-grid" onSubmit={event => void createUser(event)}>
        <label>Full name<input required maxLength={200} value={createDraft.fullName} onChange={event => setCreateDraft({ ...createDraft, fullName: event.target.value })} /></label>
        <label>Email address<input required type="email" maxLength={256} value={createDraft.email} onChange={event => setCreateDraft({ ...createDraft, email: event.target.value })} /></label>
        <label>Phone <span className="admin-optional">Optional</span><input maxLength={30} value={createDraft.phone} onChange={event => setCreateDraft({ ...createDraft, phone: event.target.value })} /></label>
        <label>Role<select value={createDraft.role} onChange={event => setCreateDraft({ ...createDraft, role: event.target.value as UserRow['role'] })}><option>Customer</option><option>Manager</option><option>Admin</option></select></label>
        <label className="admin-password-field">Set password<input required type="password" minLength={8} autoComplete="new-password" value={createDraft.password} onChange={event => setCreateDraft({ ...createDraft, password: event.target.value })} /><small>At least 8 characters, with uppercase, lowercase, and a number.</small></label>
        <div className="admin-form-actions"><button className="admin-primary" disabled={saving}>{saving ? 'Creating...' : 'Create account'}</button><button className="admin-secondary" type="button" disabled={saving} onClick={() => { setCreating(false); setCreateDraft(blankUser); setError('') }}>Cancel</button></div>
      </form>
    </Modal>}

    <section className="admin-panel">
      <PanelHeading eyebrow="ACCOUNT DIRECTORY" title={`${data?.totalCount ?? 0} users`} actions={<button className="admin-secondary" type="button" disabled={loading} onClick={() => void load()}>Refresh</button>} />
      <div className="admin-filters"><form onSubmit={event => { event.preventDefault(); setPage(1); setSearch(searchDraft.trim()) }}><input aria-label="Search users" placeholder="Search name or email" value={searchDraft} onChange={event => setSearchDraft(event.target.value)} /><button className="admin-secondary">Search</button></form>
        <label>Role<select value={role} onChange={event => { setPage(1); setRole(event.target.value) }}><option value="">All roles</option><option>Customer</option><option>Manager</option><option>Admin</option></select></label>
        <label>Account state<select value={deleted} onChange={event => { setPage(1); setDeleted(event.target.value) }}><option value="">All accounts</option><option value="false">Active records</option><option value="true">Deleted records</option></select></label>
      </div>
      {error && !data && <div className="admin-empty" role="alert">{error}</div>}
      <div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>User</th><th>Role</th><th>Status</th><th>Created</th><th>Actions</th></tr></thead><tbody>
        {loading && !data ? <tr><td colSpan={5} className="admin-empty">Loading users…</td></tr> : data?.items.length ? data.items.map(person => <tr key={person.userId} className={person.isDeleted ? 'admin-user-deleted' : ''}><td><strong>{person.fullName}</strong><small>{person.email}{person.phone ? ` · ${person.phone}` : ''}</small></td><td><span className={`admin-role admin-role-${person.role.toLowerCase()}`}>{person.role}</span></td><td><span className={person.isDeleted ? 'admin-state admin-state-deleted' : person.isActive ? 'admin-state admin-state-active' : 'admin-state'}>{person.isDeleted ? 'Deleted' : person.isActive ? 'Active' : 'Disabled'}</span></td><td>{new Date(person.createdAt).toLocaleDateString()}</td><td className="admin-row-actions"><button className="admin-secondary" type="button" disabled={person.isDeleted} onClick={() => openEditor(person)}>Edit</button><button className={person.isDeleted ? 'admin-primary' : 'admin-danger'} type="button" disabled={saving || (!person.isDeleted && person.userId === currentUser?.userId)} onClick={() => void deleteOrRestore(person)}>{person.isDeleted ? 'Restore' : 'Delete'}</button></td></tr>) : !error && <tr><td colSpan={5} className="admin-empty">No users match these filters.</td></tr>}
      </tbody></table></div>
      {data && <Pagination page={page} totalPages={data.totalPages} loading={loading} onPageChange={setPage} summary={<span>{data.totalCount} accounts</span>} />}
    </section>

    {editing && editDraft && <Modal title="Edit user" eyebrow={`ACCOUNT #${editing.userId}`} closeDisabled={saving} onClose={() => setEditing(null)}>
      {error && <div className="admin-alert" role="alert">{error}</div>}
      <form className="admin-form-grid" onSubmit={event => void saveUser(event)}>
        <label>Full name<input required maxLength={200} value={editDraft.fullName} onChange={event => setEditDraft({ ...editDraft, fullName: event.target.value })} /></label>
        <label>Email address<input required type="email" maxLength={256} value={editDraft.email} onChange={event => setEditDraft({ ...editDraft, email: event.target.value })} /></label>
        <label>Phone<input maxLength={30} value={editDraft.phone} onChange={event => setEditDraft({ ...editDraft, phone: event.target.value })} /></label>
        <label>Role<select disabled={editing.userId === currentUser?.userId} value={editDraft.role} onChange={event => setEditDraft({ ...editDraft, role: event.target.value as UserRow['role'] })}><option>Customer</option><option>Manager</option><option>Admin</option></select></label>
        <label>Account status<select disabled={editing.userId === currentUser?.userId} value={String(editDraft.isActive)} onChange={event => setEditDraft({ ...editDraft, isActive: event.target.value === 'true' })}><option value="true">Active</option><option value="false">Disabled</option></select></label>
        <label className="admin-password-field">Set a new password <span className="admin-optional">Leave blank to keep current</span><input type="password" minLength={8} autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} /><small>At least 8 characters, with uppercase, lowercase, and a number.</small></label>
        <div className="admin-form-actions"><button className="admin-primary" disabled={saving}>{saving ? 'Saving…' : 'Save changes'}</button><button className="admin-secondary" type="button" onClick={() => setEditing(null)}>Cancel</button></div>
      </form>
    </Modal>}
  </main>
}
