import { api } from '@/api/cliente'
import type { MiPerfil as MiPerfilDto } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { CampoFormulario } from '@/componentes/CampoFormulario'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { aHoraCorta, aplicarErroresDelServidor, formatearFecha } from '@/lib/formulario'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, LogOut, Moon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { CredencialesWebAuthn } from './perfil/CredencialesWebAuthn'

const esquema = z
  .object({
    contrasenaActual: z.string().min(1, 'Escribe tu contraseña actual.'),
    contrasenaNueva: z.string().min(10, 'La contraseña nueva debe tener al menos 10 caracteres.'),
    confirmacion: z.string().min(1, 'Confirma la contraseña nueva.'),
  })
  .refine((valores) => valores.contrasenaNueva === valores.confirmacion, {
    path: ['confirmacion'],
    message: 'Las contraseñas no coinciden.',
  })
  .refine((valores) => valores.contrasenaActual !== valores.contrasenaNueva, {
    path: ['contrasenaNueva'],
    message: 'La contraseña nueva debe ser distinta de la actual.',
  })

type Valores = z.infer<typeof esquema>

const CAMPOS = ['contrasenaActual', 'contrasenaNueva', 'confirmacion'] as const

export function FormularioContrasena({ alCambiar }: { alCambiar?: () => void }) {
  const [avisoGeneral, setAvisoGeneral] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({ resolver: zodResolver(esquema) })

  const enviar = handleSubmit(async (valores) => {
    setAvisoGeneral(null)

    try {
      await api.cambiarContrasena(valores)
      reset()
      toast.success('Contraseña actualizada.')
      alCambiar?.()
    } catch (fallo) {
      setAvisoGeneral(aplicarErroresDelServidor(fallo, setError, CAMPOS))
    }
  })

  return (
    <form onSubmit={enviar} className="space-y-4" noValidate>
      {avisoGeneral && (
        <Alert variant="destructive">
          <AlertDescription>{avisoGeneral}</AlertDescription>
        </Alert>
      )}

      <CampoFormulario
        id="contrasenaActual"
        etiqueta="Contraseña actual"
        error={errors.contrasenaActual?.message}
      >
        <Input
          id="contrasenaActual"
          type="password"
          autoComplete="current-password"
          {...register('contrasenaActual')}
        />
      </CampoFormulario>

      <CampoFormulario
        id="contrasenaNueva"
        etiqueta="Contraseña nueva"
        error={errors.contrasenaNueva?.message}
        ayuda="Mínimo 10 caracteres, con mayúscula, minúscula, dígito y símbolo."
      >
        <Input
          id="contrasenaNueva"
          type="password"
          autoComplete="new-password"
          {...register('contrasenaNueva')}
        />
      </CampoFormulario>

      <CampoFormulario
        id="confirmacion"
        etiqueta="Confirmar contraseña"
        error={errors.confirmacion?.message}
      >
        <Input
          id="confirmacion"
          type="password"
          autoComplete="new-password"
          {...register('confirmacion')}
        />
      </CampoFormulario>

      <Button type="submit" disabled={isSubmitting} className="w-full sm:w-auto">
        {isSubmitting && <Loader2 className="size-4 animate-spin" />}
        Cambiar contraseña
      </Button>
    </form>
  )
}

export function MiPerfil() {
  const { cerrarSesion } = useAuth()
  const navegar = useNavigate()
  const [perfil, setPerfil] = useState<MiPerfilDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  const salir = () => {
    cerrarSesion()
    navegar('/login', { replace: true })
  }

  useEffect(() => {
    api
      .miPerfil()
      .then(setPerfil)
      .catch((fallo: unknown) =>
        setError(fallo instanceof Error ? fallo.message : 'No se pudo cargar tu expediente.'),
      )
  }, [])

  const empleado = perfil?.empleado
  const turno = perfil?.turno

  return (
    <>
      <EncabezadoPagina
        titulo="Mi perfil"
        descripcion="Tus datos, tu turno asignado y el acceso a tu cuenta."
        acciones={
          <Button variant="outline" onClick={salir}>
            <LogOut className="size-4" />
            Cerrar sesión
          </Button>
        }
      />

      {error && (
        <Alert variant="destructive" className="mb-6">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Datos personales</CardTitle>
            <CardDescription>
              Para corregir algo de esta ficha, habla con tu supervisor o con Recursos Humanos.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {!perfil && !error ? (
              <Skeleton className="h-64 w-full" />
            ) : (
              <>
                <dl className="space-y-3 text-sm">
                  {(
                    [
                      ['Nombre completo', empleado?.nombreCompleto],
                      ['Número de empleado', empleado?.numeroEmpleado],
                      ['Puesto', empleado?.puesto],
                      ['Departamento', empleado?.departamentoNombre],
                      ['Sede', empleado?.sedeNombre],
                      ['Fecha de ingreso', formatearFecha(empleado?.fechaIngreso)],
                      ['Correo corporativo', perfil?.correo],
                    ] as const
                  ).map(([etiqueta, valor]) => (
                    <div key={etiqueta} className="flex items-start justify-between gap-4">
                      <dt className="text-muted-foreground">{etiqueta}</dt>
                      <dd className="text-right font-medium">{valor ?? '—'}</dd>
                    </div>
                  ))}
                </dl>

                <Separator className="my-4" />

                <div className="space-y-3">
                  <h3 className="text-sm font-medium">Turno asignado</h3>
                  {turno ? (
                    <div className="rounded-lg border p-4">
                      <div className="flex items-center justify-between gap-3">
                        <span className="font-medium">{turno.nombre}</span>
                        {turno.cruzaMedianoche && (
                          <Badge variant="outline" className="gap-1">
                            <Moon className="size-3" />
                            Nocturno
                          </Badge>
                        )}
                      </div>
                      <p className="mt-1 font-mono text-sm text-muted-foreground">
                        {aHoraCorta(turno.horaEntrada)} – {aHoraCorta(turno.horaSalida)} ·{' '}
                        {turno.horasProgramadas} h efectivas
                      </p>
                      <p className="mt-2 text-xs text-muted-foreground">
                        {turno.diasLaborales.join(', ')} · {turno.toleranciaMinutos} min de
                        tolerancia
                      </p>
                    </div>
                  ) : (
                    <p className="text-sm text-muted-foreground">
                      Todavía no tienes un turno asignado.
                    </p>
                  )}
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Cambiar contraseña</CardTitle>
            <CardDescription>
              {perfil?.ultimoAccesoUtc
                ? `Último acceso: ${new Date(perfil.ultimoAccesoUtc).toLocaleString('es-MX')}`
                : 'Cambia tu contraseña periódicamente.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <FormularioContrasena />
          </CardContent>
        </Card>

        <div className="lg:col-span-2">
          <CredencialesWebAuthn />
        </div>
      </div>
    </>
  )
}
