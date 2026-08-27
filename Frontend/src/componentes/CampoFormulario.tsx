import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'
import type { ReactNode } from 'react'

/**
 * Etiqueta, control y mensaje de error. El mensaje es el mismo tanto si lo produjo zod
 * en el navegador como si vino del servidor, así que el usuario no percibe la diferencia.
 */
export function CampoFormulario({
  id,
  etiqueta,
  error,
  ayuda,
  className,
  children,
}: {
  id: string
  etiqueta: string
  error?: string
  ayuda?: string
  className?: string
  children: ReactNode
}) {
  return (
    <div className={cn('space-y-1.5', className)}>
      <Label htmlFor={id}>{etiqueta}</Label>
      {children}
      {error ? (
        <p className="text-xs font-medium text-destructive" role="alert">
          {error}
        </p>
      ) : (
        ayuda && <p className="text-xs text-muted-foreground">{ayuda}</p>
      )}
    </div>
  )
}
