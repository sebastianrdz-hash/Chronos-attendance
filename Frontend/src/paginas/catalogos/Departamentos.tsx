import { api, type SolicitudDepartamento } from '@/api/cliente'
import type { Departamento, ParametrosLista, Sede } from '@/api/tipos'
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
import { SelectSimple } from '@/componentes/SelectSimple'
import { useListaPaginada } from '@/hooks/useListaPaginada'
import { aplicarErroresDelServidor } from '@/lib/formulario'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, Pencil, Plus, Power } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

const esquema = z.object({
  nombre: z.string().trim().min(3, 'El nombre debe tener al menos 3 caracteres.').max(120),
  codigo: z
    .string()
    .trim()
    .min(2, 'El código debe tener al menos 2 caracteres.')
    .max(20)
    .regex(/^[A-Za-z0-9-]+$/, 'El código solo admite letras, números y guiones.'),
  sedeId: z.string().uuid('Selecciona una sede.'),
  activo: z.boolean(),
})

type Valores = z.infer<typeof esquema>

const CAMPOS = ['nombre', 'codigo', 'sedeId', 'activo'] as const

function Formulario({
  abierto,
  departamento,
  sedes,
  soloSuDepartamento,
  onCerrar,
  onGuardado,
}: {
  abierto: boolean
  departamento: Departamento | null
  sedes: Sede[]
  soloSuDepartamento: boolean
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
  } = useForm<Valores>({ resolver: zodResolver(esquema), defaultValues: { activo: true } })

  useEffect(() => {
    if (!abierto) return

    setAvisoGeneral(null)
    reset({
      nombre: departamento?.nombre ?? '',
      codigo: departamento?.codigo ?? '',
      sedeId: departamento?.sedeId ?? sedes[0]?.id ?? '',
      activo: departamento?.activo ?? true,
    })
  }, [abierto, departamento, sedes, reset])

  const sedeId = watch('sedeId')
  const activo = watch('activo')

  const enviar = handleSubmit(async (valores) => {
    setAvisoGeneral(null)

    const cuerpo: SolicitudDepartamento = { ...valores, codigo: valores.codigo.toUpperCase() }

    try {
      if (departamento) {
        await api.departamentos.actualizar(departamento.id, cuerpo)
        toast.success(`"${cuerpo.nombre}" actualizado.`)
      } else {
        await api.departamentos.crear(cuerpo)
        toast.success(`"${cuerpo.nombre}" registrado.`)
      }

      onGuardado()
      onCerrar()
    } catch (fallo) {
      setAvisoGeneral(aplicarErroresDelServidor(fallo, setError, CAMPOS))
    }
  })

  return (
    <Dialog open={abierto} onOpenChange={(valor) => !valor && onCerrar()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{departamento ? 'Editar departamento' : 'Nuevo departamento'}</DialogTitle>
          <DialogDescription>
            El código es único dentro de cada sede, no en toda la organización.
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
              <Input id="nombre" {...register('nombre')} placeholder="Recursos Humanos" />
            </CampoFormulario>
            <CampoFormulario id="codigo" etiqueta="Código" error={errors.codigo?.message}>
              <Input id="codigo" {...register('codigo')} placeholder="RH" className="uppercase" />
            </CampoFormulario>
          </div>

          <CampoFormulario
            id="sedeId"
            etiqueta="Sede"
            error={errors.sedeId?.message}
            ayuda={
              soloSuDepartamento ? 'Solo un administrador puede mover un departamento de sede.' : undefined
            }
          >
            <SelectSimple
              id="sedeId"
              valor={sedeId}
              placeholder="Selecciona una sede"
              disabled={soloSuDepartamento}
              opciones={sedes.map((sede) => ({ valor: sede.id, etiqueta: sede.nombre }))}
              onCambio={(valor) => setValue('sedeId', valor, { shouldValidate: true })}
            />
          </CampoFormulario>

          <div className="flex items-center gap-2">
            <Checkbox
              id="activo"
              checked={activo}
              onCheckedChange={(valor) => setValue('activo', valor === true)}
            />
            <Label htmlFor="activo" className="font-normal">
              Departamento activo
            </Label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              {departamento ? 'Guardar cambios' : 'Registrar'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function Departamentos() {
  const { usuario } = useAuth()
  const esAdmin = usuario?.rol === 'Admin'

  const consultar = useCallback(
    (parametros: ParametrosLista) => api.departamentos.listar(parametros),
    [],
  )
  const lista = useListaPaginada<Departamento>(consultar)

  const [sedes, setSedes] = useState<Sede[]>([])
  const [enEdicion, setEnEdicion] = useState<Departamento | null>(null)
  const [abierto, setAbierto] = useState(false)

  useEffect(() => {
    api.sedes
      .listar({ tamano: 100, activo: true })
      .then((resultado) => setSedes(resultado.elementos))
      .catch(() => setSedes([]))
  }, [])

  // El supervisor solo manda sobre su propio departamento; el resto es de solo lectura.
  const puedeEditar = (departamento: Departamento) =>
    esAdmin || usuario?.departamentoId === departamento.id

  const abrir = (departamento: Departamento | null) => {
    setEnEdicion(departamento)
    setAbierto(true)
  }

  const desactivar = async (departamento: Departamento) => {
    try {
      await api.departamentos.desactivar(departamento.id)
      toast.success(`"${departamento.nombre}" pasó a baja.`)
      lista.recargar()
    } catch (fallo) {
      toast.error(fallo instanceof Error ? fallo.message : 'No se pudo dar de baja.')
    }
  }

  const columnas: Columna<Departamento>[] = [
    {
      clave: 'nombre',
      encabezado: 'Departamento',
      ordenPor: 'nombre',
      celda: (depto) => <span className="font-medium">{depto.nombre}</span>,
    },
    {
      clave: 'codigo',
      encabezado: 'Código',
      ordenPor: 'codigo',
      celda: (depto) => <span className="font-mono text-xs">{depto.codigo}</span>,
    },
    {
      clave: 'sede',
      encabezado: 'Sede',
      ordenPor: 'sede',
      celda: (depto) => <span className="text-sm">{depto.sedeNombre}</span>,
    },
    {
      clave: 'empleados',
      encabezado: 'Empleados',
      celda: (depto) => <span className="text-sm">{depto.totalEmpleados}</span>,
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      ordenPor: 'activo',
      celda: (depto) => (
        <Badge variant={depto.activo ? 'secondary' : 'outline'}>
          {depto.activo ? 'Activo' : 'Baja'}
        </Badge>
      ),
    },
    {
      clave: 'acciones',
      encabezado: '',
      className: 'text-right',
      celda: (depto) => (
        <div className="flex justify-end gap-1">
          {puedeEditar(depto) && (
            <Button variant="ghost" size="icon" onClick={() => abrir(depto)} aria-label="Editar">
              <Pencil className="size-4" />
            </Button>
          )}
          {esAdmin && depto.activo && (
            <Button
              variant="ghost"
              size="icon"
              onClick={() => desactivar(depto)}
              aria-label="Dar de baja"
            >
              <Power className="size-4" />
            </Button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <EncabezadoPagina
        titulo="Departamentos"
        descripcion={
          esAdmin
            ? 'Cada departamento pertenece a una sede y agrupa a su plantilla.'
            : 'Lectura global. Solo puedes editar el departamento que supervisas.'
        }
      />

      <TablaDatos
        columnas={columnas}
        filas={lista.elementos}
        claveDeFila={(depto) => depto.id}
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
        buscarPlaceholder="Buscar por nombre o código…"
        filtroActivo={lista.parametros.activo}
        onFiltroActivo={(activo) => lista.filtrar({ activo })}
        filtrosExtra={
          <SelectSimple
            valor={lista.parametros.sedeId ?? 'todas'}
            etiquetaAccesible="Filtrar por sede"
            className="w-48"
            opciones={[
              { valor: 'todas', etiqueta: 'Todas las sedes' },
              ...sedes.map((sede) => ({ valor: sede.id, etiqueta: sede.nombre })),
            ]}
            onCambio={(valor) => lista.filtrar({ sedeId: valor === 'todas' ? undefined : valor })}
          />
        }
        acciones={
          esAdmin && (
            <Button onClick={() => abrir(null)}>
              <Plus className="size-4" />
              Nuevo departamento
            </Button>
          )
        }
      />

      <Formulario
        abierto={abierto}
        departamento={enEdicion}
        sedes={sedes}
        soloSuDepartamento={!esAdmin}
        onCerrar={() => setAbierto(false)}
        onGuardado={lista.recargar}
      />
    </>
  )
}
