import axios from 'axios'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { assetUrl } from './productApi'
import { addAddress, checkout, getAddresses, getCart, money, notifyCartUpdated, type Address, type Cart } from './shoppingApi'

const emptyAddress = { line1: '', line2: '', city: '', postalCode: '', country: 'Pakistan', isDefault: false }

export default function CheckoutPage() {
  const navigate = useNavigate()
  const [cart, setCart] = useState<Cart | null>(null)
  const [addresses, setAddresses] = useState<Address[]>([])
  const [addressId, setAddressId] = useState<number | ''>('')
  const [paymentMethod, setPaymentMethod] = useState<'CashOnDelivery' | 'MockCard'>('CashOnDelivery')
  const [addingAddress, setAddingAddress] = useState(false)
  const [addressForm, setAddressForm] = useState(emptyAddress)
  const [submitting, setSubmitting] = useState(false)
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<string[]>([])
  const [pageError, setPageError] = useState('')

  async function refresh() {
    const [nextCart, nextAddresses] = await Promise.all([getCart(), getAddresses()])
    setCart(nextCart)
    setAddresses(nextAddresses)
    setAddressId(current => current || nextAddresses.find(address => address.isDefault)?.addressId || nextAddresses[0]?.addressId || '')
  }

  useEffect(() => {
    void refresh().catch(() => setPageError('Checkout information could not be loaded. Please try again.')).finally(() => setLoading(false))
  }, [])

  async function saveAddress(event: FormEvent) {
    event.preventDefault()
    setPageError('')
    try {
      const created = await addAddress({ ...addressForm, line2: addressForm.line2 || null })
      const nextAddresses = await getAddresses()
      setAddresses(nextAddresses)
      setAddressId(created.addressId)
      setAddingAddress(false)
      setAddressForm(emptyAddress)
    } catch (error) {
      setPageError(axios.isAxiosError(error) ? problemText(error.response?.data) : 'Address could not be saved.')
    }
  }

  async function placeOrder() {
    if (!cart || addressId === '') return
    setSubmitting(true)
    setErrors([])
    setPageError('')
    try {
      const order = await checkout(addressId, paymentMethod, crypto.randomUUID())
      notifyCartUpdated()
      navigate(`/orders/${order.orderId}/confirmation`, { replace: true })
    } catch (error) {
      if (axios.isAxiosError(error)) {
        if (error.response?.status === 409) {
          setErrors(problemErrors(error.response.data))
          await getCart().then(setCart).catch(() => undefined)
        } else {
          setPageError(problemText(error.response?.data))
        }
      } else {
        setPageError('Your order could not be placed. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <main className="commerce-page"><p>Loading checkout…</p></main>
  if (!cart) return <main className="commerce-page"><p className="shopping-error">{pageError || 'Checkout could not be loaded.'}</p><Link to="/cart">Back to bag</Link></main>

  return <main className="commerce-page">
    <div className="commerce-heading"><p className="eyebrow">ALMOST YOURS</p><h1>Checkout</h1></div>
    {pageError && <p className="shopping-error" role="alert">{pageError}</p>}
    {errors.length > 0 && <div className="shopping-error" role="alert"><strong>Some items changed before checkout:</strong><ul>{errors.map(error => <li key={error}>{error}</li>)}</ul></div>}
    <div className="commerce-layout checkout-layout">
      <section className="checkout-main">
        <section className="checkout-section"><div className="checkout-section-heading"><h2>Shipping address</h2><button type="button" className="link-button" onClick={() => setAddingAddress(value => !value)}>{addingAddress ? 'Choose saved address' : 'Add address'}</button></div>
          {addingAddress ? <form className="checkout-address-form" onSubmit={event => void saveAddress(event)}>
            <label>Address line 1<input required maxLength={200} value={addressForm.line1} onChange={event => setAddressForm({ ...addressForm, line1: event.target.value })} /></label>
            <label>Address line 2 <span>(optional)</span><input maxLength={200} value={addressForm.line2} onChange={event => setAddressForm({ ...addressForm, line2: event.target.value })} /></label>
            <div className="checkout-address-row"><label>City<input required maxLength={100} value={addressForm.city} onChange={event => setAddressForm({ ...addressForm, city: event.target.value })} /></label><label>Postal code<input required maxLength={20} value={addressForm.postalCode} onChange={event => setAddressForm({ ...addressForm, postalCode: event.target.value })} /></label></div>
            <label>Country<input required maxLength={100} value={addressForm.country} onChange={event => setAddressForm({ ...addressForm, country: event.target.value })} /></label>
            <button className="browse-cta" type="submit">Save address</button>
          </form> : addresses.length === 0 ? <p className="commerce-muted">Add a shipping address to continue.</p> : <div className="checkout-addresses">{addresses.map(address => <label className={addressId === address.addressId ? 'checkout-address selected' : 'checkout-address'} key={address.addressId}><input type="radio" name="address" checked={addressId === address.addressId} onChange={() => setAddressId(address.addressId)} /><span><strong>{address.line1}{address.line2 ? `, ${address.line2}` : ''}</strong><small>{address.city}, {address.postalCode}, {address.country}</small>{address.isDefault && <em>Default</em>}</span></label>)}</div>}
        </section>
        <section className="checkout-section"><h2>Payment method</h2><div className="checkout-payment-options"><label><input type="radio" checked={paymentMethod === 'CashOnDelivery'} onChange={() => setPaymentMethod('CashOnDelivery')} /><span><strong>Cash on delivery</strong><small>Pay when your order arrives</small></span></label><label><input type="radio" checked={paymentMethod === 'MockCard'} onChange={() => setPaymentMethod('MockCard')} /><span><strong>Mock card</strong><small>Simulated payment for this project</small></span></label></div></section>
      </section>
      <aside className="commerce-summary"><h2>Order summary</h2>{cart.items.map(item => <div className="checkout-summary-item" key={item.cartItemId}>{item.imageUrl && <img src={assetUrl(item.imageUrl) ?? ''} alt="" />}<span>{item.productName}<small>{item.color} · {item.size} × {item.quantity}</small></span><strong>{money(item.lineTotal)}</strong></div>)}<p><span>Subtotal</span><strong>{money(cart.subtotal)}</strong></p>{cart.items.some(item => !item.inStock) && <p className="shopping-warning">Update unavailable items in your bag before placing the order.</p>}<button className="browse-cta checkout-submit" type="button" disabled={submitting || !addressId || cart.items.length === 0 || cart.items.some(item => !item.inStock)} onClick={() => void placeOrder()}>{submitting ? 'Placing order…' : 'Place order'} <span>→</span></button><Link className="checkout-back-link" to="/cart">Return to shopping bag</Link></aside>
    </div>
  </main>
}

function problemErrors(value: unknown): string[] {
  if (!value || typeof value !== 'object') return ['Stock changed. Review your bag and try again.']
  const data = value as { errors?: Record<string, string[]>; title?: string; detail?: string }
  const entries = Object.values(data.errors ?? {}).flat()
  return entries.length ? entries : [data.detail ?? data.title ?? 'Stock changed. Review your bag and try again.']
}

function problemText(value: unknown): string {
  if (!value || typeof value !== 'object') return 'The request could not be completed.'
  const data = value as { detail?: string; title?: string }
  return data.detail ?? data.title ?? 'The request could not be completed.'
}
