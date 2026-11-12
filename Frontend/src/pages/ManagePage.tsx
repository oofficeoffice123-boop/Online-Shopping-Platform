import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { api } from '../auth/apiClient'
import { assetUrl } from './productApi'
import './ManagePage.css'

type Category = { categoryId: number; name: string }
export default function ManagePage() {
  const [categories, setCategories] = useState<Category[]>([])

  useEffect(() => {
    void api.get<Category[]>('/api/manage/categories')
      .then(({ data }) => setCategories(data))
      .catch(() => setCategories([]))
  }, [])

  return <main className="manage-page">
    <section className="manager-hero">
      <div><p className="eyebrow">Z SQUARE  |  OPERATIONS</p><h1>Inventory control</h1></div>
      <span className="manager-live"><i /> LIVE INVENTORY</span>
    </section>
    <InventoryWorkspace categories={categories} />
    <OrderWorkspace />
  </main>
}
type InventoryRow = {
  variantId: number
  productId: number
  productCode: string
  productName: string
  imageUrl: string | null
  category: string
  sku: string
  size: string
  color: string
  stockQty: number
  price: number
  productActive: boolean
  variantActive: boolean
  rowVersion: string
}

type InventoryResponse = {
  items: InventoryRow[]
  page: number
  pageSize: number
  totalCount: number
  summary: { variantCount: number; totalUnits: number; lowStockCount: number; outOfStockCount: number }
}

function InventoryWorkspace({ categories }: { categories: any[] }) {
  const [rows, setRows] = useState<InventoryRow[]>([])
  const [lowRows, setLowRows] = useState<InventoryRow[]>([])
  const [summary, setSummary] = useState<InventoryResponse['summary'] | null>(null)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [stockFilter, setStockFilter] = useState('all')
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draftStock, setDraftStock] = useState('')
  const [editingPriceId, setEditingPriceId] = useState<number | null>(null)
  const [draftPrice, setDraftPrice] = useState('')
  const [savingId, setSavingId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const pageSize = 25

  const loadInventory = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params = { page, pageSize, search: search || undefined, categoryId: categoryId || undefined, stock: stockFilter }
      const [main, low, out] = await Promise.all([
        api.get<InventoryResponse>('/api/manage/inventory', { params }),
        api.get<InventoryResponse>('/api/manage/inventory', { params: { page: 1, pageSize: 8, stock: 'low' } }),
        api.get<InventoryResponse>('/api/manage/inventory', { params: { page: 1, pageSize: 8, stock: 'out' } }),
      ])
      setRows(main.data.items)
      setTotalCount(main.data.totalCount)
      setSummary(main.data.summary)
      setLowRows([...out.data.items, ...low.data.items])
    } catch {
      setError('Inventory could not be loaded. Refresh or sign in again and retry.')
    } finally {
      setLoading(false)
    }
  }, [page, pageSize, search, categoryId, stockFilter])

  useEffect(() => { void loadInventory() }, [loadInventory])

  function beginEdit(row: InventoryRow) {
    setEditingId(row.variantId)
    setDraftStock(String(row.stockQty))
    setNotice('')
  }

  async function saveStock(row: InventoryRow) {
    const nextStock = Number(draftStock)
    if (!Number.isSafeInteger(nextStock) || nextStock < 0) {
      setNotice('Enter a whole number of zero or more.')
      return
    }
    setSavingId(row.variantId)
    setNotice('')
    try {
      await api.patch(`/api/manage/variants/${row.variantId}/stock`, { set: nextStock, rowVersion: row.rowVersion })
      setEditingId(null)
      setNotice(`Stock updated for ${row.productName}  |  ${row.size}  |  ${row.color}.`)
      await loadInventory()
    } catch (requestError: any) {
      setNotice(requestError?.response?.status === 409
        ? 'This stock changed in another session. Refresh the inventory and try again.'
        : 'Stock could not be saved. Check your connection and try again.')
    } finally {
      setSavingId(null)
    }
  }

  async function savePrice(row: InventoryRow) {
    const nextPrice = Number(draftPrice)
    if (!Number.isFinite(nextPrice) || nextPrice <= 0 || nextPrice > 10_000_000) {
      setNotice('Enter a price greater than zero and no more than PKR 10,000,000.')
      return
    }
    setSavingId(row.variantId)
    setNotice('')
    try {
      await api.put(`/api/manage/variants/${row.variantId}`, {
        priceOverride: nextPrice,
        stockQty: row.stockQty,
        rowVersion: row.rowVersion,
      })
      setEditingPriceId(null)
      setNotice(`Price updated for ${row.productName}  |  ${row.size}  |  ${row.color}.`)
      await loadInventory()
    } catch (requestError: any) {
      setNotice(requestError?.response?.status === 409
        ? 'This variant changed in another session. Refresh the inventory and try again.'
        : 'Price could not be saved. Check the amount and try again.')
    } finally {
      setSavingId(null)
    }
  }

  function stockBadge(quantity: number) {
    if (quantity === 0) return <span className="inventory-badge inventory-badge-out">Out of stock</span>
    if (quantity < 10) return <span className="inventory-badge inventory-badge-low">Low stock</span>
    return <span className="inventory-badge inventory-badge-good">In stock</span>
  }

  function stockControls(row: InventoryRow) {
    const isEditing = editingId === row.variantId
    return <div className="inventory-stock-control">
      {isEditing
        ? <>
            <input aria-label={`Stock quantity for ${row.sku}`} type="number" min="0" step="1" value={draftStock} onChange={event => setDraftStock(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') void saveStock(row); if (event.key === 'Escape') setEditingId(null) }} autoFocus />
            <IconButton label="Save stock" disabled={savingId === row.variantId} onClick={() => void saveStock(row)}><Icon name="check" /></IconButton>
            <IconButton label="Cancel editing" onClick={() => setEditingId(null)}><Icon name="close" /></IconButton>
          </>
        : <>
            <strong>{row.stockQty}</strong>
            {stockBadge(row.stockQty)}
            <IconButton label={`Edit stock for ${row.sku}`} onClick={() => beginEdit(row)}><Icon name="edit" /></IconButton>
          </>}
    </div>
  }

  function tableRows(items: InventoryRow[], compact = false) {
    if (items.length === 0) return <tr><td className="inventory-empty" colSpan={compact ? 4 : 7}>{compact ? 'No low stock items. Nice work.' : 'No inventory matches these filters.'}</td></tr>
    return items.map(row => <tr key={row.variantId} className={row.stockQty < 10 ? 'inventory-row-alert' : ''}>
      {!compact && <td><div className="inventory-product"><InventoryThumbnail row={row} /><span><strong>{row.productName}</strong><small>{row.productCode}  |  {row.category}</small></span></div></td>}
      <td>{compact && <span className="inventory-low-ident"><InventoryThumbnail row={row} /><span><span className="inventory-sku">{row.sku}</span><small>{row.productName}  |  {row.size}  |  {row.color}</small></span></span>}{!compact && <span className="inventory-sku">{row.sku}</span>}{compact && <small className="inventory-mobile-product">{row.productName}  |  {row.size}  |  {row.color}</small>}</td>
      {!compact && <><td>{row.size}</td><td>{row.color}</td></>}
      <td>{stockControls(row)}</td>
      {!compact && <><td><div className="inventory-price-control">{editingPriceId === row.variantId
        ? <><span>PKR</span><input aria-label={`Price for ${row.sku}`} type="number" min="0.01" max="10000000" step="0.01" value={draftPrice} onChange={event => setDraftPrice(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') void savePrice(row); if (event.key === 'Escape') setEditingPriceId(null) }} autoFocus /><IconButton label="Save price" disabled={savingId === row.variantId} onClick={() => void savePrice(row)}><Icon name="check" /></IconButton><IconButton label="Cancel price editing" onClick={() => setEditingPriceId(null)}><Icon name="close" /></IconButton></>
        : <><span>PKR {row.price.toLocaleString()}</span><IconButton label={`Edit price for ${row.sku}`} onClick={() => { setEditingPriceId(row.variantId); setDraftPrice(row.price.toFixed(2)); setNotice('') }}><Icon name="edit" /></IconButton></>}</div></td><td><span className={row.productActive && row.variantActive ? 'inventory-active' : 'inventory-inactive'}>{row.productActive && row.variantActive ? 'Active' : 'Paused'}</span></td></>}
      {compact && <td>{row.stockQty === 0 ? 'Out' : `${row.stockQty} left`}</td>}
    </tr>)
  }

  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))

  return <div className="inventory-workspace">
    <div className="inventory-metrics">
      <article><span>Tracked variants</span><strong>{summary?.variantCount ?? '-'}</strong><small>Across active catalog records</small></article>
      <article><span>Units on hand</span><strong>{summary?.totalUnits.toLocaleString() ?? '-'}</strong><small>Total sellable stock</small></article>
      <article className="inventory-metric-warn"><span>Running low</span><strong>{summary?.lowStockCount ?? '-'}</strong><small>1-9 units remaining</small></article>
      <article className="inventory-metric-critical"><span>Out of stock</span><strong>{summary?.outOfStockCount ?? '-'}</strong><small>Needs replenishment</small></article>
    </div>

    <section className="inventory-low-panel">
      <header className="inventory-section-head"><div><p className="eyebrow">REPLENISHMENT QUEUE</p><h2>Needs attention</h2></div><span className="inventory-alert-count">{summary?.lowStockCount ?? 0} low  |  {summary?.outOfStockCount ?? 0} out</span></header>
      <div className="inventory-table-wrap"><table className="inventory-table inventory-low-table"><thead><tr><th>SKU</th><th>Availability</th><th>Stock</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{tableRows(lowRows, true)}</tbody></table></div>
    </section>

    <section className="inventory-main-panel">
      <header className="inventory-section-head inventory-main-head"><div><p className="eyebrow">STOCK LEDGER</p><h2>All inventory</h2><p>{totalCount.toLocaleString()} variants in this view</p></div><IconButton label="Refresh inventory" onClick={() => void loadInventory()}><Icon name="refresh" /></IconButton></header>
      <div className="inventory-toolbar">
        <form className="inventory-search" onSubmit={event => { event.preventDefault(); setPage(1); setSearch(searchText.trim()) }}><Icon name="search" /><input aria-label="Search inventory" placeholder="Search product, code, or SKU" value={searchText} onChange={event => setSearchText(event.target.value)} /><button type="submit">Search</button></form>
        <select aria-label="Filter by category" value={categoryId} onChange={event => { setPage(1); setCategoryId(event.target.value) }}><option value="">All categories</option>{categories.map(category => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select>
        <select aria-label="Filter by stock level" value={stockFilter} onChange={event => { setPage(1); setStockFilter(event.target.value) }}><option value="all">All stock levels</option><option value="low">Low stock</option><option value="out">Out of stock</option><option value="healthy">Healthy stock</option></select>
      </div>
      {error && <p className="inventory-error" role="alert">{error}</p>}
      {notice && <p className="inventory-notice" role="status">{notice}</p>}
      <div className="inventory-table-wrap"><table className="inventory-table"><thead><tr><th>Product</th><th>SKU</th><th>Size</th><th>Color</th><th>Stock left</th><th>Price</th><th>Status</th></tr></thead><tbody>{loading ? <tr><td className="inventory-empty" colSpan={7}>Loading stock ledger...</td></tr> : tableRows(rows)}</tbody></table></div>
      <footer className="inventory-pagination"><span>Showing {totalCount ? (page - 1) * pageSize + 1 : 0}-{Math.min(page * pageSize, totalCount)} of {totalCount}</span><div><IconButton label="Previous page" disabled={page <= 1 || loading} onClick={() => setPage(value => Math.max(1, value - 1))}><Icon name="left" /></IconButton><span>Page {page} of {pageCount}</span><IconButton label="Next page" disabled={page >= pageCount || loading} onClick={() => setPage(value => Math.min(pageCount, value + 1))}><Icon name="right" /></IconButton></div></footer>
    </section>
  </div>
}

function IconButton({ label, disabled, onClick, children }: { label: string; disabled?: boolean; onClick: () => void; children: ReactNode }) {
  return <button className="inventory-icon-button" type="button" aria-label={label} title={label} disabled={disabled} onClick={onClick}>{children}</button>
}

function InventoryThumbnail({ row }: { row: InventoryRow }) {
  return <span className="inventory-product-mark"><img src={assetUrl(row.imageUrl) ?? ''} alt={row.productName} loading="lazy" /></span>
}

function Icon({ name }: { name: 'check' | 'close' | 'edit' | 'refresh' | 'search' | 'left' | 'right' | 'eye' }) {
  const paths: Record<typeof name, ReactNode> = {
    check: <path d="m5 12 4 4L19 6" />,
    close: <path d="m6 6 12 12M18 6 6 18" />,
    edit: <><path d="M12 20h9" /><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z" /></>,
    refresh: <><path d="M20 7v5h-5" /><path d="M4 17v-5h5" /><path d="M5.6 9A7 7 0 0 1 17.5 6L20 12M4 12l2.5 6A7 7 0 0 0 18.4 15" /></>,
    search: <><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /></>,
    left: <path d="m15 18-6-6 6-6" />,
    right: <path d="m9 18 6-6-6-6" />,
    eye: <><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" /><circle cx="12" cy="12" r="3" /></>,
  }
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">{paths[name]}</svg>
}


type ManagedOrder = { orderId: number; orderDate: string; name: string; totalAmount: number; customer: string; items: number }
type OrderDetail = {
  orderId: number; orderDate: string; status: string; totalAmount: number
  customer: { userId: number; fullName: string; email: string }
  shipName: string; shipLine1: string; shipLine2?: string | null; shipCity: string; shipPostalCode: string; shipCountry: string
  items: { name: string; quantity: number; unitPrice: number; lineTotal: number }[]
  payments: { method: string; status: string; amount: number; transactionRef?: string | null; paidAt?: string | null }[]
}
type OrderDashboard = { ordersToday: number; ordersByStatus: { status: string; count: number }[] }

const nextOrderStatuses: Record<string, string[]> = {
  Pending: ['Paid', 'Cancelled'],
  Paid: ['Shipped', 'Cancelled'],
  Shipped: ['Delivered'],
}
const orderStatusFilters = ['All statuses', 'Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled']

function OrderWorkspace() {
  const [orders, setOrders] = useState<ManagedOrder[]>([])
  const [dashboard, setDashboard] = useState<OrderDashboard | null>(null)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [status, setStatus] = useState('')
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [detail, setDetail] = useState<OrderDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [busyOrder, setBusyOrder] = useState<number | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const pageSize = 15

  const loadOrders = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params: Record<string, string | number> = { page, pageSize }
      if (status) params.status = status
      if (from) params.from = from
      if (to) params.to = to
      if (/^\d+$/.test(search)) params.orderId = Number(search)
      else if (search) params.email = search
      const [list, stats] = await Promise.all([
        api.get('/api/manage/orders', { params }),
        api.get<OrderDashboard>('/api/manage/dashboard'),
      ])
      setOrders(list.data.items)
      setTotalCount(list.data.totalCount)
      setDashboard(stats.data)
    } catch {
      setError('Orders could not be loaded. Refresh or sign in again and retry.')
    } finally {
      setLoading(false)
    }
  }, [page, pageSize, status, search, from, to])

  useEffect(() => { void loadOrders() }, [loadOrders])

  async function updateStatus(order: ManagedOrder, nextStatus: string) {
    setBusyOrder(order.orderId)
    setNotice('')
    try {
      await api.patch(`/api/manage/orders/${order.orderId}/status`, { status: nextStatus })
      setNotice(`Order #${order.orderId} moved to ${nextStatus}.`)
      await loadOrders()
    } catch (requestError: any) {
      setError(requestError?.response?.status === 409
        ? 'That status change is no longer available. Refresh the order list.'
        : `Order #${order.orderId} could not be updated.`)
    } finally {
      setBusyOrder(null)
    }
  }

  async function openOrder(orderId: number) {
    try {
      const response = await api.get<OrderDetail>(`/api/manage/orders/${orderId}`)
      setDetail(response.data)
    } catch {
      setError(`Order #${orderId} details could not be loaded.`)
    }
  }

  const statusCount = (name: string) => dashboard?.ordersByStatus.find(item => item.status === name)?.count ?? 0
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const money = (value: number) => `PKR ${value.toLocaleString()}`

  return <section className="orders-workspace">
    <header className="inventory-section-head orders-head"><div><p className="eyebrow">FULFILMENT DESK</p><h2>Order status</h2></div><IconButton label="Refresh orders" onClick={() => void loadOrders()}><Icon name="refresh" /></IconButton></header>
    <div className="orders-status-cards">
      <article><span>Placed today</span><strong>{dashboard?.ordersToday ?? '-'}</strong></article>
      <article className="orders-pending"><span>Pending</span><strong>{statusCount('Pending')}</strong></article>
      <article><span>Paid</span><strong>{statusCount('Paid')}</strong></article>
      <article><span>Shipped</span><strong>{statusCount('Shipped')}</strong></article>
      <article className="orders-complete"><span>Delivered</span><strong>{statusCount('Delivered')}</strong></article>
    </div>
    <div className="orders-toolbar">
      <form className="inventory-search" onSubmit={event => { event.preventDefault(); setPage(1); setSearch(searchText.trim()) }}><Icon name="search" /><input aria-label="Search orders" placeholder="Order number or customer email" value={searchText} onChange={event => setSearchText(event.target.value)} /><button type="submit">Search</button></form>
      <select aria-label="Filter orders by status" value={status} onChange={event => { setPage(1); setStatus(event.target.value) }}>{orderStatusFilters.map((item, index) => <option key={item} value={index === 0 ? '' : item}>{item}</option>)}</select>
      <label>From <input type="date" value={from} onChange={event => { setPage(1); setFrom(event.target.value) }} /></label>
      <label>To <input type="date" value={to} onChange={event => { setPage(1); setTo(event.target.value) }} /></label>
    </div>
    {error && <p className="inventory-error" role="alert">{error}</p>}
    {notice && <p className="inventory-notice" role="status">{notice}</p>}
    <div className="inventory-table-wrap"><table className="inventory-table orders-table"><thead><tr><th>Order</th><th>Customer</th><th>Placed</th><th>Items</th><th>Total</th><th>Status</th><th>Next step</th><th><span className="sr-only">Details</span></th></tr></thead><tbody>
      {loading ? <tr><td className="inventory-empty" colSpan={8}>Loading orders...</td></tr> : orders.length === 0 ? <tr><td className="inventory-empty" colSpan={8}>No orders match these filters.</td></tr> : orders.map(order => <tr key={order.orderId}>
        <td><strong className="orders-id">#{order.orderId}</strong></td>
        <td className="orders-customer">{order.customer}</td>
        <td>{new Date(order.orderDate).toLocaleDateString()}</td>
        <td>{order.items}</td>
        <td>{money(order.totalAmount)}</td>
        <td><span className={`order-status-chip order-status-${order.name.toLowerCase()}`}>{order.name}</span></td>
        <td>{(nextOrderStatuses[order.name] ?? []).length > 0
          ? <select aria-label={`Update order ${order.orderId} status`} defaultValue="" disabled={busyOrder === order.orderId} onChange={event => { if (event.target.value) void updateStatus(order, event.target.value) }}><option value="">Choose status...</option>{nextOrderStatuses[order.name].map(next => <option key={next} value={next}>{next}</option>)}</select>
          : <span className="orders-no-action">-</span>}</td>
        <td><IconButton label={`View order ${order.orderId}`} onClick={() => void openOrder(order.orderId)}><Icon name="eye" /></IconButton></td>
      </tr>)}
    </tbody></table></div>
    <footer className="inventory-pagination"><span>{totalCount.toLocaleString()} orders</span><div><IconButton label="Previous orders page" disabled={page <= 1 || loading} onClick={() => setPage(value => Math.max(1, value - 1))}><Icon name="left" /></IconButton><span>Page {page} of {pageCount}</span><IconButton label="Next orders page" disabled={page >= pageCount || loading} onClick={() => setPage(value => Math.min(pageCount, value + 1))}><Icon name="right" /></IconButton></div></footer>
    {detail && <div className="order-detail-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) setDetail(null) }}><section className="order-detail-dialog" role="dialog" aria-modal="true" aria-labelledby="order-detail-title"><header><div><p className="eyebrow">ORDER DETAILS</p><h3 id="order-detail-title">Order #{detail.orderId}</h3></div><IconButton label="Close order details" onClick={() => setDetail(null)}><Icon name="close" /></IconButton></header><div className="order-detail-summary"><span className={`order-status-chip order-status-${detail.status.toLowerCase()}`}>{detail.status}</span><span>{new Date(detail.orderDate).toLocaleString()}</span><strong>{money(detail.totalAmount)}</strong></div><div className="order-detail-columns"><div><h4>Customer</h4><strong>{detail.customer.fullName}</strong><span>{detail.customer.email}</span></div><div><h4>Ship to</h4><strong>{detail.shipName}</strong><span>{detail.shipLine1}{detail.shipLine2 ? `, ${detail.shipLine2}` : ''}</span><span>{detail.shipCity}, {detail.shipPostalCode}</span><span>{detail.shipCountry}</span></div></div><h4 className="order-detail-subhead">Items</h4><div className="order-detail-lines">{detail.items.map((item, index) => <div key={`${item.name}-${index}`}><span>{item.name}<small>{item.quantity} x {money(item.unitPrice)}</small></span><strong>{money(item.lineTotal)}</strong></div>)}</div><h4 className="order-detail-subhead">Payment</h4>{detail.payments.map((payment, index) => <div className="order-payment-line" key={`${payment.method}-${index}`}><span>{payment.method}  |  {payment.status}</span><strong>{money(payment.amount)}</strong></div>)}</section></div>}
  </section>
}