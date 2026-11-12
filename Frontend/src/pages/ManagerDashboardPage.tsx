import { useEffect, useState } from 'react'
import { api } from '../auth/apiClient'
import { Feedback } from '../components/ui/Feedback'
import './ManagePage.css'
import './ManagerWorkspace.css'

type Dash = { ordersToday: number; ordersByStatus: { status: string; count: number }[]; lowStockVariants: { variantId: number; sku: string; stockQty: number; product: string; size: string; color: string }[] }
type Sales = { bucket: number; revenue: number; units: number }[]
type Best = { productId: number; name: string; unitsSold: number; revenue: number }
type Summary = { variantCount: number; totalUnits: number; lowStockCount: number; outOfStockCount: number }

export default function ManagerDashboardPage() {
  const today = new Date()
  const [dashboard, setDashboard] = useState<Dash | null>(null)
  const [summary, setSummary] = useState<Summary | null>(null)
  const [sales, setSales] = useState<Sales>([])
  const [bestSellers, setBestSellers] = useState<Best[]>([])
  const [view, setView] = useState<'monthly' | 'yearly'>('monthly')
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth() + 1)
  const [loading, setLoading] = useState(true)
  const [salesLoading, setSalesLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    Promise.all([
      api.get<Dash>('/api/manage/dashboard'),
      api.get('/api/manage/inventory', { params: { page: 1, pageSize: 1 } }),
      api.get<Best[]>('/api/manage/best-sellers'),
    ]).then(([orders, inventory, best]) => {
      setDashboard(orders.data)
      setSummary(inventory.data.summary)
      setBestSellers(best.data)
    }).catch(() => setError('Manager data could not be loaded. Refresh the page or sign in again.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    setSalesLoading(true)
    api.get<{ items: Sales }>('/api/manage/sales', { params: { view, period: month, year } })
      .then(response => setSales(response.data.items))
      .catch(() => setError('Sales data could not be loaded.'))
      .finally(() => setSalesLoading(false))
  }, [view, year, month])

  const maxRevenue = Math.max(1, ...sales.map(item => item.revenue))
  const rangeLabel = view === 'monthly'
    ? `${new Date(year, month - 1).toLocaleString(undefined, { month: 'long' })} ${year}`
    : `${year}`

  return <main className="manage-page manager-dashboard">
    <header className="workspace-header">
      <div><p className="eyebrow">Z SQUARE <span>/</span> OPERATIONS</p><h1>Dashboard</h1></div>
      <span className="workspace-live"><i /> LIVE OVERVIEW</span>
    </header>

    {error && <Feedback className="workspace-alert">{error}</Feedback>}
    {loading ? <div className="workspace-loading">Loading your store overview…</div> : <>
      <section className="metric-grid" aria-label="Inventory summary">
        <article className="metric-card"><span>Tracked variants</span><strong>{summary?.variantCount.toLocaleString() ?? '—'}</strong><small>Across the catalog</small></article>
        <article className="metric-card"><span>Units on hand</span><strong>{summary?.totalUnits.toLocaleString() ?? '—'}</strong><small>Available inventory</small></article>
        <article className="metric-card metric-card-warn"><span>Running low</span><strong>{summary?.lowStockCount.toLocaleString() ?? '—'}</strong><small>1–9 units remaining</small></article>
        <article className="metric-card metric-card-alert"><span>Out of stock</span><strong>{summary?.outOfStockCount.toLocaleString() ?? '—'}</strong><small>Needs replenishment</small></article>
      </section>

      <section className="dashboard-panel sales-panel">
        <div className="panel-heading"><div><p className="panel-kicker">REVENUE</p><h2>Sales over time</h2><p className="panel-subtitle">{rangeLabel} · Revenue from completed orders</p></div>
          <div className="chart-controls"><div className="view-toggle" role="group" aria-label="Sales view"><button className={view === 'monthly' ? 'is-active' : ''} onClick={() => setView('monthly')}>Month</button><button className={view === 'yearly' ? 'is-active' : ''} onClick={() => setView('yearly')}>Year</button></div>
            {view === 'monthly' && <select aria-label="Select month" value={month} onChange={event => setMonth(Number(event.target.value))}>{Array.from({ length: 12 }, (_, index) => <option value={index + 1} key={index}>{new Date(2000, index).toLocaleString(undefined, { month: 'long' })}</option>)}</select>}
            <select aria-label="Select year" value={year} onChange={event => setYear(Number(event.target.value))}>{Array.from({ length: 6 }, (_, index) => today.getFullYear() - index).map(value => <option key={value}>{value}</option>)}</select>
          </div>
        </div>
        <div className="sales-chart" aria-label={`Sales chart for ${rangeLabel}`}>
          {salesLoading ? <div className="chart-empty">Loading sales…</div> : sales.every(item => item.revenue === 0) ? <div className="chart-empty">No sales recorded for this period.</div> : <>
            <div className="chart-y-axis"><span>Rs. {Math.round(maxRevenue).toLocaleString()}</span><span>Rs. {Math.round(maxRevenue / 2).toLocaleString()}</span><span>Rs. 0</span></div>
            <div className="chart-plot">{sales.map(item => <div className="chart-column" key={item.bucket} title={`${view === 'monthly' ? `Day ${item.bucket}` : new Date(year, item.bucket - 1).toLocaleString(undefined, { month: 'long' })}: Rs. ${item.revenue.toLocaleString()} · ${item.units} units`}><div className="chart-bar-track"><div className="chart-bar" style={{ height: `${item.revenue === 0 ? 0 : Math.max(3, item.revenue / maxRevenue * 100)}%` }} /></div><span className="chart-label">{view === 'monthly' ? (item.bucket % 5 === 1 || item.bucket === sales.length ? item.bucket : '') : new Date(year, item.bucket - 1).toLocaleString(undefined, { month: 'short' })}</span></div>)}</div>
          </>}
        </div>
      </section>

      <div className="dashboard-lower-grid">
        <section className="dashboard-panel attention-panel"><div className="panel-heading"><div><p className="panel-kicker">STOCK CHECK</p><h2>Attention needed</h2><p className="panel-subtitle">{dashboard?.lowStockVariants.length ?? 0} variants at 5 units or fewer</p></div><span className="panel-count">{dashboard?.lowStockVariants.length ?? 0}</span></div>
          {dashboard?.lowStockVariants.length ? <div className="attention-list">{dashboard.lowStockVariants.slice(0, 8).map(item => <article key={item.variantId}><span className={item.stockQty === 0 ? 'stock-dot is-empty' : 'stock-dot'} /><div className="attention-product"><strong>{item.product}</strong><small>{item.sku} · {item.size} · {item.color}</small></div><span className={item.stockQty === 0 ? 'stock-quantity is-empty' : 'stock-quantity'}>{item.stockQty === 0 ? 'Out' : `${item.stockQty} left`}</span></article>)}</div> : <div className="panel-empty">Everything is comfortably in stock.</div>}
        </section>

        <section className="dashboard-panel best-panel"><div className="panel-heading"><div><p className="panel-kicker">CUSTOMER FAVORITES</p><h2>Best sellers</h2><p className="panel-subtitle">All-time units and revenue</p></div></div>
          {bestSellers.length ? <><article className="top-seller"><span className="top-seller-rank">#1 TOP SELLER</span><h3>{bestSellers[0].name}</h3><div><strong>{bestSellers[0].unitsSold.toLocaleString()} <small>units</small></strong><strong>Rs. {bestSellers[0].revenue.toLocaleString()}</strong></div></article><ol className="seller-list">{bestSellers.slice(1).map((item, index) => <li key={item.productId}><span className="seller-rank">0{index + 2}</span><span className="seller-name">{item.name}</span><span className="seller-units">{item.unitsSold.toLocaleString()} sold</span><strong>Rs. {item.revenue.toLocaleString()}</strong></li>)}</ol></> : <div className="panel-empty">Sales will appear here after orders are placed.</div>}
        </section>
      </div>

      <section className="order-summary-panel"><div><p className="panel-kicker">FULFILMENT</p><h2>Orders today</h2><strong className="today-orders">{dashboard?.ordersToday ?? 0}</strong></div><div className="order-status-list">{dashboard?.ordersByStatus.map(item => <div key={item.status}><span>{item.status}</span><strong>{item.count}</strong></div>)}</div></section>
    </>}
  </main>
}
