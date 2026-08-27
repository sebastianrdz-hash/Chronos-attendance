import { api } from '@/api/cliente'
import type { DiaSemana, MiPerfil, PerfilUsuario } from '@/api/tipos'
import { BotonEnlace } from '@/componentes/BotonEnlace'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TarjetaPolitica } from '@/componentes/TarjetaPolitica'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { aHoraCorta, formatearFecha } from '@/lib/formulario'
import { CalendarClock, Moon } from 'lucide-react'
import { useEffect, useState } from 'react'

const DIAS: { valor: DiaSemana; corto: string }[] = [
  { valor: 'Lunes', corto: 'Lu' },
  { valor: 'Martes', corto: 'Ma' },
  { valor: 'Miercoles', corto: 'Mi' },
  { valor: 'Jueves', corto: 'Ju' },
  { valor: 'Viernes', corto: 'Vi' },
  { valor: 'Sabado', corto: 'Sá' },
  { valor: 'Domingo', corto: 'Do' },
]

export function PanelEmpleado({ usuario }: { usuario: PerfilUsuario }) {
  const [perfil, setPerfil] = useState<MiPerfil | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api
      .miPerfil()
      .then(setPerfil)
      .catch((fallo: unknown) =>
        setError(fallo instanceof Error ? fallo.message : 'No se pudo cargar tu expediente.'),
      )
  }, [])

  const turno = perfil?.turno

  return (
    <>
      <EncabezadoPagina
        titulo={`Hola, ${usuario.nombre.split(' ')[0]}`}
        descripcion="Tu expediente y el horario contra el que se califica tu asistencia."
        acciones={
          <BotonEnlace a="/perfil" variant="outline">
            Ver mi perfil
          </BotonEnlace>
        }
      />

      {error && (
        <Alert variant="destructive" className="mb-6">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-6 lg:grid-cols-[1fr_1fr]">
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Tu expediente</CardTitle>
              <CardDescription>Datos resueltos por la API desde PostgreSQL.</CardDescription>
            </CardHeader>
            <CardContent>
              {!perfil && !error ? (
                <Skeleton className="h-40 w-full" />
              ) : (
                <dl className="space-y-3 text-sm">
                  {(
                    [
                      ['Número de empleado', perfil?.empleado.numeroEmpleado],
                      ['Puesto', perfil?.empleado.puesto],
                      ['Departamento', perfil?.empleado.departamentoNombre],
                      ['Sede', perfil?.empleado.sedeNombre],
                      ['Fecha de ingreso', formatearFecha(perfil?.empleado.fechaIngreso)],
                      ['Correo', perfil?.correo],
                    ] as const
                  ).map(([etiqueta, valor]) => (
                    <div key={etiqueta} className="flex items-start justify-between gap-4">
                      <dt className="text-muted-foreground">{etiqueta}</dt>
                      <dd className="text-right font-medium">{valor ?? '—'}</dd>
                    </div>
                  ))}
                </dl>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CalendarClock className="size-5 text-muted-foreground" />
                Tu turno
              </CardTitle>
              <CardDescription>
                {turno ? turno.nombre : 'Todavía no tienes un turno asignado.'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!perfil && !error && <Skeleton className="h-24 w-full" />}

              {turno && (
                <div className="space-y-4">
                  <div className="flex items-baseline gap-3">
                    <span className="font-mono text-3xl font-semibold tabular-nums">
                      {aHoraCorta(turno.horaEntrada)} – {aHoraCorta(turno.horaSalida)}
                    </span>
                    {turno.cruzaMedianoche && (
                      <Badge variant="outline" className="gap-1">
                        <Moon className="size-3" />
                        Cruza medianoche
                      </Badge>
                    )}
                  </div>

                  <div className="flex gap-1.5">
                    {DIAS.map(({ valor, corto }) => (
                      <span
                        key={valor}
                        title={valor}
                        className={
                          turno.diasLaborales.includes(valor)
                            ? 'flex size-8 items-center justify-center rounded-md bg-primary/10 text-xs font-medium text-primary'
                            : 'flex size-8 items-center justify-center rounded-md bg-muted text-xs text-muted-foreground/50'
                        }
                      >
                        {corto}
                      </span>
                    ))}
                  </div>

                  <dl className="grid grid-cols-3 gap-4 border-t pt-4 text-sm">
                    <div>
                      <dt className="text-xs text-muted-foreground">Jornada efectiva</dt>
                      <dd className="font-medium">{turno.horasProgramadas} h</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">Tolerancia</dt>
                      <dd className="font-medium">{turno.toleranciaMinutos} min</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">Descanso</dt>
                      <dd className="font-medium">{turno.minutosDescanso} min</dd>
                    </div>
                  </dl>

                  <p className="text-xs text-muted-foreground">
                    Llegar dentro de los {turno.toleranciaMinutos} minutos de tolerancia no cuenta
                    como retardo.
                  </p>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <TarjetaPolitica />
      </div>
    </>
  )
}
