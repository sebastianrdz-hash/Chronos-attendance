import { api, type SolicitudEmpleado } from '@/api/cliente'
import type { Departamento, Empleado, Rol, Turno } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { CampoFormulario } from '@/componentes/CampoFormulario'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { SelectSimple } from '@/componentes/SelectSimple'
import { aplicarErroresDelServidor } from '@/lib/formulario'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

const esquema = z.object({
  numeroEmpleado: z
    .string()
    .trim()
    .min(3, 'El número de empleado debe tener al menos 3 caracteres.')
    .max(20),
  nombres: z.string().trim().min(2, 'El nombre es obligatorio.').max(80),
  apellidoPaterno: z.string().trim().min(2, 'El apellido paterno es obligatorio.').max(80),
  apellidoMaterno: z.string().trim().max(80).optional(),
  correoCorporativo: z.string().trim().email('El correo no tiene un formato válido.').max(160),
  puesto: z.string().trim().max(100).optional(),
  fechaIngreso: z.string().min(1, 'Indica la fecha de ingreso.'),
  departamentoId: z.string().uuid('Selecciona un departamento.'),
  turnoId: z.string().optional(),
  rol: z.enum(['Admin', 'Supervisor', 'Empleado']),
})

type Valores = z.infer<typeof esquema>

const CAMPOS = [
  'numeroEmpleado',
  'nombres',
  'apellidoPaterno',
  'apellidoMaterno',
  'correoCorporativo',
  'puesto',
  'fechaIngreso',
  'departamentoId',
  'sedeId',
  'turnoId',
  'rol',
] as const

const SIN_TURNO = 'sin-turno'

export function FormularioEmpleado({
  abierto,
  empleado,
  departamentos,
  turnos,
  onCerrar,
  onGuardado,
  onAlta,
}: {
  abierto: boolean
  empleado: Empleado | null
  departamentos: Departamento[]
  turnos: Turno[]
  onCerrar: () => void
  onGuardado: () => void
  onAlta: (empleado: Empleado, contrasenaTemporal: string) => void
}) {
  const { usuario } = useAuth()
  const esAdmin = usuario?.rol === 'Admin'
  const [avisoGeneral, setAvisoGeneral] = useState<string | null>(null)

  // Un supervisor solo da de alta dentro de su departamento y solo como empleado raso.
  const disponibles = esAdmin
    ? departamentos
    : departamentos.filter((depto) => depto.id === usuario?.departamentoId)

  const rolesDisponibles: Rol[] = esAdmin ? ['Empleado', 'Supervisor', 'Admin'] : ['Empleado']

  const {
    register,
    handleSubmit,
    reset,
    setError,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({ resolver: zodResolver(esquema) })

  useEffect(() => {
    if (!abierto) return

    setAvisoGeneral(null)
    reset({
      numeroEmpleado: empleado?.numeroEmpleado ?? '',
      nombres: empleado?.nombres ?? '',
      apellidoPaterno: empleado?.apellidoPaterno ?? '',
      apellidoMaterno: empleado?.apellidoMaterno ?? '',
      correoCorporativo: empleado?.correoCorporativo ?? '',
      puesto: empleado?.puesto ?? '',
      fechaIngreso: empleado?.fechaIngreso ?? new Date().toISOString().slice(0, 10),
      departamentoId: empleado?.departamentoId ?? disponibles[0]?.id ?? '',
      turnoId: empleado?.turnoId ?? SIN_TURNO,
      rol: empleado?.rol ?? 'Empleado',
    })
    // disponibles se recalcula en cada render; solo interesa reaccionar a la apertura.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [abierto, empleado, reset])

  const departamentoId = watch('departamentoId')
  const turnoId = watch('turnoId')
  const rol = watch('rol')

  // La sede no se pide: viene determinada por el departamento, y la API rechaza
  // cualquier combinación cruzada.
  const sedeDelDepartamento = departamentos.find((depto) => depto.id === departamentoId)

  const enviar = handleSubmit(async (valores) => {
    setAvisoGeneral(null)

    if (!sedeDelDepartamento) {
      setAvisoGeneral('Selecciona un departamento válido.')
      return
    }

    const cuerpo: SolicitudEmpleado = {
      ...valores,
      numeroEmpleado: valores.numeroEmpleado.toUpperCase(),
      apellidoMaterno: valores.apellidoMaterno?.trim() || null,
      puesto: valores.puesto?.trim() || null,
      turnoId: valores.turnoId === SIN_TURNO ? null : valores.turnoId,
      sedeId: sedeDelDepartamento.sedeId,
      activo: empleado?.activo ?? true,
    }

    try {
      if (empleado) {
        await api.empleados.actualizar(empleado.id, cuerpo)
        toast.success(`${cuerpo.nombres} ${cuerpo.apellidoPaterno} actualizado.`)
        onGuardado()
        onCerrar()
      } else {
        const alta = await api.empleados.crearConCuenta(cuerpo)
        onGuardado()
        onCerrar()
        onAlta(alta.empleado, alta.contrasenaTemporal)
      }
    } catch (fallo) {
      setAvisoGeneral(aplicarErroresDelServidor(fallo, setError, CAMPOS))
    }
  })

  return (
    <Dialog open={abierto} onOpenChange={(valor) => !valor && onCerrar()}>
      <DialogContent className="max-h-[92svh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{empleado ? 'Editar empleado' : 'Alta de empleado'}</DialogTitle>
          <DialogDescription>
            {empleado
              ? 'Los cambios de correo también actualizan la cuenta de acceso.'
              : 'Se creará también su cuenta con una contraseña temporal que deberá cambiar al entrar.'}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={enviar} className="space-y-4" noValidate>
          {avisoGeneral && (
            <Alert variant="destructive">
              <AlertDescription>{avisoGeneral}</AlertDescription>
            </Alert>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <CampoFormulario
              id="numeroEmpleado"
              etiqueta="Número de empleado"
              error={errors.numeroEmpleado?.message}
            >
              <Input
                id="numeroEmpleado"
                {...register('numeroEmpleado')}
                placeholder="EMP-0016"
                className="uppercase"
              />
            </CampoFormulario>
            <CampoFormulario id="puesto" etiqueta="Puesto" error={errors.puesto?.message}>
              <Input id="puesto" {...register('puesto')} placeholder="Desarrolladora backend" />
            </CampoFormulario>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <CampoFormulario id="nombres" etiqueta="Nombres" error={errors.nombres?.message}>
              <Input id="nombres" {...register('nombres')} />
            </CampoFormulario>
            <CampoFormulario
              id="apellidoPaterno"
              etiqueta="Apellido paterno"
              error={errors.apellidoPaterno?.message}
            >
              <Input id="apellidoPaterno" {...register('apellidoPaterno')} />
            </CampoFormulario>
            <CampoFormulario
              id="apellidoMaterno"
              etiqueta="Apellido materno"
              error={errors.apellidoMaterno?.message}
            >
              <Input id="apellidoMaterno" {...register('apellidoMaterno')} />
            </CampoFormulario>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <CampoFormulario
              id="correoCorporativo"
              etiqueta="Correo corporativo"
              error={errors.correoCorporativo?.message}
              ayuda="Es también el usuario con el que inicia sesión."
            >
              <Input id="correoCorporativo" type="email" {...register('correoCorporativo')} />
            </CampoFormulario>
            <CampoFormulario
              id="fechaIngreso"
              etiqueta="Fecha de ingreso"
              error={errors.fechaIngreso?.message}
            >
              <Input id="fechaIngreso" type="date" {...register('fechaIngreso')} />
            </CampoFormulario>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <CampoFormulario
              id="departamentoId"
              etiqueta="Departamento"
              error={errors.departamentoId?.message}
              ayuda={sedeDelDepartamento ? `Sede: ${sedeDelDepartamento.sedeNombre}` : undefined}
            >
              <SelectSimple
                id="departamentoId"
                valor={departamentoId ?? ''}
                placeholder="Selecciona"
                opciones={disponibles.map((depto) => ({ valor: depto.id, etiqueta: depto.nombre }))}
                onCambio={(valor) => setValue('departamentoId', valor, { shouldValidate: true })}
              />
            </CampoFormulario>

            <CampoFormulario id="turnoId" etiqueta="Turno" error={errors.turnoId?.message}>
              <SelectSimple
                id="turnoId"
                valor={turnoId ?? SIN_TURNO}
                opciones={[
                  { valor: SIN_TURNO, etiqueta: 'Sin turno asignado' },
                  ...turnos.map((turno) => ({ valor: turno.id, etiqueta: turno.nombre })),
                ]}
                onCambio={(valor) => setValue('turnoId', valor)}
              />
            </CampoFormulario>

            <CampoFormulario
              id="rol"
              etiqueta="Rol"
              error={errors.rol?.message}
              ayuda={esAdmin ? undefined : 'Solo un administrador otorga roles elevados.'}
            >
              <SelectSimple
                id="rol"
                valor={rol ?? 'Empleado'}
                disabled={!esAdmin}
                opciones={rolesDisponibles.map((opcion) => ({ valor: opcion, etiqueta: opcion }))}
                onCambio={(valor) => setValue('rol', valor as Rol)}
              />
            </CampoFormulario>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              {empleado ? 'Guardar cambios' : 'Dar de alta'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
