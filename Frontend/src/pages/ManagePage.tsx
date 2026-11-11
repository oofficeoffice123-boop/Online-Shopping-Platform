import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { api } from '../auth/apiClient'

type Product = { productId: number; productCode: string; name: string; description?: string; categoryId: number; category: string; basePrice: number; isActive: boolean }
type Order = { orderId: number; orderDate: string; name: string; totalAmount: number; customer: string }
type Dashboard = { ordersByStatus: { status: string; count: number }[]; ordersToday: number; lowStockVariants: { variantId: number; sku: string; stockQty: number; product: string; size: string; color: string }[]; recentOrders: Order[] }

export default function ManagePage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const [products, setProducts] = useState<Product[]>([])
  const [orders, setOrders] = useState<Order[]>([])
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [adminDashboard, setAdminDashboard] = useState<any>(null)
  const [orderDetail, setOrderDetail] = useState<any>(null)
  const [users, setUsers] = useState<any[]>([])
  const [categories, setCategories] = useState<any[]>([])
  const [sizes, setSizes] = useState<any[]>([])
  const [colors, setColors] = useState<any[]>([])
  const [notice, setNotice] = useState('')
  const [search, setSearch] = useState('')
  const [productForm, setProductForm] = useState({ productCode: '', name: '', description: '', categoryId: 0, basePrice: 1 })

  async function load() {
    const [p, o, d, c, s, co] = await Promise.all([
      api.get('/api/manage/products', { params: { page: 1, pageSize: 100, search: search || undefined } }),
      api.get('/api/manage/orders', { params: { page: 1, pageSize: 20 } }),
      api.get<Dashboard>('/api/manage/dashboard'),
      api.get('/api/manage/categories'),
      api.get('/api/manage/sizes'),
      api.get('/api/manage/colors'),
    ])
    setProducts(p.data.items); setOrders(o.data.items); setDashboard(d.data); setCategories(c.data); setSizes(s.data); setColors(co.data)
    if (isAdmin) {
      const [a, u] = await Promise.all([api.get('/api/admin/dashboard'), api.get('/api/admin/users', { params: { page: 1, pageSize: 100 } })])
      setAdminDashboard(a.data); setUsers(u.data.items)
    }
  }
  useEffect(() => { void load().catch(() => setNotice('Could not load management data.')) }, [isAdmin])

  async function createProduct(event: React.FormEvent) {
    event.preventDefault()
    try { await api.post('/api/manage/products', productForm); setNotice('Product created.'); setProductForm({ productCode: '', name: '', description: '', categoryId: 0, basePrice: 1 }); await load() }
    catch { setNotice('Product could not be created. Check the fields and ProductCode uniqueness.') }
  }
  async function toggleProduct(p: Product) { await api.patch(`/api/manage/products/${p.productId}/active`, { isActive: !p.isActive }); await load() }
  async function status(order: Order, next: string) { await api.patch(`/api/manage/orders/${order.orderId}/status`, { status: next }); await load() }
  const nextStatuses: Record<string, string[]> = { Pending: ['Paid', 'Cancelled'], Paid: ['Shipped', 'Cancelled'], Shipped: ['Delivered'] }

  return <main className="manage-page">
    <p className="eyebrow">STAFF AREA</p><h1>Welcome, {user?.fullName}</h1><p>Manage catalog, inventory and customer orders.</p>
    {notice && <p role="status">{notice}</p>}
    <section><h2>Overview</h2><p>Orders today: <strong>{dashboard?.ordersToday ?? 0}</strong></p><div className="manage-statuses">{dashboard?.ordersByStatus.map(x => <span key={x.status}>{x.status}: {x.count}</span>)}</div>
      <h3>Low stock (5 or fewer)</h3><ul>{dashboard?.lowStockVariants.map(v => <li key={v.variantId}>{v.product} · {v.size} · {v.color} — {v.stockQty} left ({v.sku})</li>)}</ul></section>
    <section><h2>Products</h2><label>Search <input value={search} onChange={e => setSearch(e.target.value)} onKeyDown={e => e.key === 'Enter' && void load()} /></label>
      <form onSubmit={e => void createProduct(e)} className="manage-form"><h3>Add product</h3><input placeholder="Product code" required value={productForm.productCode} onChange={e => setProductForm({ ...productForm, productCode: e.target.value })} /><input placeholder="Name" required value={productForm.name} onChange={e => setProductForm({ ...productForm, name: e.target.value })} /><input placeholder="Description" value={productForm.description} onChange={e => setProductForm({ ...productForm, description: e.target.value })} /><select required value={productForm.categoryId || ''} onChange={e => setProductForm({ ...productForm, categoryId: Number(e.target.value) })}><option value="">Category</option>{categories.map((c: any) => <option key={c.categoryId} value={c.categoryId}>{c.name}</option>)}</select><input type="number" min="0.01" step="0.01" required value={productForm.basePrice} onChange={e => setProductForm({ ...productForm, basePrice: Number(e.target.value) })} /><button>Create</button></form>
      <div className="manage-list">{products.map(p => <div key={p.productId}><article><strong>{p.name}</strong> <small>{p.productCode} · {p.category} · PKR {p.basePrice}</small><span>{p.isActive ? 'Active' : 'Inactive'}</span><button onClick={() => void toggleProduct(p)}>{p.isActive ? 'Deactivate' : 'Activate'}</button>{isAdmin&&<button onClick={async()=>{if(window.confirm('Hard-delete this product? Ordered products cannot be deleted.')){await api.delete(`/api/manage/products/${p.productId}`);await load()}}}>Delete</button>}</article><ProductTools product={p} categories={categories} sizes={sizes} colors={colors} reload={load} /></div>)}</div>
    </section>
    <section><h2>Categories, sizes and colors</h2><form className="manage-form" onSubmit={async e => { e.preventDefault(); const f=new FormData(e.currentTarget); await api.post('/api/manage/categories',{name:f.get('name'),parentCategoryId:f.get('parentCategoryId') ? Number(f.get('parentCategoryId')) : null}); await load() }}><input name="name" placeholder="Category name" required /><select name="parentCategoryId"><option value="">No parent</option>{categories.map(c=><option key={c.categoryId} value={c.categoryId}>{c.name}</option>)}</select><button>Add category</button></form><ul>{categories.map(c=><li key={c.categoryId}>{c.name}<button onClick={async()=>{const name=window.prompt('Category name',c.name);if(name){await api.put(`/api/manage/categories/${c.categoryId}`,{name,parentCategoryId:c.parentCategoryId});await load()}}}>Edit</button><button onClick={async()=>{await api.delete(`/api/manage/categories/${c.categoryId}`);await load()}}>Delete</button></li>)}</ul>
      {(['sizes','colors'] as const).map(kind=><div key={kind}><h3>{kind}</h3><form className="manage-form" onSubmit={async e=>{e.preventDefault();const f=new FormData(e.currentTarget);await api.post(`/api/manage/${kind}`,{name:f.get('name')});await load()}}><input name="name" placeholder={`New ${kind.slice(0,-1)}`} required /><button>Add</button></form><ul>{(kind==='sizes'?sizes:colors).map(x=><li key={x.sizeId??x.colorId}>{x.label??x.name}<button onClick={async()=>{const name=window.prompt(`${kind.slice(0,-1)} name`,x.label??x.name);if(name){const id=x.sizeId??x.colorId;await api.put(`/api/manage/${kind}/${id}`,{name});await load()}}}>Edit</button><button onClick={async()=>{await api.delete(`/api/manage/${kind}/${x.sizeId??x.colorId}`);await load()}}>Delete</button></li>)}</ul></div>)}
    </section>
    <section><h2>Orders</h2><div className="manage-list">{orders.map(o => <article key={o.orderId}><button onClick={async()=>{const {data}=await api.get(`/api/manage/orders/${o.orderId}`);setOrderDetail(data)}}>#{o.orderId}</button><small>{o.customer} · {new Date(o.orderDate).toLocaleDateString()} · PKR {o.totalAmount}</small><span>{o.name}</span><select aria-label={`Set status for order ${o.orderId}`} value="" onChange={e => e.target.value && void status(o, e.target.value)}><option value="">Update status</option>{(nextStatuses[o.name] ?? []).map(s => <option key={s}>{s}</option>)}</select></article>)}</div>{orderDetail&&<div role="dialog" aria-label={`Order ${orderDetail.orderId} details`}><button onClick={()=>setOrderDetail(null)}>Close</button><h3>Order #{orderDetail.orderId} · {orderDetail.status}</h3><p>{orderDetail.customer.fullName} · {orderDetail.customer.email}</p><ul>{orderDetail.items.map((item:any,index:number)=><li key={index}>{item.name}: {item.quantity} × PKR {item.unitPrice}</li>)}</ul><ul>{orderDetail.payments.map((payment:any,index:number)=><li key={index}>{payment.method}: {payment.status}, PKR {payment.amount}</li>)}</ul></div>}</section>
    {isAdmin && <><section><h2>Revenue</h2><div className="manage-statuses"><span>Today: PKR {adminDashboard?.revenue?.today ?? 0}</span><span>7 days: PKR {adminDashboard?.revenue?.last7 ?? 0}</span><span>30 days: PKR {adminDashboard?.revenue?.last30 ?? 0}</span></div><h3>Daily revenue, 30 days</h3><div className="revenue-bars" role="img" aria-label="Daily revenue chart">{adminDashboard?.dailyRevenue?.map((x: any) => <span key={x.date} title={`${x.date}: PKR ${x.revenue}`} style={{ height: `${Math.max(2, Math.min(100, x.revenue / Math.max(1, ...adminDashboard.dailyRevenue.map((r: any) => r.revenue)) * 100))}%` }} />)}</div><h3>Top products</h3><ol>{adminDashboard?.topProducts?.map((p: any) => <li key={p.productId}>{p.name}: {p.units} sold</li>)}</ol></section>
      <section><h2>User management</h2><form className="manage-form" onSubmit={async e => { e.preventDefault(); const f = new FormData(e.currentTarget); const { data } = await api.post('/api/admin/users', { fullName: f.get('fullName'), email: f.get('email'), role: f.get('role') }); window.alert(`Initial password (copy now): ${data.initialPassword}`); await load() }}><input name="fullName" placeholder="Full name" required /><input name="email" type="email" placeholder="Email" required /><select name="role"><option>Manager</option><option>Admin</option></select><button>Create staff user</button></form><div className="manage-list">{users.map((u: any) => <article key={u.userId}><strong>{u.fullName}</strong><small>{u.email} · {u.role}{u.isDeleted ? ' · Deleted' : ''}</small><select disabled={u.isDeleted} value={u.role} onChange={async e => { await api.patch(`/api/admin/users/${u.userId}/role`, { role: e.target.value }); await load() }}><option>Customer</option><option>Manager</option><option>Admin</option></select><button disabled={u.userId === user?.userId || u.isDeleted} onClick={async () => { await api.patch(`/api/admin/users/${u.userId}/active`, { isActive: !u.isActive }); await load() }}>{u.isActive ? 'Disable' : 'Enable'}</button><button disabled={u.userId === user?.userId} onClick={async () => { if (u.isDeleted) { await api.patch(`/api/admin/users/${u.userId}/deleted`, { isDeleted: false }); } else if (window.confirm(`Soft-delete ${u.fullName}?`)) { await api.patch(`/api/admin/users/${u.userId}/deleted`, { isDeleted: true }); } await load() }}>{u.isDeleted ? 'Restore' : 'Delete'}</button></article>)}</div></section></>}
  </main>
}

function ProductTools({ product, categories, sizes, colors, reload }: { product: Product; categories: any[]; sizes: any[]; colors: any[]; reload: () => Promise<void> }) {
  const [open, setOpen] = useState(false)
  const [variants, setVariants] = useState<any[]>([])
  const [images, setImages] = useState<any[]>([])
  const [variantForm, setVariantForm] = useState({ sizeId: 0, colorId: 0, stockQty: 0, priceOverride: '' })
  const refresh = async () => {
    const [v, i] = await Promise.all([api.get(`/api/manage/products/${product.productId}/variants`), api.get(`/api/manage/products/${product.productId}/images`)]); setVariants(v.data); setImages(i.data)
  }
  async function show() { if (!open) await refresh(); setOpen(!open) }
  async function upload(files: FileList | null) { if (!files?.length) return; const body = new FormData(); Array.from(files).forEach(file => body.append('files', file)); await api.post(`/api/manage/products/${product.productId}/images`, body); await refresh() }
  async function reorder(from: number, to: number) { const next = [...images]; const [item] = next.splice(from, 1); next.splice(to, 0, item); setImages(next); await api.put(`/api/manage/products/${product.productId}/images/reorder`, { imageIds: next.map(x => x.imageId) }) }
  return <div className="manage-product-tools"><button type="button" onClick={() => void show()}>{open ? 'Hide editor' : 'Edit product, stock and images'}</button>{open && <div>
    <form className="manage-form" onSubmit={async e => { e.preventDefault(); const f = new FormData(e.currentTarget); await api.put(`/api/manage/products/${product.productId}`, { productCode: f.get('productCode'), name: f.get('name'), description: f.get('description'), categoryId: Number(f.get('categoryId')), basePrice: Number(f.get('basePrice')) }); await reload() }}>
      <input name="productCode" defaultValue={product.productCode} required /><input name="name" defaultValue={product.name} required /><input name="description" defaultValue={product.description ?? ''} /><select name="categoryId" defaultValue={product.categoryId}>{categories.map(c => <option key={c.categoryId} value={c.categoryId}>{c.name}</option>)}</select><input name="basePrice" type="number" min="0.01" step="0.01" defaultValue={product.basePrice} /><button>Save product</button>
    </form>
    <h4>Variants and stock</h4><form className="manage-form" onSubmit={async e => { e.preventDefault(); await api.post(`/api/manage/products/${product.productId}/variants`, { sizeId: variantForm.sizeId, colorId: variantForm.colorId, stockQty: variantForm.stockQty, priceOverride: variantForm.priceOverride ? Number(variantForm.priceOverride) : null }); setVariantForm({ sizeId: 0, colorId: 0, stockQty: 0, priceOverride: '' }); await refresh() }}><select required value={variantForm.sizeId || ''} onChange={e => setVariantForm({ ...variantForm, sizeId: Number(e.target.value) })}><option value="">Size</option>{sizes.map(x => <option key={x.sizeId} value={x.sizeId}>{x.label}</option>)}</select><select required value={variantForm.colorId || ''} onChange={e => setVariantForm({ ...variantForm, colorId: Number(e.target.value) })}><option value="">Color</option>{colors.map(x => <option key={x.colorId} value={x.colorId}>{x.name}</option>)}</select><input type="number" min="0" value={variantForm.stockQty} onChange={e => setVariantForm({ ...variantForm, stockQty: Number(e.target.value) })} /><input placeholder="Price override (optional)" type="number" min="0.01" step=".01" value={variantForm.priceOverride} onChange={e => setVariantForm({ ...variantForm, priceOverride: e.target.value })} /><button>Add variant</button></form>
    <ul>{variants.map(v => <li key={v.variantId}>{v.sku} · {v.size} · {v.color} — {v.stockQty} in stock <button onClick={async () => { await api.patch(`/api/manage/variants/${v.variantId}/stock`, { set: 0, rowVersion: v.rowVersion }); await refresh() }}>Set stock to 0</button><button onClick={async()=>{const price=window.prompt('New override price (leave blank for product price)',v.priceOverride??'');if(price!==null){await api.put(`/api/manage/variants/${v.variantId}`,{priceOverride:price===''?null:Number(price),stockQty:v.stockQty,rowVersion:v.rowVersion});await refresh()}}}>Edit price</button><button onClick={async () => { if (window.confirm('Delete this variant? Ordered variants cannot be deleted.')) { await api.delete(`/api/manage/variants/${v.variantId}`); await refresh() } }}>Delete</button></li>)}</ul>
    <h4>Images</h4><input type="file" accept=".jpg,.jpeg,.png,.webp" multiple onChange={e => void upload(e.target.files)} /><ul>{images.map((im, index) => <li key={im.imageId} draggable onDragStart={e => e.dataTransfer.setData('text/plain', String(index))} onDragOver={e => e.preventDefault()} onDrop={e => { e.preventDefault(); void reorder(Number(e.dataTransfer.getData('text/plain')), index) }}><img src={im.url} alt="Product" width="48" /> {im.isPrimary ? 'Primary' : ''}<button onClick={async () => { await api.put(`/api/manage/products/${product.productId}/images/${im.imageId}/primary`); await refresh() }}>Set primary</button><button onClick={async () => { await api.delete(`/api/manage/products/${product.productId}/images/${im.imageId}`); await refresh() }}>Delete</button> <small>Drag to reorder</small></li>)}</ul>
  </div>}</div>
}
