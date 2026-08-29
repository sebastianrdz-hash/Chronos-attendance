import { api } from '@/api/cliente'
import type { ResumenAsistencia } from '@/api/tipos'
import { BotonEnlace } from '@/componentes/BotonEnlace'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useEffect, useState } from 'react'

/**
 * El día en cuatro números y dos enlaces. Comparte origen con la pantalla de asistencia
 * para que el tablero y el reporte no puedan discrepar sobre cuántas faltas hubo.
 */
export function ResumenDeHoy({ sedeId }: { sedeId?: string | null }) {
  const [resumen, setResumen] = useState<ResumenAsistencia | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.asistencia
      .delDia({ sedeId: sedeId ?? undefined })
      .then((datos) => setResumen(datos.resumen))
      .catch((fallo: unknown) =>
        setError(fallo instanceof Error ? fallo.message : 'No se pudo cargar la asistencia.'),
      )
  }, [sedeId])

  const cargando = resumen === null && error === null

  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between space-y-0">
        <div>
          <CardTitle>Asistencia de hoy</CardTitle>
          <CardDescription>
            {sedeId ? 'En tu sede' : 'En toda la organización'}
          </CardDescription>
        </div>
        <div className="flex gap-2">
          <BotonEnlace a="/asistencia" variant="outline" size="sm">
            Ver detalle
          </BotonEnlace>
          <BotonEnlace a="/revision" variant="outline" size="sm">
            Revisar
          </BotonEnlace>
        </div>
      </CardHeader>
      <CardContent>
        {error && <p className="text-sm text-destructive">{error}</p>}
        {cargando && <Skeleton className="h-16 w-full" />}

        {resumen && (
          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Dato etiqueta="Presentes" valor={`${resumen.presentes} de ${resumen.plantilla}`} />
            <Dato etiqueta="Faltas" valor={resumen.faltas} />
            <Dato etiqueta="Retardos" valor={resumen.retardos} />
            <Dato etiqueta="Por revisar" valor={resumen.pendientesDeRevision} />
          </dl>
        )}
      </CardContent>
    </Card>
  )
}

function Dato({ etiqueta, valor }: { etiqueta: string; valor: number | string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{etiqueta}</dt>
      <dd className="text-xl font-semibold tabular-nums">{valor}</dd>
    </div>
  )
}
