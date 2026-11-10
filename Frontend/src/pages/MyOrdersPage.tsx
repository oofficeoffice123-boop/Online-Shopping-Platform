import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { assetUrl } from './productApi'
import { getOrders, money, type OrderPage } from './shoppingApi'

export default function MyOrdersPage() {
  const [page, setPage] = useState<OrderPage | null>(null)
  const [pageNumber, setPageNumber] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true)
    getOrders(pageNumber).then(result => { if (active) setPage(result) })
      .catch(() => { if (active) setError('Your orders could not be loaded.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [pageNumber])

  return <main className="commerce-page">
    <div className="commerce-heading"><p className="eyebrow">YOUR SHOPPING HISTORY</p><h1>My orders</h1></div>
    {loading ? <p>Loading orders…</p> : error ? <p className="shopping-error">{error}</p> : !page?.items.length ? <section className="commerce-empty"><h2>No orders yet</h2><p>Your orders will appear here after checkout.</p><Link className="browse-cta" to="/products">Explore the collection</Link></section> : <>
      <section className="order-list">{page.items.map(order => <Link className="order-list-card" to={`/orders/${order.orderId}`} key={order.orderId}>
        <div className="order-list-image">{order.firstItemImageUrl ? <img src={assetUrl(order.firstItemImageUrl) ?? ''} alt="" /> : <span>K</span>}</div>
        <div className="order-list-main"><p className="eyebrow">ORDER #{order.orderId}</p><h2>{new Date(order.orderDate).toLocaleDateString()}</h2><p>{order.itemCount} {order.itemCount === 1 ? 'item' : 'items'}</p></div>
        <div className="order-list-meta"><span className={`order-status status-${order.status.toLowerCase()}`}>{order.status}</span><strong>{money(order.totalAmount)}</strong></div>
      </Link>)}</section>
      {page.totalPages > 1 && <nav className="browse-pagination" aria-label="Order pages"><button type="button" disabled={pageNumber <= 1} onClick={() => setPageNumber(value => value - 1)}>Previous</button><span>Page {pageNumber} of {page.totalPages}</span><button type="button" disabled={pageNumber >= page.totalPages} onClick={() => setPageNumber(value => value + 1)}>Next</button></nav>}
    </>}
  </main>
}
