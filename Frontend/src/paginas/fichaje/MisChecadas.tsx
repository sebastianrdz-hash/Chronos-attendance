import { api, ErrorApi } from '@/api/cliente'
import type { AsistenciaDelDia, Checada } from '@/api/tipos'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ChevronDown, QrCode, TriangleAlert } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { etiquetaEstado } from './ResumenChecada'

export function MisChecadas() {
  const [checadas, setChecadas] = useState<Checada[] | null>(null)
  const [jornadas, setJornadas] = useState<AsistenciaDelDia[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [abierta, setAbierta] = useState<string | null>(null)

  useEffect(() => {
    api.checadas
      .mias({ tamano: 50 })
      .then((pagina) => setChecadas(pagina.elementos))
      .catch((fallo: ErrorApi) => setError(fallo.message))

    // Las horas se calculan en el servidor. Sumarlas aquí a partir de las checadas daría
    // una segunda versión de las reglas de jornada, condenada a discrepar de la nómina.
    api.asistencia
      .mia()
      .then(setJornadas)
      .catch(() => setJornadas([]))
  }, [])

  return (
    <div className="mx-auto max-w-3xl">
      <EncabezadoPagina
        titulo="Mis checadas"
        descripcion="Tu historial de fichajes y qué señales respaldó cada uno."
        acciones={
          <Button render={<Link to="/fichar" />}>
            <QrCode />
            Fichar
          </Button>
        }
      />

      {error && (
        <Alert variant="destructive" className="mb-6">
          <TriangleAlert />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {jornadas && jornadas.length > 0 && <ResumenQuincena jornadas={jornadas} />}

      {checadas === null && !error && (
        <div className="space-y-3">
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-20 w-full" />
        </div>
      )}

      {checadas?.length === 0 && (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            Todavía no tienes fichajes registrados.
          </CardContent>
        </Card>
      )}

      <div className="space-y-3">
        {checadas?.map((checada) => (
          <Card key={checada.id}>
            <CardContent className="py-4">
              <button
                type="button"
                className="flex w-full items-center justify-between text-left"
                onClick={() => setAbierta(abierta === checada.id ? null : checada.id)}
                aria-expanded={abierta === checada.id}
              >
                <div>
                  <p className="font-medium">
                    {checada.tipo === 'Entrada' ? 'Entrada' : 'Salida'} ·{' '}
                    {new Date(checada.momentoUtc).toLocaleString('es-MX', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    })}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {checada.sedeNombre ?? 'Sin sede'} · {checada.puntajeConfianza} de 100
                  </p>
                </div>

                <span className="flex items-center gap-2">
                  <Badge variant={checada.estado === 'Verificada' ? 'default' : 'secondary'}>
                    {etiquetaEstado[checada.estado]}
                  </Badge>
                  <ChevronDown
                    className={`size-4 transition-transform ${abierta === checada.id ? 'rotate-180' : ''}`}
                  />
                </span>
              </button>

              {abierta === checada.id && (
                <ul className="mt-4 space-y-2 border-t pt-4">
                  {checada.senales.map((senal, indice) => (
                    <li
                      key={`${senal.tipo}-${indice}`}
                      className="flex items-center justify-between text-sm"
                    >
                      <span>{senal.tipoNombre}</span>
                      <Badge variant="outline">
                        {senal.pesoAplicado > 0 ? `+${senal.pesoAplicado}` : senal.pesoAplicado}
                      </Badge>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

function ResumenQuincena({ jornadas }: { jornadas: AsistenciaDelDia[] }) {
  const laborales = jornadas.filter((j) => j.estado !== 'Descanso')

  const horas = laborales.reduce((suma, j) => suma + j.horasTrabajadas, 0)
  const extra = laborales.reduce((suma, j) => suma + j.horasExtra, 0)
  const retardos = laborales.filter((j) => j.minutosRetardo > 0).length
  const faltas = laborales.filter((j) => j.estado === 'Falta').length

  return (
    <Card className="mb-6">
      <CardHeader>
        <CardTitle>Tus últimos 14 días</CardTitle>
        <CardDescription>
          Calculado contra tu turno, descontando el descanso programado.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Dato etiqueta="Horas trabajadas" valor={horas.toFixed(1)} />
          <Dato etiqueta="Horas extra" valor={extra.toFixed(1)} />
          <Dato etiqueta="Retardos" valor={retardos} />
          <Dato etiqueta="Faltas" valor={faltas} />
        </dl>
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
