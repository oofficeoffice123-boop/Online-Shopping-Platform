import type { AuthUser } from './AuthContext'

export function roleHome(role: AuthUser['role'] | undefined) {
  if (role === 'Customer') return '/products'
  if (role === 'Manager') return '/manage/dashboard'
  if (role === 'Admin') return '/admin/users'
  return '/'
}
