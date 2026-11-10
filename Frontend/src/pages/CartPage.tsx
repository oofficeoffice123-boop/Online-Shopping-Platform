import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { assetUrl } from './productApi'
import { getCart, money, notifyCartUpdated, removeCartItem, setCartQuantity, type Cart } from './shoppingApi'

export default function CartPage() {
  const [cart, setCart] = useState<Cart | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busyItem, setBusyItem] = useState<number | null>(null)

  async function refresh() {
    setCart(await getCart())
  }

  useEffect(() => {
    void refresh().catch(() => setError('Your bag could not be loaded. Please try again.')).finally(() => setLoading(false))
  }, [])

  async function changeQuantity(cartItemId: number, quantity: number) {
    setBusyItem(cartItemId)
    setError('')
    try {
      setCart(await setCartQuantity(cartItemId, quantity))
      notifyCartUpdated()
    } catch {
      setError('Stock changed. Your bag has been refreshed; check the item warning before checkout.')
      await refresh().catch(() => undefined)
    } finally {
      setBusyItem(null)
    }
  }

  async function remove(cartItemId: number) {
    setBusyItem(cartItemId)
    try {
      await removeCartItem(cartItemId)
      await refresh()
      notifyCartUpdated()
    } catch {
      setError('That item could not be removed. Please refresh the page.')
    } finally {
      setBusyItem(null)
    }
  }

  if (loading) return <main className="commerce-page"><p>Loading your bag…</p></main>
  if (!cart) return <main className="commerce-page"><p className="shopping-error">{error || 'Your bag could not be loaded.'}</p></main>

  return <main className="commerce-page">
    <div className="commerce-heading"><p className="eyebrow">YOUR SELECTION</p><h1>Shopping bag</h1></div>
    {error && <p className="shopping-error" role="alert">{error}</p>}
    {cart.items.length === 0 ? <section className="commerce-empty"><h2>Your bag is empty</h2><p>Find something you love in the collection.</p><Link className="browse-cta" to="/products">Continue shopping</Link></section> : <div className="commerce-layout">
      <section className="cart-lines">
        {cart.items.map(item => <article className="cart-line" key={item.cartItemId}>
          <Link to={`/products/${item.productId}`} className="cart-line-photo">{item.imageUrl ? <img src={assetUrl(item.imageUrl) ?? ''} alt={item.productName} /> : <span>K</span>}</Link>
          <div className="cart-line-info"><p className="eyebrow">{item.color} · {item.size}</p><h2>{item.productName}</h2><p>{money(item.unitPrice)}</p>
            {!item.variantActive && <p className="shopping-warning">This product is no longer available. Remove it before checkout.</p>}
            {item.variantActive && !item.stockSufficient && <p className="shopping-warning">Stock is now lower than your quantity. Reduce the quantity to continue.</p>}
            <div className="cart-line-controls"><div className="quantity-control"><button type="button" aria-label="Decrease quantity" disabled={busyItem === item.cartItemId || item.quantity <= 1} onClick={() => void changeQuantity(item.cartItemId, item.quantity - 1)}>−</button><span>{item.quantity}</span><button type="button" aria-label="Increase quantity" disabled={busyItem === item.cartItemId || item.quantity >= 10} onClick={() => void changeQuantity(item.cartItemId, item.quantity + 1)}>+</button></div><button type="button" className="link-button" disabled={busyItem === item.cartItemId} onClick={() => void remove(item.cartItemId)}>Remove</button></div>
          </div>
          <strong className="cart-line-total">{money(item.lineTotal)}</strong>
        </article>)}
      </section>
      <aside className="commerce-summary"><h2>Summary</h2><p><span>Subtotal</span><strong>{money(cart.subtotal)}</strong></p><small>Shipping is confirmed at checkout.</small>
        <Link className="browse-cta" to="/checkout">Continue to checkout <span>→</span></Link>
      </aside>
    </div>}
  </main>
}
