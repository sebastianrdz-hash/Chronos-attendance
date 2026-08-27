import { cn } from '@/lib/utils'

export function LogotipoChronos({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      fill="none"
      aria-hidden="true"
      className={cn('size-8', className)}
    >
      <circle cx="16" cy="16" r="14" className="stroke-current" strokeWidth="2" opacity="0.35" />
      <path
        d="M16 7v9l6 3.5"
        className="stroke-current"
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="16" cy="16" r="2" className="fill-current" />
    </svg>
  )
}

export function MarcaChronos({ className }: { className?: string }) {
  return (
    <span className={cn('flex items-center gap-2.5', className)}>
      <LogotipoChronos className="size-7 text-indigo-600" />
      <span className="text-base font-semibold tracking-tight">Chronos</span>
    </span>
  )
}
