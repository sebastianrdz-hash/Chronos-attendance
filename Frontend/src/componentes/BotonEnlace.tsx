import { Button } from '@/components/ui/button'
import type { ComponentProps } from 'react'
import { Link } from 'react-router-dom'

/**
 * Un enlace con aspecto de botón. El Button de Base UI asume que el elemento sustituto es
 * un `<button>` nativo y avisa por consola cuando no lo es, así que hay que desactivar esa
 * expectativa: aquí lo que se renderiza es el `<a>` del enrutador.
 */
export function BotonEnlace({
  a,
  children,
  ...props
}: { a: string } & Omit<ComponentProps<typeof Button>, 'render' | 'nativeButton'>) {
  return (
    <Button {...props} nativeButton={false} render={<Link to={a} />}>
      {children}
    </Button>
  )
}
