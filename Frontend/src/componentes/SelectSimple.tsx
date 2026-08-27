import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cn } from '@/lib/utils'

export interface OpcionSelect {
  valor: string
  etiqueta: string
}

/**
 * Envoltura sobre el Select de Base UI para los casos habituales de este proyecto.
 * Resuelve dos asperezas de la primitiva: `onValueChange` entrega `string | null`, y el
 * disparador muestra el valor crudo (un GUID, aquí) salvo que se le pase `items` con las
 * etiquetas. Ambas cosas se repetirían en cada pantalla si no se centralizaran.
 */
export function SelectSimple({
  id,
  valor,
  opciones,
  onCambio,
  placeholder,
  etiquetaAccesible,
  disabled,
  className,
}: {
  id?: string
  valor: string
  opciones: OpcionSelect[]
  onCambio: (valor: string) => void
  placeholder?: string
  etiquetaAccesible?: string
  disabled?: boolean
  className?: string
}) {
  const items = opciones.map(({ valor: value, etiqueta: label }) => ({ value, label }))

  return (
    <Select
      items={items}
      value={valor}
      onValueChange={(nuevo) => onCambio(typeof nuevo === 'string' ? nuevo : '')}
      disabled={disabled}
    >
      <SelectTrigger id={id} className={cn('w-full', className)} aria-label={etiquetaAccesible}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {opciones.map((opcion) => (
          <SelectItem key={opcion.valor} value={opcion.valor}>
            {opcion.etiqueta}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
