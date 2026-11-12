import type { ReactNode } from 'react'

export function Feedback({
  kind = 'error',
  children,
  className = '',
}: {
  kind?: 'error' | 'notice' | 'loading' | 'empty'
  children: ReactNode
  className?: string
}) {
  const role = kind === 'error' ? 'alert' : kind === 'notice' ? 'status' : undefined
  const defaultClassName = kind === 'error' ? 'admin-alert' : kind === 'notice' ? 'admin-notice' : ''
  return <div className={className || defaultClassName} role={role}>{children}</div>
}

