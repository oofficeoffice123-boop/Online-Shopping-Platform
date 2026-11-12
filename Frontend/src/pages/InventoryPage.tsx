import { useEffect, useState } from 'react'
import { api } from '../auth/apiClient'
import './ManagePage.css'
import './ManagerWorkspace.css'

type Row = { variantId: number; productId: number; productCode: string; productName: string; category: string; sku: string; size: string; color: string; stockQty: number; price: number; discountPercent: number | null; rowVersion: string }
type Category = { categoryId: number; name: string }
type RestockEntry = { sku: string; quantity: number; match: Row | null; issue: string | null }
type DiscountPreview = { affectedCount: number; percent: number; remove: boolean; averagePriceDrop: number; warnings: string[]; items: { variantId: number; productId: number; productName: string; sku: string; oldPrice: number; newPrice: number; warning: string | null }[] }

export default function InventoryPage() {
  const [rows, setRows] = useState<Row[]>([])
  const [selected, setSelected] = useState<Set<number>>(() => new Set())
  const [selectedOnPage, setSelectedOnPage] = useState<Record<number, number>>({})
  const [categories, setCategories] = useState<Category[]>([])
  const [search, setSearch] = useState('')
  const [stock, setStock] = useState('')
  const [category, setCategory] = useState('')
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [editing, setEditing] = useState<number | null>(null)
  const [draft, setDraft] = useState('')
  const [discount, setDiscount] = useState('')
  const [preview, setPreview] = useState<DiscountPreview | null>(null)
  const [removeDiscount, setRemoveDiscount] = useState(false)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [restockText, setRestockText] = useState('')
  const [restockEntries, setRestockEntries] = useState<RestockEntry[]>([])
  const [restockLoading, setRestockLoading] = useState(false)
  const [restockSaving, setRestockSaving] = useState(false)

  async function loadInventory() {
    setLoading(true)
    setError('')
    try {
      const { data } = await api.get('/api/manage/inventory', { params: { page, pageSize: 25, search: search || undefined, stock: stock || undefined, categoryId: category || undefined } })
      setRows(data.items)
      setTotal(data.totalCount)
    } catch {
      setError('Inventory could not be loaded. Refresh or sign in again and retry.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void loadInventory() }, [page, search, stock, category])
  useEffect(() => { api.get<Category[]>('/api/manage/categories').then(({ data }) => setCategories(data)).catch(() => setError('Categories could not be loaded.')) }, [])

  async function saveStock(row: Row) {
    try {
      await api.patch(`/api/manage/variants/${row.variantId}/stock`, { set: Number(draft), rowVersion: row.rowVersion })
      setEditing(null)
      setNotice(`Stock updated for ${row.sku}.`)
      await loadInventory()
    } catch {
      setError(`Stock for ${row.sku} could not be updated. Refresh and retry.`)
    }
  }

  async function openDiscountPreview(remove: boolean) {
    setError('')
    setRemoveDiscount(remove)
    setPreviewLoading(true)
    try {
      const { data } = await api.post<DiscountPreview>('/api/manage/discounts/preview', { variantIds: [...selected], percent: Number(discount || 0), remove })
      setPreview(data)
    } catch {
      setError('Discount preview could not be loaded. Refresh the selection and retry.')
    } finally {
      setPreviewLoading(false)
    }
  }

  async function applyDiscount() {
    try {
      await api.post('/api/manage/discounts/bulk', { variantIds: [...selected], percent: Number(discount || 0), remove: removeDiscount })
      setNotice('Discount updated for the selected variants.')
      setPreview(null)
      setSelected(new Set())
      setSelectedOnPage({})
      await loadInventory()
    } catch {
      setError('Discount could not be applied. Refresh the inventory and retry.')
    }
  }

  async function resolveEntries(items: { sku: string; quantity: number }[]) {
    setRestockLoading(true)
    setError('')
    try {
      const entries = await Promise.all(items.map(async item => {
        if (!Number.isInteger(item.quantity) || item.quantity <= 0)
          return { ...item, match: null, issue: 'Enter a positive whole quantity.' }
        try {
          const { data } = await api.get('/api/manage/inventory', { params: { page: 1, pageSize: 100, search: item.sku.trim() } })
          const matches: Row[] = data.items.filter((row: Row) => row.sku.toLowerCase() === item.sku.trim().toLowerCase())
          if (matches.length === 1) return { ...item, sku: item.sku.trim(), match: matches[0], issue: null }
          return { ...item, match: null, issue: matches.length > 1 ? 'SKU is ambiguous. Refine it and check again.' : 'SKU was not found. Edit it and check again.' }
        } catch {
          return { ...item, match: null, issue: 'Could not verify this SKU. Try again.' }
        }
      }))
      setRestockEntries(entries)
    } finally {
      setRestockLoading(false)
    }
  }

  function parsePastedList() {
    const items = restockText.split(/[\n,;]+/).map(value => value.trim()).filter(Boolean).map(value => {
      const found = value.match(/^(.+?)\s*(?:x|×)\s*(\d+)$/i)
      return found ? { sku: found[1].trim(), quantity: Number(found[2]) } : { sku: value, quantity: 0 }
    })
    if (!items.length) { setError('Paste one or more SKU and quantity pairs first.'); return }
    void resolveEntries(items)
  }

  async function parseWithAi() {
    if (!restockText.trim()) { setError('Paste a restock list first.'); return }
    setError('')
    try {
      const { data } = await api.post<{ items: { sku: string; quantity: number }[] }>('/api/manage/restock/parse', { text: restockText })
      setNotice('AI extracted the entries. Review the SKU matches and quantities below.')
      await resolveEntries(data.items)
    } catch {
      setError('AI could not parse this list. You can still build a preview from SKU x quantity entries.')
    }
  }

  function editEntry(index: number, change: Partial<RestockEntry>) {
    setRestockEntries(previous => previous.map((entry, current) => current === index
      ? { ...entry, ...change, match: change.sku !== undefined ? null : entry.match, issue: change.sku !== undefined ? 'Check this SKU to match it to a product.' : change.quantity !== undefined ? (Number.isInteger(change.quantity) && (change.quantity ?? 0) > 0 ? null : 'Enter a positive whole quantity.') : entry.issue }
      : entry))
  }

  async function checkEntry(index: number) {
    const entry = restockEntries[index]
    if (entry) await resolveEntries(restockEntries.map((item, current) => current === index ? { sku: entry.sku, quantity: entry.quantity } : { sku: item.sku, quantity: item.quantity }))
  }

  async function confirmRestock() {
    if (!restockEntries.length || restockEntries.some(entry => !entry.match || entry.issue)) return
    setRestockSaving(true)
    setError('')
    try {
      await api.post('/api/manage/restock/bulk', { items: restockEntries.map(({ sku, quantity }) => ({ sku, quantity })) })
      setNotice(`Restock applied to ${restockEntries.length} variants.`)
      setRestockText('')
      setRestockEntries([])
      await loadInventory()
    } catch {
      setError('Restock could not be applied. The server rejected one or more SKUs or quantities.')
    } finally {
      setRestockSaving(false)
    }
  }

  const selectedPageCount = new Set([...selected].map(id => selectedOnPage[id]).filter(Boolean)).size
  const canRestock = restockEntries.length > 0 && restockEntries.every(entry => entry.match && !entry.issue)

  return <main className="manage-page inventory-page">
    <header className="workspace-header"><div><p className="eyebrow">Z SQUARE <span>/</span> OPERATIONS</p><h1>Inventory</h1></div><span className="workspace-live"><i /> STOCK CONTROL</span></header>

    {error && <div className="workspace-alert" role="alert">{error}</div>}{notice && <div className="workspace-notice" role="status">{notice}</div>}

    <section className="inventory-main-panel inventory-ledger">
      <header className="inventory-section-head"><div><p className="panel-kicker">CATALOG CONTROL</p><h2>All inventory</h2><p>{total.toLocaleString()} variants in this view</p></div><button className="workspace-quiet-button" onClick={() => void loadInventory()} disabled={loading}>↻ <span>Refresh</span></button></header>
      <div className="inventory-toolbar inventory-filters"><label className="inventory-search-field"><span>Search</span><input aria-label="Search inventory" placeholder="Product name, code, or SKU" value={search} onChange={event => { setPage(1); setSearch(event.target.value) }} /></label>
        <label><span>Category</span><select value={category} onChange={event => { setPage(1); setCategory(event.target.value) }}><option value="">All categories</option>{categories.map(item => <option value={item.categoryId} key={item.categoryId}>{item.name}</option>)}</select></label>
        <label><span>Availability</span><select value={stock} onChange={event => { setPage(1); setStock(event.target.value) }}><option value="">All stock levels</option><option value="low">Low stock</option><option value="out">Out of stock</option></select></label>
        <button className="workspace-secondary-button" onClick={() => { setSelected(previous => new Set([...previous, ...rows.map(row => row.variantId)])); setSelectedOnPage(previous => ({ ...previous, ...Object.fromEntries(rows.map(row => [row.variantId, page])) })) }}>Select page</button>
      </div>

      {selected.size > 0 && <div className="inventory-selection-bar"><div><strong>{selected.size} variants selected</strong><span>Across {selectedPageCount} {selectedPageCount === 1 ? 'page' : 'pages'}</span></div><button onClick={() => { setSelected(new Set()); setSelectedOnPage({}) }}>Clear selection</button></div>}
      <div className="inventory-table-wrap"><table className="inventory-table manager-table"><thead><tr><th><input type="checkbox" aria-label="Select all on current page" checked={rows.length > 0 && rows.every(row => selected.has(row.variantId))} onChange={event => { setSelected(previous => { const next = new Set(previous); rows.forEach(row => event.target.checked ? next.add(row.variantId) : next.delete(row.variantId)); return next }); setSelectedOnPage(previous => { const next = { ...previous }; rows.forEach(row => event.target.checked ? next[row.variantId] = page : delete next[row.variantId]); return next }) }} /></th><th>Product</th><th>SKU</th><th>Category</th><th>Stock on hand</th><th>Price</th><th>Discount</th></tr></thead>
        <tbody>{loading ? <tr><td colSpan={7} className="table-state">Loading inventory…</td></tr> : !rows.length ? <tr><td colSpan={7} className="table-state">No products match these filters.</td></tr> : rows.map(row => <tr key={row.variantId}>
          <td><input type="checkbox" aria-label={`Select ${row.productName} ${row.sku}`} checked={selected.has(row.variantId)} onChange={event => { setSelected(previous => { const next = new Set(previous); event.target.checked ? next.add(row.variantId) : next.delete(row.variantId); return next }); setSelectedOnPage(previous => { const next = { ...previous }; event.target.checked ? next[row.variantId] = page : delete next[row.variantId]; return next }) }} /></td>
          <td><div className="ledger-product"><strong>{row.productName}</strong><small>{row.productCode} · {row.size} · {row.color}</small></div></td><td><code>{row.sku}</code></td><td>{row.category}</td>
          <td>{editing === row.variantId ? <div className="stock-edit"><input aria-label={`New stock for ${row.sku}`} type="number" min="0" value={draft} onChange={event => setDraft(event.target.value)} /><button onClick={() => void saveStock(row)}>Save</button><button className="text-action" onClick={() => setEditing(null)}>Cancel</button></div> : <div className="stock-cell"><strong>{row.stockQty}</strong><span className={row.stockQty === 0 ? 'stock-pill stock-pill-out' : row.stockQty < 10 ? 'stock-pill stock-pill-low' : 'stock-pill stock-pill-good'}>{row.stockQty === 0 ? 'Out of stock' : row.stockQty < 10 ? 'Low stock' : 'In stock'}</span><button className="text-action" onClick={() => { setEditing(row.variantId); setDraft(String(row.stockQty)) }}>Edit</button></div>}</td>
          <td>Rs. {row.price.toLocaleString()}</td><td>{row.discountPercent ? <span className="discount-pill">{row.discountPercent}% off</span> : <span className="muted-dash">—</span>}</td>
        </tr>)}</tbody></table></div>
      <footer className="ledger-pagination"><span>Showing {total ? (page - 1) * 25 + 1 : 0}–{Math.min(page * 25, total)} of {total.toLocaleString()}</span><div><button disabled={page <= 1 || loading} onClick={() => setPage(value => value - 1)}>Previous</button><span>Page {page} of {Math.max(1, Math.ceil(total / 25))}</span><button disabled={page * 25 >= total || loading} onClick={() => setPage(value => value + 1)}>Next</button></div></footer>
      <div className="discount-toolbar"><div><p className="panel-kicker">BULK PROMOTION</p><strong>Apply one discount to the selection</strong><small>Preview each updated price before applying.</small></div><div className="discount-actions"><label><span className="sr-only">Discount percentage</span><input aria-label="Discount percentage" type="number" min="0" max="100" placeholder="Percent" value={discount} onChange={event => setDiscount(event.target.value)} /></label><button className="workspace-primary-button" disabled={!selected.size || previewLoading} onClick={() => void openDiscountPreview(false)}>{previewLoading ? 'Building preview…' : `Review ${selected.size || ''} selected`}</button><button className="workspace-secondary-button" disabled={!selected.size || previewLoading} onClick={() => void openDiscountPreview(true)}>Remove discount</button></div></div>
    </section>

    <section className="restock-workspace">
      <div className="restock-heading"><div><p className="panel-kicker">BATCH RESTOCK</p><h2>Restocking assistant</h2></div><span className="ai-mark">AI <i>EXTRACTION</i></span></div>
      <div className="restock-content"><div className="restock-input-column"><label htmlFor="restock-input">Supplier list</label><textarea id="restock-input" rows={8} value={restockText} onChange={event => setRestockText(event.target.value)} placeholder={'KH-1023 x 50\nKH-1045 x 30'} /><div className="restock-input-footer"><div><button className="workspace-secondary-button" disabled={restockLoading || !restockText.trim()} onClick={parsePastedList}>{restockLoading ? 'Checking…' : 'Build preview'}</button><button className="workspace-primary-button" disabled={restockLoading || !restockText.trim()} onClick={() => void parseWithAi()}>{restockLoading ? 'Working…' : '✦ Parse with AI'}</button></div></div></div>
        <div className="restock-preview-column"><div className="restock-preview-heading"><div><h3>Review entries</h3><p>{restockEntries.length ? `${restockEntries.length} line${restockEntries.length === 1 ? '' : 's'} parsed` : 'Your matched products will appear here.'}</p></div>{restockEntries.length > 0 && <button className="text-action" onClick={() => setRestockEntries([])}>Clear</button>}</div>
          {restockLoading ? <div className="restock-empty">Matching SKUs to catalog…</div> : !restockEntries.length ? <div className="restock-empty"><span className="restock-empty-icon">↳</span><strong>Ready when you are</strong></div> : <div className="restock-review-table"><table className="inventory-table"><thead><tr><th>SKU</th><th>Product match</th><th>Current</th><th>Add</th><th>New total</th></tr></thead><tbody>{restockEntries.map((entry, index) => <tr key={`${entry.sku}-${index}`}><td><input aria-label={`SKU on line ${index + 1}`} value={entry.sku} onChange={event => editEntry(index, { sku: event.target.value })} onBlur={() => { if (entry.sku.trim() && !entry.match) void checkEntry(index) }} /><small className={entry.issue ? 'entry-issue' : 'entry-ok'}>{entry.issue ?? 'Matched'}</small></td><td>{entry.match ? <span className="matched-product">{entry.match.productName}<small>{entry.match.category}</small></span> : <span className="unmatched-product">Needs a match</span>}</td><td>{entry.match?.stockQty ?? '—'}</td><td><input aria-label={`Quantity to add on line ${index + 1}`} type="number" min="1" step="1" value={entry.quantity || ''} onChange={event => editEntry(index, { quantity: Number(event.target.value) })} /></td><td><strong>{entry.match && !entry.issue ? (entry.match.stockQty + entry.quantity).toLocaleString() : '—'}</strong></td></tr>)}</tbody></table></div>}
          <div className="restock-confirm-row"><span>{restockEntries.length && !canRestock ? 'Resolve flagged lines before applying.' : restockEntries.length ? `${restockEntries.length} stock updates are ready.` : 'No stock changes have been made.'}</span><button className="workspace-primary-button" disabled={!canRestock || restockSaving || restockLoading} onClick={() => void confirmRestock()}>{restockSaving ? 'Applying…' : 'Confirm restock'}</button></div>
        </div>
      </div>
    </section>

    {preview && <div className="order-detail-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) setPreview(null) }}><section className="order-detail-dialog discount-dialog" role="dialog" aria-modal="true" aria-labelledby="discount-preview-title"><div className="panel-kicker">FINAL REVIEW</div><h2 id="discount-preview-title">Confirm bulk discount</h2><p className="discount-summary">{preview.affectedCount} variants · {preview.remove ? 'Remove current discounts' : `${preview.percent}% discount`} · {preview.averagePriceDrop >= 0 ? 'Average price drop' : 'Average price increase'} <strong>Rs. {Math.abs(preview.averagePriceDrop).toFixed(2)}</strong></p>{preview.warnings.length > 0 && <div className="preview-warnings">{preview.warnings.map(warning => <p key={warning}>ⓘ {warning}</p>)}</div>}<div className="preview-scroll"><table className="inventory-table manager-table"><thead><tr><th>Product</th><th>SKU</th><th>Old price</th><th>New price</th><th>Note</th></tr></thead><tbody>{preview.items.map(item => <tr key={item.variantId}><td>{item.productName}</td><td>{item.sku}</td><td>Rs. {item.oldPrice.toFixed(2)}</td><td><strong>Rs. {item.newPrice.toFixed(2)}</strong></td><td>{item.warning ?? '—'}</td></tr>)}</tbody></table></div><div className="dialog-actions"><button className="workspace-secondary-button" onClick={() => setPreview(null)}>Go back</button><button className="workspace-primary-button" disabled={previewLoading} onClick={() => void applyDiscount()}>Apply to {preview.affectedCount} variants</button></div></section></div>}
  </main>
}
