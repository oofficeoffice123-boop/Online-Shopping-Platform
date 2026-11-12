import type { ReactNode } from 'react'

export function Pagination({
  page,
  totalPages,
  onPageChange,
  loading = false,
  className = 'admin-pagination',
  previousClassName = 'admin-secondary',
  nextClassName = 'admin-secondary',
  summary,
}: {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
  loading?: boolean
  className?: string
  previousClassName?: string
  nextClassName?: string
  summary?: ReactNode
}) {
  if (totalPages <= 1) return null
  return <footer className={className}>
    {summary}
    <div><button type="button" className={previousClassName} disabled={page <= 1 || loading} onClick={() => onPageChange(page - 1)}>Previous</button>
      <span>Page {page} of {totalPages}</span>
      <button type="button" className={nextClassName} disabled={page >= totalPages || loading} onClick={() => onPageChange(page + 1)}>Next</button></div>
  </footer>
}
