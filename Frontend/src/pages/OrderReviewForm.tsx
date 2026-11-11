import { useEffect, useState, type FormEvent } from 'react'
import { api } from '../auth/apiClient'
import type { ReviewEligibility } from './productApi'

export default function OrderReviewForm({ productId, productName }: { productId: number; productName: string }) {
  const [eligibility, setEligibility] = useState<ReviewEligibility | null>(null)
  const [rating, setRating] = useState(5)
  const [comment, setComment] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  async function loadEligibility() {
    const { data } = await api.get<ReviewEligibility>(`/api/products/${productId}/reviews/eligibility`)
    setEligibility(data)
    if (data.existingReview) {
      setRating(data.existingReview.rating)
      setComment(data.existingReview.comment ?? '')
    }
  }

  useEffect(() => {
    let active = true
    api.get<ReviewEligibility>(`/api/products/${productId}/reviews/eligibility`)
      .then(({ data }) => { if (active) { setEligibility(data); if (data.existingReview) { setRating(data.existingReview.rating); setComment(data.existingReview.comment ?? '') } } })
      .catch(() => { if (active) setError('Review status could not be loaded.') })
    return () => { active = false }
  }, [productId])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    try {
      if (comment.trim().length > 1000) throw new Error('Comments can be at most 1,000 characters.')
      if (comment.length > 0 && comment.trim().length === 0) throw new Error('Comment cannot contain only whitespace.')
      const body = { rating, comment: comment.trim() || null }
      if (eligibility?.existingReview) await api.put(`/api/reviews/${eligibility.existingReview.reviewId}`, body)
      else await api.post(`/api/products/${productId}/reviews`, body)
      await loadEligibility()
      setNotice('Your review has been saved.')
    } catch (reason) {
      const data = (reason as { response?: { data?: { detail?: string; title?: string } } }).response?.data
      setError(data?.detail ?? data?.title ?? (reason instanceof Error ? reason.message : 'Review could not be saved.'))
    } finally {
      setBusy(false)
    }
  }

  if (eligibility && !eligibility.canReview && !eligibility.existingReview) return null

  return <section className="order-review-form">
    <h3>{eligibility?.existingReview ? `Your review for ${productName}` : `Review ${productName}`}</h3>
    {error && <p className="shopping-error" role="alert">{error}</p>}
    {notice && <p className="order-review-notice" role="status">{notice}</p>}
    {!eligibility && !error && <p>Checking review status…</p>}
    {eligibility && <form onSubmit={submit}>
      <fieldset><legend>Your rating</legend><div className="review-picker">{[1, 2, 3, 4, 5].map(star => <button key={star} type="button" aria-label={`${star} stars`} aria-pressed={rating === star} onClick={() => setRating(star)}>{star <= rating ? '★' : '☆'}</button>)}</div></fieldset>
      <label>Comment<textarea maxLength={1000} value={comment} onChange={event => setComment(event.target.value)} placeholder="Share your experience" /></label>
      <small>{comment.length}/1000</small>
      <button type="submit" disabled={busy}>{busy ? 'Saving…' : eligibility.existingReview ? 'Update review' : 'Submit review'}</button>
    </form>}
  </section>
}
