import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { api } from '../auth/apiClient'
import './AdminWorkspace.css'

type Review = { reviewId: number; productId: number; productName: string; reviewerName: string; rating: number; comment: string | null; createdAt: string; isHidden: boolean; moderatedAt: string | null; moderatedBy: string | null }
type ReviewPage = { items: Review[]; page: number; totalPages: number; totalCount: number }

export default function ManageReviewsPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const [data, setData] = useState<ReviewPage | null>(null)
  const [page, setPage] = useState(1)
  const [productId, setProductId] = useState('')
  const [rating, setRating] = useState('')
  const [hidden, setHidden] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<number | null>(null)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const response = await api.get<ReviewPage>('/api/manage/reviews', { params: { page, pageSize: 20, productId: productId || undefined, rating: rating || undefined, hidden: hidden === '' ? undefined : hidden === 'true' } })
      setData(response.data)
    } catch { setError('Reviews could not be loaded. Refresh or sign in again.') }
    finally { setLoading(false) }
  }, [page, productId, rating, hidden])

  useEffect(() => { void load() }, [load])

  async function moderate(review: Review) {
    setSaving(review.reviewId); setError('')
    try { await api.patch(`/api/manage/reviews/${review.reviewId}/hidden`, { isHidden: !review.isHidden }); await load() }
    catch { setError('The moderation change could not be saved.') }
    finally { setSaving(null) }
  }

  async function remove(review: Review) {
    if (!window.confirm(`Permanently delete this review for ${review.productName}?`)) return
    setSaving(review.reviewId); setError('')
    try { await api.delete(`/api/manage/reviews/${review.reviewId}`); await load() }
    catch { setError('The review could not be deleted.') }
    finally { setSaving(null) }
  }

  return <main className="admin-reviews-shell">
    <header className="admin-review-header"><div><p className="admin-kicker">Z SQUARE / {isAdmin ? 'ADMINISTRATION' : 'OPERATIONS'}</p><h1>Review moderation</h1><p>Review customer feedback, hide inappropriate comments, and manage removals.</p></div><span className="review-count-chip">{data?.totalCount ?? 0} reviews</span></header>
    {error && <div className="admin-alert" role="alert">{error}</div>}
    <section className="admin-panel">
      <div className="admin-review-filters"><label>Product ID<input type="number" min="1" placeholder="Any product" value={productId} onChange={event => { setProductId(event.target.value); setPage(1) }} /></label><label>Rating<select value={rating} onChange={event => { setRating(event.target.value); setPage(1) }}><option value="">All ratings</option>{[5,4,3,2,1].map(value => <option key={value} value={value}>{value} stars</option>)}</select></label><label>Moderation state<select value={hidden} onChange={event => { setHidden(event.target.value); setPage(1) }}><option value="">All comments</option><option value="false">Not hidden</option><option value="true">Hidden</option></select></label><button className="admin-secondary" type="button" disabled={loading} onClick={() => void load()}>Refresh</button></div>
      <div className="admin-review-list">{loading && !data ? <div className="admin-review-empty">Loading reviews…</div> : data?.items.length ? data.items.map(review => <article className="admin-review-card" key={review.reviewId}>
        <div className="admin-review-main"><h2 className="admin-review-product">{review.productName}</h2><div className="admin-review-meta"><span className="admin-review-rating">{'★'.repeat(review.rating)}{'☆'.repeat(5-review.rating)}</span><span>{review.reviewerName}</span><time dateTime={review.createdAt}>{new Date(review.createdAt).toLocaleDateString()}</time><span>Product #{review.productId}</span></div>{review.comment && <p className="admin-review-comment">{review.comment}</p>}{review.isHidden && <span className="admin-review-hidden">Hidden{review.moderatedBy ? ` · ${review.moderatedBy}` : ''}</span>}</div>
        <div className="admin-review-actions"><button className="admin-secondary" type="button" disabled={saving === review.reviewId} onClick={() => void moderate(review)}>{review.isHidden ? 'Unhide' : 'Hide'}</button>{isAdmin && <button className="admin-danger" type="button" disabled={saving === review.reviewId} onClick={() => void remove(review)}>Delete</button>}</div>
      </article>) : !error && <div className="admin-review-empty">No reviews match these filters.</div>}</div>
      {data && data.totalPages > 1 && <footer className="admin-review-pagination"><button className="admin-secondary" disabled={page <= 1 || loading} onClick={() => setPage(value => value - 1)}>Previous</button><span>Page {page} of {data.totalPages}</span><button className="admin-secondary" disabled={page >= data.totalPages || loading} onClick={() => setPage(value => value + 1)}>Next</button></footer>}
    </section>
  </main>
}
