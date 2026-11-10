import axios from 'axios'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { assetUrl } from './productApi'
import { cancelOrder, getOrder, money, type OrderDetails } from './shoppingApi'

export default function OrderDetailsPage() {
  const { id = '' } = useParams()
  const [order, setOrder] = useState<OrderDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function load() {
    setOrder(await getOrder(id))
  }

  useEffect(() => {
    let active = true
    getOrder(id).then(result => { if (active) setOrder(result) })
      .catch(() => { if (active) setError('This order was not found.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [id])

  async function cancel() {
    if (!order) return
    setBusy(true)
    setError('')
    try {
      await cancelOrder(order.orderId)
      await load()
    } catch (problem) {
      setError(axios.isAxiosError(problem) ? problem.response?.data?.title ?? 'The order could not be cancelled.' : 'The order could not be cancelled.')
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <main className="commerce-page"><p>Loading order…</p></main>
  if (!order) return <main className="commerce-page"><section className="commerce-empty"><h1>Order not found</h1><Link to="/orders">Back to my orders</Link></section></main>

  return <main className="commerce-page">
    <div className="commerce-heading"><p className="eyebrow">ORDER #{order.orderId}</p><h1>Order details</h1><span className={`order-status status-${order.status.toLowerCase()}`}>{order.status}</span></div>
    {error && <p className="shopping-error" role="alert">{error}</p>}
    <div className="commerce-layout order-detail-layout"><section className="order-detail-items">{order.items.map((item, index) => <article className="order-detail-line" key={`${item.variantId}-${index}`}>
      <div className="order-detail-image">{item.imageUrl ? <img src={assetUrl(item.imageUrl) ?? ''} alt={item.productName} /> : <span>K</span>}</div>
      <div><p className="eyebrow">{item.color} · {item.size}</p><h2>{item.productName}</h2><p>{item.quantity} × {money(item.unitPrice)}</p></div><strong>{money(item.lineTotal)}</strong>
    </article>)}<div className="order-total-row"><span>Total</span><strong>{money(order.totalAmount)}</strong></div></section>
      <aside className="order-detail-aside"><section><h2>Shipping address</h2><p>{order.shipName}<br />{order.shipLine1}{order.shipLine2 && <><br />{order.shipLine2}</>}<br />{order.shipCity}, {order.shipPostalCode}<br />{order.shipCountry}</p></section>
        <section><h2>Payment</h2>{order.payments.map((payment, index) => <p key={index}>{payment.method}<br /><span>{payment.status}</span></p>)}</section>
        <section><h2>Placed</h2><p>{new Date(order.orderDate).toLocaleString()}</p></section>
        {order.status === 'Pending' && <button type="button" className="danger-button" disabled={busy} onClick={() => void cancel()}>{busy ? 'Cancelling…' : 'Cancel order'}</button>}
        <Link to="/orders" className="checkout-back-link">Back to my orders</Link>
      </aside>
    </div>
  </main>
}
