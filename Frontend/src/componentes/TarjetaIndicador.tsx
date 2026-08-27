import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import type { LucideIcon } from 'lucide-react'

export function TarjetaIndicador({
  etiqueta,
  valor,
  detalle,
  icono: Icono,
  cargando,
}: {
  etiqueta: string
  valor: number | string
  detalle?: string
  icono: LucideIcon
  cargando?: boolean
}) {
  return (
    <Card>
      <CardContent className="flex items-start justify-between gap-4 pt-6">
        <div className="space-y-1">
          <p className="text-sm text-muted-foreground">{etiqueta}</p>
          {cargando ? (
            <Skeleton className="h-8 w-16" />
          ) : (
            <p className="text-3xl font-semibold tabular-nums">{valor}</p>
          )}
          {detalle && <p className="text-xs text-muted-foreground">{detalle}</p>}
        </div>
        <div className="rounded-lg bg-primary/10 p-2.5 text-primary">
          <Icono className="size-5" />
        </div>
      </CardContent>
    </Card>
  )
}
