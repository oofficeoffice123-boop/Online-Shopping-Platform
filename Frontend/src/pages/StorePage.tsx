import { type FormEvent, useState } from 'react'
import '../App.css'

type Filters = {
  category: string | null
  minPrice: number | null
  maxPrice: number | null
  color: string | null
  keyword: string | null
  size: string | null
  sort: string | null
  page: number
  pageSize: number
}

type Product = {
  productId: number
  productCode: string
  name: string
  description: string | null
  category: string
  price: number
  primaryImageUrl: string | null
}

type PagedProducts = { items: Product[]; page: number; pageSize: number; totalCount: number; totalPages: number }
type SmartResponse = { products: PagedProducts; interpretedFilters: Filters; fallback: boolean }

const emptyFilters: Filters = {
  category: null, minPrice: null, maxPrice: null, color: null, keyword: null,
  size: null, sort: null, page: 1, pageSize: 20,
}

const apiBase = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7115'

export default function StorePage() {
  const [query, setQuery] = useState('')
  const [aiSearch, setAiSearch] = useState(false)
  const [filters, setFilters] = useState<Filters>(emptyFilters)
  const [products, setProducts] = useState<PagedProducts | null>(null)
  const [fallback, setFallback] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const updateFilter = <K extends keyof Filters>(key: K, value: Filters[K]) =>
    setFilters(current => ({ ...current, [key]: value, page: 1 }))

  async function runSearch(useAi = aiSearch, useQueryAsKeyword = true) {
    setBusy(true)
    setError('')
    setFallback(false)
    try {
      if (useAi) {
        const response = await fetch(`${apiBase}/api/search/smart`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ query }),
        })
        if (!response.ok) throw new Error(await response.text())
        const data = await response.json() as SmartResponse
        setProducts(data.products)
        setFilters(data.interpretedFilters)
        setFallback(data.fallback)
      } else {
        const normalFilters = {
          ...filters,
          keyword: filters.keyword ?? (useQueryAsKeyword ? query.trim() || null : null),
        }
        setFilters(normalFilters)
        const params = new URLSearchParams()
        Object.entries(normalFilters).forEach(([key, value]) => {
          if (value !== null && value !== '') params.set(key, String(value))
        })
        const response = await fetch(`${apiBase}/api/search/products?${params}`)
        if (!response.ok) throw new Error(await response.text())
        setProducts(await response.json() as PagedProducts)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Search failed. Check the API connection and try again.')
    } finally {
      setBusy(false)
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    void runSearch()
  }

  const chips: [keyof Filters, string, string | number | null][] = [
    ['category', 'Category', filters.category], ['color', 'Color', filters.color],
    ['size', 'Size', filters.size], ['minPrice', 'From PKR', filters.minPrice],
    ['maxPrice', 'Up to PKR', filters.maxPrice], ['keyword', 'Keyword', filters.keyword],
    ['sort', 'Sort', filters.sort],
  ]

  return (
    <main className="store-shell">
      <section className="hero">
        <p className="eyebrow">THE NEW SEASON, FOUND</p>
        <h1>Find your<br /><em>everyday</em> edit.</h1>
        <p className="intro">Search the Z SQUARE collection in your own words.</p>
        <form onSubmit={submit} className="search-form">
          <label className="sr-only" htmlFor="search-query">Search products</label>
          <input id="search-query" value={query} maxLength={200} onChange={e => setQuery(e.target.value)} placeholder="Try “black formal kurta under 5000”" />
          <button className="search-button" disabled={busy || (aiSearch && !query.trim())}>{busy ? 'Searching…' : 'Search'} <span>↗</span></button>
        </form>
      </section>

      <section className="filters-panel" aria-label="Product filters">
        <div className="filter-heading"><h2>Refine your search</h2><button type="button" className="text-button" onClick={() => setFilters(emptyFilters)}>Clear all</button></div>
        <div className="filter-grid">
          <label>Category<input value={filters.category ?? ''} onChange={e => updateFilter('category', e.target.value || null)} placeholder="e.g. Dresses" /></label>
          <label>Color<input value={filters.color ?? ''} onChange={e => updateFilter('color', e.target.value || null)} placeholder="e.g. Black" /></label>
          <label>Size<input value={filters.size ?? ''} onChange={e => updateFilter('size', e.target.value || null)} placeholder="e.g. M" /></label>
          <label>Keyword or fabric<input value={filters.keyword ?? ''} onChange={e => updateFilter('keyword', e.target.value || null)} placeholder="e.g. lawn" /></label>
          <label>Min price<input type="number" min="0" value={filters.minPrice ?? ''} onChange={e => updateFilter('minPrice', e.target.value ? Number(e.target.value) : null)} placeholder="PKR" /></label>
          <label>Max price<input type="number" min="0" value={filters.maxPrice ?? ''} onChange={e => updateFilter('maxPrice', e.target.value ? Number(e.target.value) : null)} placeholder="PKR" /></label>
          <label>Sort<select value={filters.sort ?? 'relevance'} onChange={e => updateFilter('sort', e.target.value || null)}><option value="relevance">Relevance</option><option value="priceAsc">Price: low to high</option><option value="priceDesc">Price: high to low</option><option value="newest">Newest</option></select></label>
        </div>
      </section>

      {chips.some(([, , value]) => value !== null) && <section className="chips" aria-label="Active filters">
        {chips.map(([key, label, value]) => value !== null && <button className="chip" key={key} onClick={() => updateFilter(key, null)} type="button">{label}: {value}<span aria-hidden="true"> ×</span><span className="sr-only">Remove {label}</span></button>)}
      </section>}

      {fallback && <p className="notice">AI interpretation was unavailable, so we searched the words you entered.</p>}
      {error && <p className="error" role="alert">{error}</p>}
      {products && <section className="results">
        <div className="results-heading"><div><p className="eyebrow">YOUR EDIT</p><h2>{products.totalCount} pieces to explore</h2></div><button className="outline-button" type="button" onClick={() => { setAiSearch(false); void runSearch(false, false) }}>Search with these filters</button></div>
        {products.items.length === 0 ? <p className="empty">No products match these filters yet.</p> : <div className="product-grid">
          {products.items.map(product => <article className="product-card" key={product.productId}>
            <div className="product-image">{product.primaryImageUrl ? <img src={`${apiBase}${product.primaryImageUrl}`} alt={product.name} /> : <span>Kh</span>}</div>
            <div className="product-meta"><span>{product.category}</span><span>{product.productCode}</span></div>
            <h3>{product.name}</h3><p className="price">PKR {product.price.toLocaleString('en-PK')}</p>
          </article>)}
        </div>}
      </section>}
      <footer>SHOP EASE <span>·</span> Made for the way you wear it.</footer>
    </main>
  )
}
