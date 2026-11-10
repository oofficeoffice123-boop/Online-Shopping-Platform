import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import axios from 'axios'
import { api } from '../auth/apiClient'
import ProductCard from './ProductCard'
import { colorValue, formatPrice, getBrowseFilters, getCategories, getProducts, type BrowseCategory, type BrowseFilters, type ProductPage } from './productApi'

type SmartResponse = {
  interpretedFilters: { category: string | null; minPrice: number | null; maxPrice: number | null; color: string | null; keyword: string | null; size: string | null; sort: string | null; inStockOnly: boolean; minRating: number | null }
  fallback: boolean
}

function toggleValue(params: URLSearchParams, key: string, value: string, checked: boolean) {
  const values = params.getAll(key).filter(item => item !== value)
  if (checked) values.push(value)
  params.delete(key)
  values.forEach(item => params.append(key, item))
  params.set('page', '1')
}

export default function ProductListPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const queryString = searchParams.toString()
  const params = useMemo(() => new URLSearchParams(queryString), [queryString])
  const [categories, setCategories] = useState<BrowseCategory[]>([])
  const [filterOptions, setFilterOptions] = useState<BrowseFilters | null>(null)
  const [products, setProducts] = useState<ProductPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [query, setQuery] = useState(params.get('q') ?? '')
  const [aiFallback, setAiFallback] = useState(false)
  const [minDraft, setMinDraft] = useState(0)
  const [maxDraft, setMaxDraft] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([getCategories(controller.signal), getBrowseFilters(controller.signal)])
      .then(([categoryList, options]) => {
        setCategories(categoryList)
        setFilterOptions(options)
        setMinDraft(Number(params.get('minPrice') ?? options.minPrice ?? 0))
        setMaxDraft(Number(params.get('maxPrice') ?? options.maxPrice ?? 0))
      })
      .catch(() => { if (!controller.signal.aborted) setError('Could not load product filters.') })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const requestParams = new URLSearchParams(queryString)
    requestParams.delete('smartQuery')
    if (!requestParams.has('page')) requestParams.set('page', '1')
    if (!requestParams.has('pageSize')) requestParams.set('pageSize', '12')
    if (!requestParams.has('sort')) requestParams.set('sort', 'newest')
    setLoading(true)
    setError('')
    getProducts(requestParams, controller.signal)
      .then(setProducts)
      .catch(() => { if (!controller.signal.aborted) setError('Could not load products. Check that the API is running.') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [queryString])

  useEffect(() => {
    setQuery(searchParams.get('smartQuery') ?? searchParams.get('q') ?? '')
    setMinDraft(Number(searchParams.get('minPrice') ?? filterOptions?.minPrice ?? 0))
    setMaxDraft(Number(searchParams.get('maxPrice') ?? filterOptions?.maxPrice ?? 0))
  }, [queryString, filterOptions, searchParams])

  useEffect(() => {
    if (!filterOptions) return
    const timer = window.setTimeout(() => {
      const next = new URLSearchParams(searchParams)
      const currentMin = next.get('minPrice')
      const currentMax = next.get('maxPrice')
      if (minDraft > (filterOptions.minPrice ?? 0)) next.set('minPrice', String(minDraft))
      else next.delete('minPrice')
      if (maxDraft < (filterOptions.maxPrice ?? maxDraft)) next.set('maxPrice', String(maxDraft))
      else next.delete('maxPrice')
      if (currentMin !== next.get('minPrice') || currentMax !== next.get('maxPrice')) {
        next.set('page', '1')
        setSearchParams(next, { replace: true })
      }
    }, 350)
    return () => window.clearTimeout(timer)
  }, [minDraft, maxDraft, filterOptions, searchParams, setSearchParams])

  function updateParam(key: string, value: string | null, replace = false) {
    const next = new URLSearchParams(searchParams)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key !== 'page') next.set('page', '1')
    setSearchParams(next, { replace })
  }

  async function submitSearch(event: FormEvent) {
    event.preventDefault()
    const searchText = query.trim()
    if (!searchText) return

    setLoading(true)
    setError('')
    setAiFallback(false)
    try {
      const { data } = await api.post<SmartResponse>('/api/search/smart', { query: searchText })
      const filters = data.interpretedFilters
      const next = new URLSearchParams()
      const category = categories.find(item => item.name.toLowerCase() === filters.category?.toLowerCase())
      if (category) next.set('categoryId', String(category.categoryId))
      if (filters.minPrice !== null) next.set('minPrice', String(filters.minPrice))
      if (filters.maxPrice !== null) next.set('maxPrice', String(filters.maxPrice))
      if (filters.color) next.append('color', filters.color)
      if (filters.size) next.append('size', filters.size)
      if (filters.keyword) next.set('q', filters.keyword)
      if (filters.sort && ['newest', 'priceAsc', 'priceDesc', 'nameAsc', 'topRated'].includes(filters.sort)) next.set('sort', filters.sort)
      if (filters.inStockOnly) next.set('inStockOnly', 'true')
      if (filters.minRating !== null) next.set('minRating', String(filters.minRating))
      next.set('smartQuery', searchText)
      next.set('page', '1')
      next.set('pageSize', '12')
      setAiFallback(data.fallback)
      setSearchParams(next)
    } catch (searchError) {
      const message = axios.isAxiosError(searchError) ? searchError.response?.data?.detail : undefined
      setError(message ?? 'AI search could not complete. Please try again.')
      setLoading(false)
    }
  }

  const currentCategory = categories.find(category => String(category.categoryId) === params.get('categoryId'))
  const activeChips = [
    ...(currentCategory ? [{ key: 'categoryId', value: currentCategory.name }] : []),
    ...params.getAll('color').map(value => ({ key: 'color', value })),
    ...params.getAll('size').map(value => ({ key: 'size', value })),
    ...(params.get('q') ? [{ key: 'q', value: params.get('q')! }] : []),
    ...(params.get('minPrice') ? [{ key: 'minPrice', value: `From ${formatPrice(Number(params.get('minPrice')))}` }] : []),
    ...(params.get('maxPrice') ? [{ key: 'maxPrice', value: `Up to ${formatPrice(Number(params.get('maxPrice')))}` }] : []),
    ...(params.get('inStockOnly') === 'true' ? [{ key: 'inStockOnly', value: 'In stock' }] : []),
    ...(params.get('minRating') ? [{ key: 'minRating', value: `${params.get('minRating')}+ stars` }] : []),
  ]

  function removeChip(key: string, value: string) {
    const next = new URLSearchParams(searchParams)
    if (key === 'color' || key === 'size') toggleValue(next, key, value, false)
    else next.delete(key)
    next.set('page', '1')
    setSearchParams(next)
  }

  const page = Number(params.get('page') ?? 1)
  const maxPrice = filterOptions?.maxPrice ?? 100000
  const minPrice = filterOptions?.minPrice ?? 0

  return <main className="browse-shell browse-list-shell">
    <section className="list-hero">
      <p className="eyebrow">THE Z SQUARE COLLECTON</p>
      <h1>Find your next <em>favorite.</em></h1>
      <form className="browse-search" onSubmit={event => void submitSearch(event)}>
        <input aria-label="Search the collection" maxLength={200} value={query} onChange={event => setQuery(event.target.value)} placeholder="Describe what you’re looking for" />
        <button className="browse-cta" type="submit">Search ↗</button>
      </form>
      {aiFallback && <p className="browse-notice">AI interpretation was unavailable, so we searched the words you entered.</p>}
    </section>

    <div className="browse-layout">
      <aside className="browse-sidebar" aria-label="Product filters">
        <div className="browse-filter-title"><h2>Filter by</h2><button type="button" onClick={() => { setSearchParams(new URLSearchParams({ page: '1', pageSize: '12', sort: 'newest' })); setQuery('') }}>Clear all</button></div>
        <label className="browse-filter-label">Category<select value={params.get('categoryId') ?? ''} onChange={event => updateParam('categoryId', event.target.value || null)}><option value="">All categories</option>{categories.map(category => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select></label>
        <fieldset className="browse-filter-group"><legend>Price range</legend><div className="price-range-labels"><span>{formatPrice(minDraft)}</span><span>{formatPrice(maxDraft)}</span></div>
          <input aria-label="Minimum price" type="range" min={minPrice} max={maxPrice} step="100" value={Math.min(minDraft, maxDraft)} onChange={event => setMinDraft(Math.min(Number(event.target.value), maxDraft))} />
          <input aria-label="Maximum price" type="range" min={minPrice} max={maxPrice} step="100" value={Math.max(maxDraft, minDraft)} onChange={event => setMaxDraft(Math.max(Number(event.target.value), minDraft))} />
        </fieldset>
        <fieldset className="browse-filter-group"><legend>Color</legend><div className="browse-option-list">{(filterOptions?.colors ?? []).map(color => <label key={color}><input type="checkbox" checked={params.getAll('color').includes(color)} onChange={event => { const next = new URLSearchParams(searchParams); toggleValue(next, 'color', color, event.target.checked); setSearchParams(next) }} /><i style={{ background: colorValue(color) }} />{color}</label>)}</div></fieldset>
        <fieldset className="browse-filter-group"><legend>Size</legend><div className="browse-size-options">{(filterOptions?.sizes ?? []).map(size => <label key={size} className={params.getAll('size').includes(size) ? 'selected' : ''}><input type="checkbox" checked={params.getAll('size').includes(size)} onChange={event => { const next = new URLSearchParams(searchParams); toggleValue(next, 'size', size, event.target.checked); setSearchParams(next) }} />{size}</label>)}</div></fieldset>
        <label className="browse-stock-check"><input type="checkbox" checked={params.get('inStockOnly') === 'true'} onChange={event => updateParam('inStockOnly', event.target.checked ? 'true' : null)} /> In stock only</label>
        <label className="browse-filter-label">Minimum rating<select value={params.get('minRating') ?? ''} onChange={event => updateParam('minRating', event.target.value || null)}><option value="">Any rating</option>{[5,4,3,2,1].map(rating=><option key={rating} value={rating}>{rating}+ stars</option>)}</select></label>
      </aside>

      <section className="browse-results">
        <div className="browse-results-heading"><div><p className="eyebrow">{currentCategory?.name ?? 'ALL PIECES'}</p><h2>{products?.totalCount ?? 0} products</h2></div>
          <label className="browse-sort">Sort by<select value={params.get('sort') ?? 'newest'} onChange={event => updateParam('sort', event.target.value)}><option value="newest">Newest</option><option value="topRated">Top rated</option><option value="priceAsc">Price: low to high</option><option value="priceDesc">Price: high to low</option><option value="nameAsc">Name: A to Z</option></select></label>
        </div>
        {activeChips.length > 0 && <div className="browse-chips">{activeChips.map((chip, index) => <button type="button" key={`${chip.key}-${chip.value}-${index}`} onClick={() => removeChip(chip.key, chip.value)}>{chip.value}<span aria-hidden="true"> ×</span></button>)}</div>}
        {error && <p className="browse-error" role="alert">{error}</p>}
        {loading ? <div className="browse-product-grid">{Array.from({ length: 6 }, (_, index) => <div className="browse-skeleton" key={index}><i /><b /><b /></div>)}</div>
          : products?.items.length ? <div className="browse-product-grid">{products.items.map(product => <ProductCard key={product.id} product={product} />)}</div>
            : <div className="browse-empty"><h3>No pieces found</h3><p>Try removing a filter or changing your search.</p><Link to="/products">Browse all products</Link></div>}
        {products && products.totalPages > 1 && <nav className="browse-pagination" aria-label="Product pages"><button type="button" disabled={page <= 1} onClick={() => updateParam('page', String(page - 1))}>← Previous</button><span>Page {page} of {products.totalPages}</span><button type="button" disabled={page >= products.totalPages} onClick={() => updateParam('page', String(page + 1))}>Next →</button></nav>}
      </section>
    </div>
  </main>
}
