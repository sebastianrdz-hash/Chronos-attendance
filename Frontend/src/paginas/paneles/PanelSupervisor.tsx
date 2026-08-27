import { api } from '@/api/cliente'
import type { Empleado, PerfilUsuario } from '@/api/tipos'
import { BotonEnlace } from '@/componentes/BotonEnlace'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TarjetaIndicador } from '@/componentes/TarjetaIndicador'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Info, ShieldCheck, UserMinus, Users } from 'lucide-react'
import { useEffect, useState } from 'react'

interface Resumen {
  equipo: Empleado[]
  bajas: number
  totalOrganizacion: number
}

export function PanelSupervisor({ usuario }: { usuario: PerfilUsuario }) {
  const [resumen, setResumen] = useState<Resumen | null>(null)
  const [fallo, setFallo] = useState<string | null>(null)

  const departamentoId = usuario.departamentoId

  useEffect(() => {
    if (!departamentoId) return

    Promise.all([
      api.empleados.listar({ departamentoId, activo: true, tamano: 100 }),
      api.empleados.listar({ departamentoId, activo: false, tamano: 1 }),
      api.empleados.listar({ tamano: 1 }),
    ])
      .then(([equipo, bajas, organizacion]) =>
        setResumen({
          equipo: equipo.elementos,
          bajas: bajas.total,
          totalOrganizacion: organizacion.total,
        }),
      )
      .catch((error: unknown) =>
        setFallo(error instanceof Error ? error.message : 'No se pudo cargar tu equipo.'),
      )
  }, [departamentoId])

  const error = departamentoId
    ? fallo
    : 'Tu cuenta no tiene un departamento asignado, así que no puedes administrar plantilla.'

  const cargando = resumen === null && error === null
  const conContrasenaTemporal = resumen?.equipo.filter((e) => e.debeCambiarContrasena) ?? []

  return (
    <>
      <EncabezadoPagina
        titulo={`Hola, ${usuario.nombre.split(' ')[0]}`}
        descripcion={`Supervisas ${usuario.departamento ?? 'tu departamento'} en ${usuario.sede ?? 'tu sede'}.`}
      />

      {error && (
        <Alert variant="destructive" className="mb-6">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-3">
        <TarjetaIndicador
          etiqueta="Tu equipo"
          valor={resumen?.equipo.length ?? 0}
          detalle="Empleados activos a tu cargo"
          icono={Users}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Bajas del departamento"
          valor={resumen?.bajas ?? 0}
          icono={UserMinus}
          cargando={cargando}
        />
        <TarjetaIndicador
          etiqueta="Visibles en total"
          valor={resumen?.totalOrganizacion ?? 0}
          detalle="Lectura global de la organización"
          icono={ShieldCheck}
          cargando={cargando}
        />
      </div>

      <Alert className="mt-6">
        <Info className="size-4" />
        <AlertDescription>
          Puedes consultar a toda la organización, pero solo editar, dar de alta o dar de baja
          dentro de {usuario.departamento ?? 'tu departamento'}. El servidor rechaza lo demás
          aunque la interfaz lo permitiera.
        </AlertDescription>
      </Alert>

      {conContrasenaTemporal.length > 0 && (
        <Alert className="mt-4">
          <AlertDescription>
            {conContrasenaTemporal.length} persona
            {conContrasenaTemporal.length === 1 ? '' : 's'} de tu equipo todavía no cambia su
            contraseña temporal: {conContrasenaTemporal.map((e) => e.nombres).join(', ')}.
          </AlertDescription>
        </Alert>
      )}

      <Card className="mt-6">
        <CardHeader className="flex-row items-start justify-between space-y-0">
          <div>
            <CardTitle>Plantilla a tu cargo</CardTitle>
            <CardDescription>{usuario.departamento}</CardDescription>
          </div>
          <BotonEnlace a="/empleados" variant="outline" size="sm">
            Administrar
          </BotonEnlace>
        </CardHeader>
        <CardContent>
          {cargando && <Skeleton className="h-32 w-full" />}

          {resumen?.equipo.length === 0 && (
            <p className="py-6 text-center text-sm text-muted-foreground">
              Todavía no hay nadie asignado a tu departamento.
            </p>
          )}

          <ul className="divide-y">
            {resumen?.equipo.map((empleado) => (
              <li key={empleado.id} className="flex items-center justify-between gap-4 py-3">
                <div>
                  <p className="text-sm font-medium">{empleado.nombreCompleto}</p>
                  <p className="text-xs text-muted-foreground">
                    {empleado.puesto ?? 'Sin puesto'} · {empleado.numeroEmpleado}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {empleado.debeCambiarContrasena && (
                    <Badge variant="outline" className="text-amber-600">
                      Contraseña temporal
                    </Badge>
                  )}
                  <Badge variant="secondary">{empleado.turnoNombre ?? 'Sin turno'}</Badge>
                </div>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </>
  )
}
