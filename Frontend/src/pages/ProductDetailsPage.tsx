import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { assetUrl, colorValue, formatPrice, getProduct, type ProductDetails } from './productApi'
import { useAuth } from '../auth/AuthContext'
import { addCartItem, notifyCartUpdated } from './shoppingApi'

export default function ProductDetailsPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [product, setProduct] = useState<ProductDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [notFound, setNotFound] = useState(false)
  const [imageIndex, setImageIndex] = useState(0)
  const [selectedColor, setSelectedColor] = useState('')
  const [selectedSize, setSelectedSize] = useState('')
  const [adding, setAdding] = useState(false)
  const [cartError, setCartError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setNotFound(false)
    getProduct(id, controller.signal)
      .then(item => {
        setProduct(item)
        setImageIndex(0)
        const availableVariant = item.variants.find(variant => variant.inStock && (variant.discountPercent ?? 0) > 0)
          ?? item.variants.find(variant => variant.inStock)
          ?? item.variants[0]
        setSelectedColor(availableVariant?.color ?? '')
        setSelectedSize(availableVariant?.size ?? '')
      })
      .catch(() => { if (!controller.signal.aborted) setNotFound(true) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id])

  const colors = useMemo(() => [...new Set(product?.variants.map(variant => variant.color) ?? [])], [product])
  const sizes = useMemo(() => [...new Set(product?.variants.map(variant => variant.size) ?? [])], [product])
  const selectedVariant = product?.variants.find(variant => variant.color === selectedColor && variant.size === selectedSize)
  const currentImage = product?.images[imageIndex]
  const displayedDiscountPercent = selectedVariant ? selectedVariant.discountPercent : product?.discountPercent
  const displayedOriginalPrice = selectedVariant ? selectedVariant.originalPrice : product?.originalPrice
  const selectedSalePrice = selectedVariant?.discountPercent && selectedVariant.originalPrice
    ? Math.round((selectedVariant.originalPrice * (100 - selectedVariant.discountPercent) / 100 + Number.EPSILON) * 100) / 100
    : selectedVariant?.price ?? product?.price

  async function addSelectedVariant() {
    if (!selectedVariant?.inStock) return
    if (!user) {
      navigate('/login', { state: { returnTo: `${location.pathname}${location.search}` } })
      return
    }
    if (user.role !== 'Customer') {
      setCartError('Only customer accounts can add items to a shopping bag.')
      return
    }
    setAdding(true)
    setCartError('')
    try {
      await addCartItem(selectedVariant.variantId)
      notifyCartUpdated()
      navigate('/cart')
    } catch (error) {
      const response = (error as { response?: { data?: { title?: string; detail?: string } } }).response?.data
      setCartError(response?.detail ?? response?.title ?? 'This item could not be added. Refresh the page and try again.')
    } finally {
      setAdding(false)
    }
  }

  if (loading) return <main className="browse-shell"><div className="detail-skeleton"><i /><div><b /><b /><b /><b /></div></div></main>
  if (notFound || !product) return <main className="browse-shell"><div className="browse-empty detail-empty"><h1>Product not found</h1><p>This product may no longer be available.</p><Link to="/products">Back to the collection</Link></div></main>

  return <main className="browse-shell detail-shell">
    <p className="detail-breadcrumb"><Link to="/">Home</Link><span>/</span><Link to="/products">{product.categoryName}</Link><span>/</span>{product.name}</p>
    <div className="detail-layout">
      <section className="detail-gallery" aria-label="Product images">
        <div className="detail-main-image">{currentImage ? <img src={assetUrl(currentImage.url) ?? ''} alt={product.name} /> : <div className="browse-image-placeholder">K</div>}</div>
        {product.images.length > 1 && <div className="detail-thumbnails">{product.images.map((image, index) => <button className={index === imageIndex ? 'active' : ''} key={image.id} type="button" onClick={() => setImageIndex(index)} aria-label={`View product image ${index + 1}`}><img src={assetUrl(image.url) ?? ''} alt="" /></button>)}</div>}
      </section>

      <section className="detail-info">
        <p className="eyebrow">{product.categoryName}</p>
        <h1>{product.name}</h1>
        {displayedDiscountPercent && displayedOriginalPrice
          ? <div className="detail-sale-price"><s>{formatPrice(displayedOriginalPrice)}</s><strong>{formatPrice(selectedSalePrice ?? product.price)}</strong><span>{displayedDiscountPercent}% OFF</span></div>
          : <p className="detail-price">{formatPrice(selectedSalePrice ?? product.price)}</p>}
        <p className="detail-stock">{selectedVariant ? selectedVariant.inStock ? selectedVariant.lowStock ? 'Low stock — order soon' : 'In stock' : 'Out of stock' : 'Select a size and color'}</p>
        {product.description && <p className="detail-description">{product.description}</p>}

        {colors.length > 0 && <fieldset className="detail-select-group"><legend>Color <span>{selectedColor}</span></legend><div className="detail-color-options">{colors.map(color => {
          const hasCombination = product.variants.some(variant => variant.color === color && variant.size === selectedSize && variant.inStock)
          return <button key={color} type="button" className={color === selectedColor ? 'selected' : ''} disabled={!hasCombination} onClick={() => setSelectedColor(color)} aria-label={color} title={color}><i style={{ background: colorValue(color) }} /></button>
        })}</div></fieldset>}

        {sizes.length > 0 && <fieldset className="detail-select-group"><legend>Size <span>{selectedSize}</span></legend><div className="detail-size-options">{sizes.map(size => {
          const variant = product.variants.find(item => item.size === size && item.color === selectedColor)
          return <button key={size} type="button" className={size === selectedSize ? 'selected' : ''} disabled={!variant?.inStock} onClick={() => setSelectedSize(size)}>{size}</button>
        })}</div></fieldset>}

        {cartError && <p className="shopping-error" role="alert">{cartError}</p>}
        <button className="browse-cta detail-cart-button" type="button" disabled={adding || !selectedVariant?.inStock} onClick={() => void addSelectedVariant()}>
          {adding ? 'Adding…' : 'Add to bag'} <span>→</span>
        </button>
      </section>
    </div>
  </main>
}
