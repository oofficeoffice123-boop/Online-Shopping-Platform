import { useEffect, type ReactNode } from 'react'

export function Modal({
  title,
  eyebrow,
  onClose,
  children,
  backdropClassName = 'admin-modal-backdrop',
  dialogClassName = 'admin-modal',
  headerClassName = 'admin-panel-heading',
  closeClassName = 'admin-icon-button',
  closeLabel = 'Close dialog',
  closeDisabled = false,
}: {
  title: string
  eyebrow?: string
  onClose: () => void
  children: ReactNode
  backdropClassName?: string
  dialogClassName?: string
  headerClassName?: string
  closeClassName?: string
  closeLabel?: string
  closeDisabled?: boolean
}) {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !closeDisabled) onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [closeDisabled, onClose])

  return <div className={backdropClassName} role="presentation" onMouseDown={event => { if (event.target === event.currentTarget && !closeDisabled) onClose() }}>
    <section className={dialogClassName} role="dialog" aria-modal="true" aria-label={title}>
      <header className={headerClassName}><div>{eyebrow && <span className="admin-kicker">{eyebrow}</span>}<h2>{title}</h2></div><button className={closeClassName} aria-label={closeLabel} type="button" disabled={closeDisabled} onClick={onClose}>×</button></header>
      {children}
    </section>
  </div>
}
