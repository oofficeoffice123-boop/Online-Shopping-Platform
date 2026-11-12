import type { ReactNode } from 'react'

export function PageHeading({
  eyebrow,
  title,
  description,
  actions,
  className = '',
}: {
  eyebrow: string
  title: string
  description?: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return <header className={className}>
    <div><p className="admin-kicker">{eyebrow}</p><h1>{title}</h1>{description && <p>{description}</p>}</div>
    {actions}
  </header>
}

export function PanelHeading({
  eyebrow,
  title,
  actions,
  className = 'admin-panel-heading',
}: {
  eyebrow?: string
  title: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return <div className={className}><div>{eyebrow && <span className="admin-kicker">{eyebrow}</span>}<h2>{title}</h2></div>{actions}</div>
}
