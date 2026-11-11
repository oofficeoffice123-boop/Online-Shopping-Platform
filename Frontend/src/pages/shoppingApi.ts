import { api } from '../auth/apiClient'

export type CartLine = {
  cartItemId: number
  variantId: number
  productId: number
  productName: string
  size: string
  color: string
  imageUrl: string | null
  unitPrice: number
  quantity: number
  lineTotal: number
  inStock: boolean
  variantActive: boolean
  stockSufficient: boolean
}

export type Cart = { items: CartLine[]; subtotal: number }

export type Address = {
  addressId: number
  line1: string
  line2: string | null
  city: string
  postalCode: string
  country: string
  isDefault: boolean
}

export type AddressInput = Omit<Address, 'addressId'>

export type OrderSummary = {
  orderId: number
  status: string
  orderDate: string
  totalAmount: number
  itemCount: number
  firstItemImageUrl: string | null
}

export type OrderPage = { items: OrderSummary[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export type OrderDetails = Pick<OrderSummary, 'orderId' | 'status' | 'orderDate' | 'totalAmount'> & {
  deliveredAt: string | null
  shipName: string
  shipLine1: string
  shipLine2: string | null
  shipCity: string
  shipPostalCode: string
  shipCountry: string
  items: { variantId: number; productId: number; productName: string; size: string; color: string; imageUrl: string | null; quantity: number; unitPrice: number; lineTotal: number }[]
}

export const money = (value: number) => `PKR ${value.toLocaleString('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export async function getCart() { return (await api.get<Cart>('/api/cart')).data }
export async function addCartItem(variantId: number, quantity = 1) {
  return (await api.post<Cart>('/api/cart/items', { variantId, quantity })).data
}
export async function setCartQuantity(cartItemId: number, quantity: number) {
  return (await api.put<Cart>(`/api/cart/items/${cartItemId}`, { quantity })).data
}
export async function removeCartItem(cartItemId: number) { await api.delete(`/api/cart/items/${cartItemId}`) }
export async function getAddresses() { return (await api.get<Address[]>('/api/addresses')).data }
export async function addAddress(request: AddressInput) { return (await api.post<Address>('/api/addresses', request)).data }
export async function checkout(addressId: number, paymentMethod: 'CashOnDelivery' | 'MockCard', idempotencyKey: string) {
  return (await api.post<OrderSummary>('/api/orders/checkout', { addressId, paymentMethod }, { headers: { 'Idempotency-Key': idempotencyKey } })).data
}
export async function getOrders(page = 1) { return (await api.get<OrderPage>(`/api/orders?page=${page}&pageSize=12`)).data }
export async function getOrder(id: string) { return (await api.get<OrderDetails>(`/api/orders/${id}`)).data }
export async function cancelOrder(id: number) { return (await api.post<OrderSummary>(`/api/orders/${id}/cancel`)).data }

export function notifyCartUpdated() { window.dispatchEvent(new Event('z-square-cart-updated')) }
