import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import ProductCard from './ProductCard'
import { getCategories, getProducts, type BrowseCategory, type ProductPage } from './productApi'

export default function HomePage() {
  const [categories, setCategories] = useState<BrowseCategory[]>([])
  const [arrivals, setArrivals] = useState<ProductPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    const params = new URLSearchParams({ sort: 'newest', page: '1', pageSize: '8' })
    Promise.all([getCategories(controller.signal), getProducts(params, controller.signal)])
      .then(([categoryList, products]) => {
        setCategories(categoryList.filter(category => category.parentCategoryId === null))
        setArrivals(products)
      })
      .catch(() => { if (!controller.signal.aborted) setError('Could not load the collection. Check that the API is running.') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [])

  return <main className="browse-shell">
    <section className="browse-hero">
      <div className="browse-hero-copy">
      <p className="eyebrow">Z SQUARE · SUMMER COLLECTION</p>
        <h1>Wear the<br /><em>everyday</em> beautifully.</h1>
        <p>Color, craft, and clothes made to move with you.</p>
        <Link className="browse-cta" to="/products">Explore the collection <span>↗</span></Link>
      </div>
      <div className="browse-hero-stamp"><span>ROOTED IN<br />COLOR</span><i>✳</i></div>
    </section>

    <section className="browse-section">
      <div className="browse-section-heading"><div><p className="eyebrow">FIND YOUR FORM</p><h2>Shop by category</h2></div><Link to="/products">All products ↗</Link></div>
      {error ? <p className="browse-error" role="alert">{error}</p> : <div className="category-tiles">
        {(loading ? Array.from({ length: 6 }, (_, i) => ({ categoryId: i, name: '', productCount: 0 })) : categories).map((category, index) => loading
          ? <div className="category-tile skeleton" key={index} />
          : <Link className={`category-tile category-tile-${index % 6}`} key={category.categoryId} to={`/products?categoryId=${category.categoryId}`}>
            <span className="category-tile-count">{String(category.productCount).padStart(2, '0')} pieces</span>
            <strong>{category.name}</strong><span className="category-tile-arrow">↗</span>
          </Link>)}
      </div>}
    </section>

    <section className="browse-section arrivals-section">
      <div className="browse-section-heading"><div><p className="eyebrow">JUST LANDED</p><h2>New arrivals</h2></div><Link to="/products?sort=newest">View all ↗</Link></div>
      {loading ? <div className="browse-product-grid">{Array.from({ length: 4 }, (_, index) => <div className="browse-skeleton" key={index}><i /><b /><b /></div>)}</div>
        : arrivals?.items.length ? <div className="browse-product-grid">{arrivals.items.map(product => <ProductCard key={product.id} product={product} />)}</div>
          : <p className="browse-empty">No arrivals to show.</p>}
    </section>
  </main>
}
