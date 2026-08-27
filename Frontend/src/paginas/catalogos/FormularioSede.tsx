import type { Sede } from '@/api/tipos'
import { api, type SolicitudSede } from '@/api/cliente'
import { CampoFormulario } from '@/componentes/CampoFormulario'
import { Alert, AlertDescription } from '@/components/ui/alert'
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
import { aplicarErroresDelServidor } from '@/lib/formulario'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

const numeroOpcional = z
  .union([z.number(), z.nan()])
  .optional()
  .transform((valor) => (valor === undefined || Number.isNaN(valor) ? null : valor))

const esquema = z.object({
  nombre: z.string().trim().min(3, 'El nombre debe tener al menos 3 caracteres.').max(120),
  codigo: z
    .string()
    .trim()
    .min(2, 'El código debe tener al menos 2 caracteres.')
    .max(20)
    .regex(/^[A-Za-z0-9-]+$/, 'El código solo admite letras, números y guiones.'),
  direccion: z.string().trim().max(250).optional(),
  zonaHoraria: z.string().trim().min(1, 'La zona horaria es obligatoria.'),
  latitud: numeroOpcional,
  longitud: numeroOpcional,
  radioMetros: numeroOpcional,
  activa: z.boolean(),
})

// Los campos numéricos opcionales llegan como NaN cuando el input queda vacío y el esquema
// los transforma a null: la entrada y la salida del formulario no coinciden, y react-hook-form
// necesita ambos tipos para que handleSubmit entregue ya el valor transformado.
type Entrada = z.input<typeof esquema>
type Salida = z.output<typeof esquema>

const CAMPOS = [
  'nombre',
  'codigo',
  'direccion',
  'zonaHoraria',
  'latitud',
  'longitud',
  'radioMetros',
  'activa',
] as const

const ZONAS = [
  'America/Mexico_City',
  'America/Monterrey',
  'America/Tijuana',
  'America/Cancun',
  'America/Hermosillo',
]

export function FormularioSede({
  abierto,
  sede,
  onCerrar,
  onGuardado,
}: {
  abierto: boolean
  sede: Sede | null
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
  } = useForm<Entrada, unknown, Salida>({
    resolver: zodResolver(esquema),
    defaultValues: { zonaHoraria: 'America/Mexico_City', activa: true },
  })

  useEffect(() => {
    if (!abierto) return

    setAvisoGeneral(null)
    reset({
      nombre: sede?.nombre ?? '',
      codigo: sede?.codigo ?? '',
      direccion: sede?.direccion ?? '',
      zonaHoraria: sede?.zonaHoraria ?? 'America/Mexico_City',
      latitud: sede?.latitud ?? undefined,
      longitud: sede?.longitud ?? undefined,
      radioMetros: sede?.radioMetros ?? undefined,
      activa: sede?.activa ?? true,
    })
  }, [abierto, sede, reset])

  const activa = watch('activa')

  const enviar = handleSubmit(async (valores) => {
    setAvisoGeneral(null)

    const cuerpo: SolicitudSede = {
      ...valores,
      direccion: valores.direccion?.trim() || null,
      codigo: valores.codigo.toUpperCase(),
    }

    try {
      if (sede) {
        await api.sedes.actualizar(sede.id, cuerpo)
        toast.success(`Sede "${cuerpo.nombre}" actualizada.`)
      } else {
        await api.sedes.crear(cuerpo)
        toast.success(`Sede "${cuerpo.nombre}" registrada.`)
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
          <DialogTitle>{sede ? 'Editar sede' : 'Nueva sede'}</DialogTitle>
          <DialogDescription>
            La zona horaria decide a qué día laboral pertenece cada fichaje de esta sede.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={enviar} className="space-y-4" noValidate>
          {avisoGeneral && (
            <Alert variant="destructive">
              <AlertDescription>{avisoGeneral}</AlertDescription>
            </Alert>
          )}

          <div className="grid gap-4 sm:grid-cols-3">
            <CampoFormulario
              id="nombre"
              etiqueta="Nombre"
              error={errors.nombre?.message}
              className="sm:col-span-2"
            >
              <Input id="nombre" {...register('nombre')} placeholder="Corporativo Monterrey" />
            </CampoFormulario>

            <CampoFormulario id="codigo" etiqueta="Código" error={errors.codigo?.message}>
              <Input id="codigo" {...register('codigo')} placeholder="MTY-01" className="uppercase" />
            </CampoFormulario>
          </div>

          <CampoFormulario id="direccion" etiqueta="Dirección" error={errors.direccion?.message}>
            <Input id="direccion" {...register('direccion')} placeholder="Av. Constitución 1000" />
          </CampoFormulario>

          <CampoFormulario
            id="zonaHoraria"
            etiqueta="Zona horaria"
            error={errors.zonaHoraria?.message}
            ayuda="Identificador IANA. El servidor rechaza los que no reconoce."
          >
            <Input id="zonaHoraria" list="zonas-horarias" {...register('zonaHoraria')} />
            <datalist id="zonas-horarias">
              {ZONAS.map((zona) => (
                <option key={zona} value={zona} />
              ))}
            </datalist>
          </CampoFormulario>

          <fieldset className="space-y-3 rounded-lg border p-4">
            <legend className="px-1 text-sm font-medium">Geocerca (opcional)</legend>
            <p className="text-xs text-muted-foreground">
              Verificación gruesa por GPS. Se llenan los tres campos o ninguno.
            </p>
            <div className="grid gap-4 sm:grid-cols-3">
              <CampoFormulario id="latitud" etiqueta="Latitud" error={errors.latitud?.message}>
                <Input
                  id="latitud"
                  type="number"
                  step="any"
                  {...register('latitud', { valueAsNumber: true })}
                />
              </CampoFormulario>
              <CampoFormulario id="longitud" etiqueta="Longitud" error={errors.longitud?.message}>
                <Input
                  id="longitud"
                  type="number"
                  step="any"
                  {...register('longitud', { valueAsNumber: true })}
                />
              </CampoFormulario>
              <CampoFormulario
                id="radioMetros"
                etiqueta="Radio (m)"
                error={errors.radioMetros?.message}
              >
                <Input
                  id="radioMetros"
                  type="number"
                  {...register('radioMetros', { valueAsNumber: true })}
                />
              </CampoFormulario>
            </div>
          </fieldset>

          <div className="flex items-center gap-2">
            <Checkbox
              id="activa"
              checked={activa}
              onCheckedChange={(valor) => setValue('activa', valor === true)}
            />
            <Label htmlFor="activa" className="font-normal">
              Sede activa
            </Label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              {sede ? 'Guardar cambios' : 'Registrar sede'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
