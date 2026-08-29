import { api, ErrorApi } from '@/api/cliente'
import type { Asistencia, EstadoAsistencia, Sede } from '@/api/tipos'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { SelectSimple } from '@/componentes/SelectSimple'
import { TarjetaIndicador } from '@/componentes/TarjetaIndicador'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { CalendarX2, Clock, TriangleAlert, UserCheck, Users } from 'lucide-react'
import { useEffect, useState } from 'react'

const variantePorEstado: Record<
  EstadoAsistencia,
  'default' | 'secondary' | 'destructive' | 'outline'
> = {
  Completa: 'default',
  Retardo: 'secondary',
  SalidaAnticipada: 'secondary',
  JornadaIncompleta: 'outline',
  Falta: 'destructive',
  Descanso: 'outline',
}

const hoy = () => new Date().toISOString().slice(0, 10)

export function AsistenciaDelDia() {
  const [fecha, setFecha] = useState(hoy)
  const [sedeId, setSedeId] = useState<string | null>(null)
  const [sedes, setSedes] = useState<Sede[]>([])
  const [datos, setDatos] = useState<Asistencia | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(true)

  useEffect(() => {
    api.sedes
      .listar({ tamano: 100, activo: true })
      .then((pagina) => setSedes(pagina.elementos))
      .catch(() => setSedes([]))
  }, [])

  useEffect(() => {
    let cancelado = false
    setCargando(true)

    api.asistencia
      .delDia({ fecha, sedeId: sedeId ?? undefined })
      .then((resultado) => {
        if (cancelado) return
        setDatos(resultado)
        setError(null)
      })
      .catch((fallo: ErrorApi) => {
        if (!cancelado) setError(fallo.message)
      })
      .finally(() => {
        if (!cancelado) setCargando(false)
      })

    return () => {
      cancelado = true
    }
  }, [fecha, sedeId])

  const resumen = datos?.resumen

  return (
    <div>
      <EncabezadoPagina
        titulo="Asistencia del día"
        descripcion="Quién llegó, quién no y cuántas horas lleva acumuladas la jornada."
      />

      <div className="mb-6 flex flex-wrap items-end gap-4">
        <div className="space-y-1.5">
          <Label htmlFor="fecha">Fecha</Label>
          <Input
            id="fecha"
            type="date"
            value={fecha}
            max={hoy()}
            onChange={(e) => setFecha(e.target.value || hoy())}
            className="w-44"
          />
        </div>

        <div className="space-y-1.5">
          <Label>Sede</Label>
          <SelectSimple
            valor={sedeId ?? 'todas'}
            etiquetaAccesible="Filtrar por sede"
            className="w-60"
            opciones={[
              { valor: 'todas', etiqueta: 'Todas las sedes' },
              ...sedes.map((sede) => ({ valor: sede.id, etiqueta: sede.nombre })),
            ]}
            onCambio={(valor) => setSedeId(valor === 'todas' ? null : valor)}
          />
        </div>
      </div>

      {error && (
        <Alert variant="destructive" className="mb-6">
          <TriangleAlert />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <TarjetaIndicador
          etiqueta="Plantilla esperada"
          valor={resumen?.plantilla ?? 0}
          detalle="Sin contar a quien está de descanso"
          icono={Users}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Presentes"
          valor={resumen?.presentes ?? 0}
          detalle={`${resumen?.horasTrabajadas ?? 0} h acumuladas`}
          icono={UserCheck}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Faltas"
          valor={resumen?.faltas ?? 0}
          detalle={`${resumen?.jornadasIncompletas ?? 0} jornada(s) sin cerrar`}
          icono={CalendarX2}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Retardos"
          valor={resumen?.retardos ?? 0}
          detalle={`${resumen?.pendientesDeRevision ?? 0} por revisar`}
          icono={Clock}
          cargando={cargando}
        />
      </div>

      <div className="overflow-hidden rounded-lg border bg-background">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Empleado</TableHead>
              <TableHead className="hidden md:table-cell">Departamento</TableHead>
              <TableHead className="hidden lg:table-cell">Turno</TableHead>
              <TableHead>Entrada</TableHead>
              <TableHead>Salida</TableHead>
              <TableHead className="text-right">Horas</TableHead>
              <TableHead>Estado</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {cargando &&
              Array.from({ length: 6 }).map((_, indice) => (
                <TableRow key={`esqueleto-${indice}`}>
                  {Array.from({ length: 7 }).map((__, columna) => (
                    <TableCell key={columna}>
                      <Skeleton className="h-4 w-full max-w-28" />
                    </TableCell>
                  ))}
                </TableRow>
              ))}

            {!cargando && datos?.empleados.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="py-10 text-center text-muted-foreground">
                  No hay plantilla activa para esos filtros.
                </TableCell>
              </TableRow>
            )}

            {!cargando &&
              datos?.empleados.map((fila) => (
                <TableRow key={fila.empleadoId}>
                  <TableCell>
                    <p className="font-medium">{fila.nombreCompleto}</p>
                    <p className="text-xs text-muted-foreground">{fila.numeroEmpleado}</p>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">{fila.departamentoNombre}</TableCell>
                  <TableCell className="hidden lg:table-cell">{fila.turnoNombre ?? '—'}</TableCell>
                  <TableCell className="tabular-nums">
                    {aHora(fila.entrada)}
                    {fila.minutosRetardo > 0 && (
                      <span className="ml-1.5 text-xs text-amber-600">
                        +{fila.minutosRetardo} min
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="tabular-nums">{aHora(fila.salida)}</TableCell>
                  <TableCell className="text-right tabular-nums">
                    {fila.horasTrabajadas.toFixed(2)}
                    {fila.horasExtra > 0 && (
                      <span className="ml-1.5 text-xs text-emerald-600">
                        +{fila.horasExtra.toFixed(2)}
                      </span>
                    )}
                  </TableCell>
                  <TableCell>
                    <span className="flex flex-wrap items-center gap-1.5">
                      <Badge variant={variantePorEstado[fila.estado]}>{fila.estadoNombre}</Badge>
                      {fila.requiereRevision && (
                        <Badge variant="outline" className="text-amber-600">
                          Por revisar
                        </Badge>
                      )}
                    </span>
                  </TableCell>
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

function aHora(momento: string | null): string {
  if (!momento) return '—'

  return new Date(momento).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' })
}
