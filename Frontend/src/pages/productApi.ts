import { api } from '../auth/apiClient'

export type ProductCard = {
  id: number
  productId: number
  productCode: string
  name: string
  categoryName: string
  category: string
  price: number
  primaryImageUrl: string | null
  secondaryImageUrl: string | null
  availableColors: string[]
  inStock: boolean
  lowStock: boolean
  averageRating: number | null
  reviewCount: number
}

export type ProductPage = {
  items: ProductCard[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type BrowseCategory = {
  categoryId: number
  parentCategoryId: number | null
  name: string
  productCount: number
}

export type BrowseFilters = {
  colors: string[]
  sizes: string[]
  minPrice: number | null
  maxPrice: number | null
}

export type ProductDetails = {
  id: number
  productCode: string
  name: string
  description: string | null
  categoryName: string
  price: number
  images: { id: number; url: string; isPrimary: boolean; sortOrder: number }[]
  variants: { variantId: number; size: string; color: string; price: number; inStock: boolean; lowStock: boolean }[]
  averageRating: number | null
  reviewCount: number
}

export type ProductReview = { reviewId: number; reviewerName: string; rating: number; comment: string | null; createdAt: string }
export type ReviewSummary = { averageRating: number | null; totalCount: number; countsByStar: Record<string, number> }
export type ReviewPage = { items: ProductReview[]; page: number; pageSize: number; totalCount: number; totalPages: number; summary: ReviewSummary }
export type ReviewEligibility = { canReview: boolean; hasDeliveredPurchase: boolean; existingReview: { reviewId: number; rating: number; comment: string | null; createdAt: string; isHidden: boolean } | null }

export const assetUrl = (path: string | null) => path ? new URL(path, api.defaults.baseURL).toString() : null

export async function getCategories(signal?: AbortSignal) {
  return (await api.get<BrowseCategory[]>('/api/categories', { signal })).data
}

export async function getBrowseFilters(signal?: AbortSignal) {
  return (await api.get<BrowseFilters>('/api/products/filters', { signal })).data
}

export async function getProducts(params: URLSearchParams, signal?: AbortSignal) {
  return (await api.get<ProductPage>(`/api/products?${params.toString()}`, { signal })).data
}

export async function getProduct(id: string, signal?: AbortSignal) {
  return (await api.get<ProductDetails>(`/api/products/${id}`, { signal })).data
}

export async function getReviews(productId: number, page = 1) {
  return (await api.get<ReviewPage>(`/api/products/${productId}/reviews`, { params: { page, pageSize: 5 } })).data
}

export function formatPrice(price: number) {
  return `PKR ${price.toLocaleString('en-PK')}`
}

export function colorValue(name: string) {
  const normalized = name.toLowerCase()
  if (normalized.includes('multi')) return 'linear-gradient(135deg, #b64f67 0 33%, #daa94e 33% 66%, #4f7770 66%)'
  if (normalized.includes('off-white') || normalized.includes('white') || normalized.includes('ivory')) return '#eee8db'
  if (normalized.includes('green')) return normalized.includes('light') ? '#9eb89b' : '#426755'
  if (normalized.includes('blue')) return normalized.includes('light') ? '#a6c0d2' : '#425f7f'
  if (normalized.includes('pink')) return '#d99daa'
  if (normalized.includes('red') || normalized.includes('maroon')) return '#8c3948'
  if (normalized.includes('yellow') || normalized.includes('gold')) return '#d4ae55'
  if (normalized.includes('brown') || normalized.includes('camel')) return '#9b7453'
  if (normalized.includes('black')) return '#282622'
  if (normalized.includes('grey') || normalized.includes('gray')) return '#8c8b88'
  if (normalized.includes('purple')) return '#75577f'
  if (normalized.includes('orange')) return '#d27e49'
  return '#b3a38d'
}
