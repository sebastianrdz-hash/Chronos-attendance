import { api, type SolicitudTurno } from '@/api/cliente'
import type { DiaSemana, ParametrosLista, Turno } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { CampoFormulario } from '@/componentes/CampoFormulario'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TablaDatos, type Columna } from '@/componentes/TablaDatos'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useListaPaginada } from '@/hooks/useListaPaginada'
import { aHoraCorta, aHoraLarga, aplicarErroresDelServidor } from '@/lib/formulario'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, Moon, Pencil, Plus, Power } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

const DIAS: { valor: DiaSemana; corto: string }[] = [
  { valor: 'Lunes', corto: 'Lu' },
  { valor: 'Martes', corto: 'Ma' },
  { valor: 'Miercoles', corto: 'Mi' },
  { valor: 'Jueves', corto: 'Ju' },
  { valor: 'Viernes', corto: 'Vi' },
  { valor: 'Sabado', corto: 'Sá' },
  { valor: 'Domingo', corto: 'Do' },
]

const esquema = z
  .object({
    nombre: z.string().trim().min(3, 'El nombre debe tener al menos 3 caracteres.').max(80),
    horaEntrada: z.string().min(1, 'Indica la hora de entrada.'),
    horaSalida: z.string().min(1, 'Indica la hora de salida.'),
    toleranciaMinutos: z.number().int().min(0).max(120),
    minutosDescanso: z.number().int().min(0).max(240),
    diasLaborales: z.array(z.string()).min(1, 'Selecciona al menos un día laboral.'),
    activo: z.boolean(),
  })
  .refine((valores) => valores.horaEntrada !== valores.horaSalida, {
    path: ['horaSalida'],
    message: 'La salida no puede ser igual a la entrada.',
  })

type Valores = z.infer<typeof esquema>

const CAMPOS = [
  'nombre',
  'horaEntrada',
  'horaSalida',
  'toleranciaMinutos',
  'minutosDescanso',
  'diasLaborales',
  'activo',
] as const

function Formulario({
  abierto,
  turno,
  onCerrar,
  onGuardado,
}: {
  abierto: boolean
  turno: Turno | null
  onCerrar: () => void
  onGuardado: () => void
}) {
  const [avisoGeneral, setAvisoGeneral] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({
    resolver: zodResolver(esquema),
    defaultValues: { toleranciaMinutos: 10, minutosDescanso: 60, diasLaborales: [], activo: true },
  })

  useEffect(() => {
    if (!abierto) return

    setAvisoGeneral(null)
    reset({
      nombre: turno?.nombre ?? '',
      horaEntrada: aHoraCorta(turno?.horaEntrada ?? '09:00:00'),
      horaSalida: aHoraCorta(turno?.horaSalida ?? '18:00:00'),
      toleranciaMinutos: turno?.toleranciaMinutos ?? 10,
      minutosDescanso: turno?.minutosDescanso ?? 60,
      diasLaborales: turno?.diasLaborales ?? ['Lunes', 'Martes', 'Miercoles', 'Jueves', 'Viernes'],
      activo: turno?.activo ?? true,
    })
  }, [abierto, turno, reset])

  const dias = watch('diasLaborales')
  const activo = watch('activo')
  const entrada = watch('horaEntrada')
  const salida = watch('horaSalida')
  const cruzaMedianoche = Boolean(entrada && salida && salida <= entrada)

  const alternarDia = (dia: DiaSemana) => {
    setValue(
      'diasLaborales',
      dias.includes(dia) ? dias.filter((d) => d !== dia) : [...dias, dia],
      { shouldValidate: true },
    )
  }

  const enviar = handleSubmit(async (valores) => {
    setAvisoGeneral(null)

    const cuerpo: SolicitudTurno = {
      ...valores,
      horaEntrada: aHoraLarga(valores.horaEntrada),
      horaSalida: aHoraLarga(valores.horaSalida),
    }

    try {
      if (turno) {
        await api.turnos.actualizar(turno.id, cuerpo)
        toast.success(`Turno "${cuerpo.nombre}" actualizado.`)
      } else {
        await api.turnos.crear(cuerpo)
        toast.success(`Turno "${cuerpo.nombre}" registrado.`)
      }

      onGuardado()
      onCerrar()
    } catch (fallo) {
      setAvisoGeneral(aplicarErroresDelServidor(fallo, setError, CAMPOS))
    }
  })

  return (
    <Dialog open={abierto} onOpenChange={(valor) => !valor && onCerrar()}>
      <DialogContent className="max-h-[92svh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{turno ? 'Editar turno' : 'Nuevo turno'}</DialogTitle>
          <DialogDescription>
            Si la salida es anterior o igual a la entrada, el turno se interpreta como nocturno y
            termina al día siguiente.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={enviar} className="space-y-4" noValidate>
          {avisoGeneral && (
            <Alert variant="destructive">
              <AlertDescription>{avisoGeneral}</AlertDescription>
            </Alert>
          )}

          <CampoFormulario id="nombre" etiqueta="Nombre" error={errors.nombre?.message}>
            <Input id="nombre" {...register('nombre')} placeholder="Matutino" />
          </CampoFormulario>

          <div className="grid gap-4 sm:grid-cols-2">
            <CampoFormulario
              id="horaEntrada"
              etiqueta="Hora de entrada"
              error={errors.horaEntrada?.message}
            >
              <Input id="horaEntrada" type="time" {...register('horaEntrada')} />
            </CampoFormulario>
            <CampoFormulario
              id="horaSalida"
              etiqueta="Hora de salida"
              error={errors.horaSalida?.message}
              ayuda={cruzaMedianoche ? 'Este turno cruza medianoche.' : undefined}
            >
              <Input id="horaSalida" type="time" {...register('horaSalida')} />
            </CampoFormulario>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <CampoFormulario
              id="toleranciaMinutos"
              etiqueta="Tolerancia (min)"
              error={errors.toleranciaMinutos?.message}
              ayuda="Margen antes de contar retardo."
            >
              <Input
                id="toleranciaMinutos"
                type="number"
                {...register('toleranciaMinutos', { valueAsNumber: true })}
              />
            </CampoFormulario>
            <CampoFormulario
              id="minutosDescanso"
              etiqueta="Descanso (min)"
              error={errors.minutosDescanso?.message}
              ayuda="Se descuenta de la jornada efectiva."
            >
              <Input
                id="minutosDescanso"
                type="number"
                {...register('minutosDescanso', { valueAsNumber: true })}
              />
            </CampoFormulario>
          </div>

          <div className="space-y-1.5">
            <Label>Días laborales</Label>
            <div className="flex flex-wrap gap-2">
              {DIAS.map(({ valor, corto }) => (
                <button
                  key={valor}
                  type="button"
                  onClick={() => alternarDia(valor)}
                  aria-pressed={dias.includes(valor)}
                  className={
                    dias.includes(valor)
                      ? 'size-10 rounded-md border border-primary bg-primary text-sm font-medium text-primary-foreground'
                      : 'size-10 rounded-md border text-sm font-medium text-muted-foreground hover:bg-muted'
                  }
                >
                  {corto}
                </button>
              ))}
            </div>
            {errors.diasLaborales && (
              <p className="text-xs font-medium text-destructive" role="alert">
                {errors.diasLaborales.message}
              </p>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Checkbox
              id="activo"
              checked={activo}
              onCheckedChange={(valor) => setValue('activo', valor === true)}
            />
            <Label htmlFor="activo" className="font-normal">
              Turno activo
            </Label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              {turno ? 'Guardar cambios' : 'Registrar turno'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function Turnos() {
  const { usuario } = useAuth()
  const puedeEditar = usuario?.rol === 'Admin'

  const consultar = useCallback((parametros: ParametrosLista) => api.turnos.listar(parametros), [])
  const lista = useListaPaginada<Turno>(consultar)

  const [enEdicion, setEnEdicion] = useState<Turno | null>(null)
  const [abierto, setAbierto] = useState(false)

  const abrir = (turno: Turno | null) => {
    setEnEdicion(turno)
    setAbierto(true)
  }

  const desactivar = async (turno: Turno) => {
    try {
      await api.turnos.desactivar(turno.id)
      toast.success(`Turno "${turno.nombre}" dado de baja.`)
      lista.recargar()
    } catch (fallo) {
      toast.error(fallo instanceof Error ? fallo.message : 'No se pudo dar de baja.')
    }
  }

  const columnas: Columna<Turno>[] = [
    {
      clave: 'nombre',
      encabezado: 'Turno',
      ordenPor: 'nombre',
      celda: (turno) => (
        <div className="flex items-center gap-2">
          <span className="font-medium">{turno.nombre}</span>
          {turno.cruzaMedianoche && (
            <Badge variant="outline" className="gap-1">
              <Moon className="size-3" />
              Nocturno
            </Badge>
          )}
        </div>
      ),
    },
    {
      clave: 'horario',
      encabezado: 'Horario',
      ordenPor: 'horaEntrada',
      celda: (turno) => (
        <span className="font-mono text-sm">
          {aHoraCorta(turno.horaEntrada)} – {aHoraCorta(turno.horaSalida)}
        </span>
      ),
    },
    {
      clave: 'jornada',
      encabezado: 'Jornada efectiva',
      celda: (turno) => <span className="text-sm">{turno.horasProgramadas} h</span>,
    },
    {
      clave: 'dias',
      encabezado: 'Días',
      celda: (turno) => (
        <div className="flex gap-1">
          {DIAS.map(({ valor, corto }) => (
            <span
              key={valor}
              className={
                turno.diasLaborales.includes(valor)
                  ? 'flex size-6 items-center justify-center rounded bg-primary/10 text-[10px] font-medium text-primary'
                  : 'flex size-6 items-center justify-center rounded bg-muted text-[10px] text-muted-foreground/50'
              }
              title={valor}
            >
              {corto}
            </span>
          ))}
        </div>
      ),
    },
    {
      clave: 'tolerancia',
      encabezado: 'Tolerancia',
      celda: (turno) => <span className="text-sm">{turno.toleranciaMinutos} min</span>,
    },
    {
      clave: 'empleados',
      encabezado: 'Asignados',
      celda: (turno) => <span className="text-sm">{turno.totalEmpleados}</span>,
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      ordenPor: 'activo',
      celda: (turno) => (
        <Badge variant={turno.activo ? 'secondary' : 'outline'}>
          {turno.activo ? 'Activo' : 'Baja'}
        </Badge>
      ),
    },
  ]

  if (puedeEditar) {
    columnas.push({
      clave: 'acciones',
      encabezado: '',
      className: 'text-right',
      celda: (turno) => (
        <div className="flex justify-end gap-1">
          <Button variant="ghost" size="icon" onClick={() => abrir(turno)} aria-label="Editar">
            <Pencil className="size-4" />
          </Button>
          {turno.activo && (
            <Button
              variant="ghost"
              size="icon"
              onClick={() => desactivar(turno)}
              aria-label="Dar de baja"
            >
              <Power className="size-4" />
            </Button>
          )}
        </div>
      ),
    })
  }

  return (
    <>
      <EncabezadoPagina
        titulo="Turnos"
        descripcion="Horarios contra los que se califican los fichajes: retardos, salidas anticipadas y horas extra."
      />

      <TablaDatos
        columnas={columnas}
        filas={lista.elementos}
        claveDeFila={(turno) => turno.id}
        cargando={lista.cargando}
        error={lista.error}
        total={lista.total}
        pagina={lista.parametros.pagina ?? 1}
        totalPaginas={lista.totalPaginas}
        hayPaginaAnterior={lista.hayPaginaAnterior}
        hayPaginaSiguiente={lista.hayPaginaSiguiente}
        ordenarPor={lista.parametros.ordenarPor}
        descendente={lista.parametros.descendente}
        onOrdenar={lista.ordenar}
        onPagina={lista.irAPagina}
        onBuscar={(texto) => lista.filtrar({ buscar: texto })}
        buscarPlaceholder="Buscar por nombre…"
        filtroActivo={lista.parametros.activo}
        onFiltroActivo={(activo) => lista.filtrar({ activo })}
        acciones={
          puedeEditar && (
            <Button onClick={() => abrir(null)}>
              <Plus className="size-4" />
              Nuevo turno
            </Button>
          )
        }
      />

      <Formulario
        abierto={abierto}
        turno={enEdicion}
        onCerrar={() => setAbierto(false)}
        onGuardado={lista.recargar}
      />
    </>
  )
}
