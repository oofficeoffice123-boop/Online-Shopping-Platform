import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { api } from '../auth/apiClient'
import { getReviews, type ReviewEligibility, type ReviewPage } from './productApi'

export default function ProductReviews({ productId }: { productId: number }) {
  const { user } = useAuth()
  const location = useLocation()
  const [reviews, setReviews] = useState<ReviewPage | null>(null)
  const [eligibility, setEligibility] = useState<ReviewEligibility | null>(null)
  const [page, setPage] = useState(1)
  const [rating, setRating] = useState(5)
  const [comment, setComment] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function reload() {
    const result = await getReviews(productId, page); setReviews(result)
    if (user?.role === 'Customer') {
      const { data } = await api.get<ReviewEligibility>(`/api/products/${productId}/reviews/eligibility`)
      setEligibility(data)
      if (data.existingReview) { setRating(data.existingReview.rating); setComment(data.existingReview.comment ?? '') }
      else { setRating(5); setComment('') }
    } else setEligibility(null)
  }
  useEffect(() => { let mounted = true; void (async()=>{try{await reload()}catch{if(mounted)setError('Reviews could not be loaded.')}})();return()=>{mounted=false} }, [productId, page, user?.userId, user?.role])

  async function save(event: React.FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      if (comment.trim().length > 1000) throw new Error('Comments can be at most 1000 characters.')
      if (comment.length > 0 && comment.trim().length === 0) throw new Error('Comment cannot contain only whitespace.')
      const body = { rating, comment: comment.trim() || null }
      if (eligibility?.existingReview) await api.put(`/api/reviews/${eligibility.existingReview.reviewId}`, body)
      else await api.post(`/api/products/${productId}/reviews`, body)
      await reload()
    } catch (reason) {
      const data = (reason as { response?: { data?: { detail?: string; title?: string } } }).response?.data
      setError(data?.detail ?? data?.title ?? (reason instanceof Error ? reason.message : 'Review could not be saved.'))
    } finally { setBusy(false) }
  }

  return <section className="product-reviews" aria-labelledby="reviews-heading">
    <h2 id="reviews-heading">Customer reviews</h2>
    <div className="review-summary"><strong>{reviews?.summary.averageRating == null ? 'No ratings yet' : `★ ${reviews.summary.averageRating.toFixed(1)} out of 5`}</strong><span>{reviews?.summary.totalCount ?? 0} reviews</span>
      {[5,4,3,2,1].map(star=>{const count=reviews?.summary.countsByStar[String(star)]??0;const total=reviews?.summary.totalCount??0;return <div className="review-star-bar" key={star}><span>{star} ★</span><progress max={total||1} value={count}/><span>{count}</span></div>})}
    </div>
    {error && <p className="shopping-error" role="alert">{error}</p>}
    {reviews?.items.map(review=><article className="customer-review" key={review.reviewId}><header><strong>{review.reviewerName}</strong><span>{'★'.repeat(review.rating)}{'☆'.repeat(5-review.rating)}</span><time>{new Date(review.createdAt).toLocaleDateString()}</time></header>{review.comment&&<p>{review.comment}</p>}</article>)}
    {reviews && reviews.totalPages>1&&<nav className="review-pagination" aria-label="Review pages"><button disabled={page<=1} onClick={()=>setPage(page-1)}>Previous</button><span>Page {page} of {reviews.totalPages}</span><button disabled={page>=reviews.totalPages} onClick={()=>setPage(page+1)}>Next</button></nav>}

    <div className="review-editor"><h3>{eligibility?.existingReview?'Edit your review':'Write a review'}</h3>
      {!user&&<p><Link to="/login" state={{returnTo:`${location.pathname}${location.search}`}}>Log in to review</Link></p>}
      {user&&user.role!=='Customer'&&<p>Customer accounts can submit reviews.</p>}
      {user?.role==='Customer'&&!eligibility&&<p>Checking purchase eligibility…</p>}
      {user?.role==='Customer'&&eligibility&&!eligibility.existingReview&&!eligibility.hasDeliveredPurchase&&<p>Purchase and receive this item to review it.</p>}
      {user?.role==='Customer'&&eligibility&&(eligibility.canReview||eligibility.existingReview)&&<form onSubmit={save}>
        <fieldset><legend>Your rating</legend><div className="review-picker">{[1,2,3,4,5].map(star=><button key={star} type="button" aria-label={`${star} stars`} aria-pressed={rating===star} onClick={()=>setRating(star)}>{star<=rating?'★':'☆'}</button>)}</div></fieldset>
        <label>Comment<textarea maxLength={1000} value={comment} onChange={event=>setComment(event.target.value)} placeholder="Share your experience" /></label><small>{comment.length}/1000</small>
        <button disabled={busy}>{busy?'Saving…':eligibility.existingReview?'Save changes':'Submit review'}</button>
        {eligibility.existingReview&&<button type="button" disabled={busy} onClick={async()=>{setError('');try{await api.delete(`/api/reviews/${eligibility.existingReview!.reviewId}`);await reload()}catch{setError('Review could not be deleted.')}}}>Delete my review</button>}
      </form>}
    </div>
  </section>
}
