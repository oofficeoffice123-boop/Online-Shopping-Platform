import type { ReactNode } from 'react'

export function TableFrame({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <div className={className}>{children}</div>
}

export function SelectionCheckbox({
  checked,
  label,
  onChange,
}: {
  checked: boolean
  label: string
  onChange: (checked: boolean) => void
}) {
  return <input type="checkbox" aria-label={label} checked={checked} onChange={event => onChange(event.target.checked)} />
}
