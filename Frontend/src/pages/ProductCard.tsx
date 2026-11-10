import { Link } from 'react-router-dom'
import { assetUrl, colorValue, formatPrice, type ProductCard as Product } from './productApi'

export default function ProductCard({ product }: { product: Product }) {
  const primary = assetUrl(product.primaryImageUrl)
  const secondary = assetUrl(product.secondaryImageUrl)

  return <Link className="browse-product-card" to={`/products/${product.id}`}>
    <div className="browse-product-photo">
      {primary ? <img className="browse-image-primary" src={primary} alt={product.name} loading="lazy" /> : <span className="browse-image-placeholder">K</span>}
      {secondary && <img className="browse-image-secondary" src={secondary} alt="" loading="lazy" />}
      {!product.inStock && <span className="stock-ribbon">Sold out</span>}
    </div>
    <div className="browse-product-copy">
      <p className="browse-product-category">{product.categoryName}</p>
      <h3>{product.name}</h3>
      <p className="product-rating" aria-label={product.averageRating == null ? 'No reviews yet' : `Rated ${product.averageRating} out of 5 from ${product.reviewCount} reviews`}>
        {product.averageRating == null ? '☆☆☆☆☆' : `★ ${product.averageRating.toFixed(1)} · ${product.reviewCount} ${product.reviewCount === 1 ? 'review' : 'reviews'}`}
      </p>
      <div className="browse-card-bottom">
        <p className="browse-price">{formatPrice(product.price)}</p>
        <span className="browse-colors" aria-label={`${product.availableColors.length} colors`}>
          {product.availableColors.slice(0, 5).map(color => <i key={color} title={color} style={{ background: colorValue(color) }} />)}
        </span>
      </div>
      {product.lowStock && <small className="low-stock-note">Low stock</small>}
    </div>
  </Link>
}
