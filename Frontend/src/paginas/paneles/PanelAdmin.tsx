import { api } from '@/api/cliente'
import type { Departamento, Sede, Turno } from '@/api/tipos'
import { BotonEnlace } from '@/componentes/BotonEnlace'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TarjetaIndicador } from '@/componentes/TarjetaIndicador'
import { TarjetaPolitica } from '@/componentes/TarjetaPolitica'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { aHoraCorta } from '@/lib/formulario'
import { Building2, CalendarClock, Moon, Network, UserMinus, Users } from 'lucide-react'
import { useEffect, useState } from 'react'
import { ResumenDeHoy } from './ResumenDeHoy'

interface Resumen {
  empleadosActivos: number
  empleadosBaja: number
  sedes: Sede[]
  departamentos: Departamento[]
  turnos: Turno[]
}

export function PanelAdmin({ nombre }: { nombre: string }) {
  const [resumen, setResumen] = useState<Resumen | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    // Solo se necesitan los totales, así que se pide una página de tamaño 1 en las
    // consultas de conteo y la lista completa donde sí se van a mostrar los elementos.
    Promise.all([
      api.empleados.listar({ activo: true, tamano: 1 }),
      api.empleados.listar({ activo: false, tamano: 1 }),
      api.sedes.listar({ tamano: 100 }),
      api.departamentos.listar({ tamano: 100 }),
      api.turnos.listar({ tamano: 100 }),
    ])
      .then(([activos, bajas, sedes, departamentos, turnos]) =>
        setResumen({
          empleadosActivos: activos.total,
          empleadosBaja: bajas.total,
          sedes: sedes.elementos,
          departamentos: departamentos.elementos,
          turnos: turnos.elementos,
        }),
      )
      .catch((fallo: unknown) =>
        setError(fallo instanceof Error ? fallo.message : 'No se pudo cargar el resumen.'),
      )
  }, [])

  const cargando = resumen === null && error === null

  return (
    <>
      <EncabezadoPagina
        titulo={`Hola, ${nombre}`}
        descripcion="Estado general de la organización. Desde aquí administras toda la configuración."
      />

      {error && <p className="mb-6 text-sm text-destructive">{error}</p>}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <TarjetaIndicador
          etiqueta="Empleados activos"
          valor={resumen?.empleadosActivos ?? 0}
          icono={Users}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Dados de baja"
          valor={resumen?.empleadosBaja ?? 0}
          detalle="Conservan su historial de checadas"
          icono={UserMinus}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Sedes"
          valor={resumen?.sedes.length ?? 0}
          icono={Building2}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Departamentos"
          valor={resumen?.departamentos.length ?? 0}
          icono={Network}
          cargando={cargando}
        />
      </div>

      <div className="mt-6">
        <ResumenDeHoy />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <div className="space-y-6">
          <Card>
            <CardHeader className="flex-row items-start justify-between space-y-0">
              <div>
                <CardTitle>Plantilla por sede</CardTitle>
                <CardDescription>Distribución de la organización.</CardDescription>
              </div>
              <BotonEnlace a="/sedes" variant="outline" size="sm">
                Administrar
              </BotonEnlace>
            </CardHeader>
            <CardContent className="space-y-4">
              {cargando && <Skeleton className="h-24 w-full" />}
              {resumen?.sedes.map((sede) => {
                const total = resumen.sedes.reduce((suma, s) => suma + s.totalEmpleados, 0)
                const porcentaje = total === 0 ? 0 : (sede.totalEmpleados / total) * 100

                return (
                  <div key={sede.id} className="space-y-1.5">
                    <div className="flex items-center justify-between gap-3 text-sm">
                      <span className="font-medium">{sede.nombre}</span>
                      <span className="tabular-nums text-muted-foreground">
                        {sede.totalEmpleados} empleados
                      </span>
                    </div>
                    <div className="h-2 overflow-hidden rounded-full bg-muted">
                      <div
                        className="h-full rounded-full bg-primary"
                        style={{ width: `${porcentaje}%` }}
                      />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {sede.totalDepartamentos} departamentos · {sede.zonaHoraria}
                    </p>
                  </div>
                )
              })}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex-row items-start justify-between space-y-0">
              <div>
                <CardTitle>Turnos configurados</CardTitle>
                <CardDescription>Horarios contra los que se califica cada jornada.</CardDescription>
              </div>
              <BotonEnlace a="/turnos" variant="outline" size="sm">
                Administrar
              </BotonEnlace>
            </CardHeader>
            <CardContent className="space-y-3">
              {cargando && <Skeleton className="h-20 w-full" />}
              {resumen?.turnos.map((turno) => (
                <div
                  key={turno.id}
                  className="flex items-center justify-between gap-4 rounded-lg border p-3"
                >
                  <div className="flex items-center gap-2">
                    <CalendarClock className="size-4 text-muted-foreground" />
                    <div>
                      <p className="text-sm font-medium">{turno.nombre}</p>
                      <p className="font-mono text-xs text-muted-foreground">
                        {aHoraCorta(turno.horaEntrada)} – {aHoraCorta(turno.horaSalida)} ·{' '}
                        {turno.horasProgramadas} h
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {turno.cruzaMedianoche && (
                      <Badge variant="outline" className="gap-1">
                        <Moon className="size-3" />
                        Nocturno
                      </Badge>
                    )}
                    <Badge variant="secondary">{turno.totalEmpleados}</Badge>
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        <TarjetaPolitica />
      </div>
    </>
  )
}
