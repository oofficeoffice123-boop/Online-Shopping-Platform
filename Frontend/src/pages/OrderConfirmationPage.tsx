import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { assetUrl } from './productApi'
import { getOrder, money, type OrderDetails } from './shoppingApi'

export default function OrderConfirmationPage() {
  const { id = '' } = useParams()
  const [order, setOrder] = useState<OrderDetails | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let active = true
    getOrder(id).then(result => { if (active) setOrder(result) }).catch(() => { if (active) setFailed(true) })
    return () => { active = false }
  }, [id])

  if (failed) return <main className="commerce-page"><section className="commerce-empty"><h1>Order placed</h1><p>We could not load the order details just now.</p><Link to="/orders">View my orders</Link></section></main>
  if (!order) return <main className="commerce-page"><p>Preparing your confirmation…</p></main>

  return <main className="commerce-page confirmation-page">
    <div className="confirmation-mark">✓</div><p className="eyebrow">THANK YOU</p><h1>Your order is confirmed</h1>
    <p className="commerce-muted">Order #{order.orderId} · {new Date(order.orderDate).toLocaleDateString()}</p>
    <div className="confirmation-summary">{order.items.map((item, index) => <div className="checkout-summary-item" key={`${item.variantId}-${index}`}>{item.imageUrl && <img src={assetUrl(item.imageUrl) ?? ''} alt="" />}<span>{item.productName}<small>{item.color} · {item.size} × {item.quantity}</small></span><strong>{money(item.lineTotal)}</strong></div>)}<p><span>Total</span><strong>{money(order.totalAmount)}</strong></p></div>
    <div className="confirmation-actions"><Link className="browse-cta" to={`/orders/${order.orderId}`}>View order details</Link><Link to="/products">Continue shopping</Link></div>
  </main>
}
