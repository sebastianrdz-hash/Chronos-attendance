import { api } from '@/api/cliente'
import type { Departamento, Empleado, ParametrosLista, Turno } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TablaDatos, type Columna } from '@/componentes/TablaDatos'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { SelectSimple } from '@/componentes/SelectSimple'
import { useListaPaginada } from '@/hooks/useListaPaginada'
import { formatearFecha } from '@/lib/formulario'
import { KeyRound, MoreHorizontal, Pencil, Plus, RotateCcw, UserMinus } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { toast } from 'sonner'
import { CredencialTemporal } from './CredencialTemporal'
import { FormularioEmpleado } from './FormularioEmpleado'

export function Empleados() {
  const { usuario } = useAuth()
  const esAdmin = usuario?.rol === 'Admin'

  const consultar = useCallback((parametros: ParametrosLista) => api.empleados.listar(parametros), [])
  const lista = useListaPaginada<Empleado>(consultar, { activo: true })

  const [departamentos, setDepartamentos] = useState<Departamento[]>([])
  const [turnos, setTurnos] = useState<Turno[]>([])
  const [enEdicion, setEnEdicion] = useState<Empleado | null>(null)
  const [abierto, setAbierto] = useState(false)
  const [credencial, setCredencial] = useState<{ empleado: Empleado; contrasena: string } | null>(
    null,
  )

  useEffect(() => {
    Promise.all([
      api.departamentos.listar({ tamano: 100, activo: true }),
      api.turnos.listar({ tamano: 100, activo: true }),
    ])
      .then(([deptos, turnosApi]) => {
        setDepartamentos(deptos.elementos)
        setTurnos(turnosApi.elementos)
      })
      .catch(() => toast.error('No se pudieron cargar los catálogos.'))
  }, [])

  /** El supervisor escribe solo sobre su propio departamento; la API lo vuelve a exigir. */
  const puedeEditar = (empleado: Empleado) =>
    esAdmin || usuario?.departamentoId === empleado.departamentoId

  const abrir = (empleado: Empleado | null) => {
    setEnEdicion(empleado)
    setAbierto(true)
  }

  const ejecutar = async (accion: Promise<unknown>, mensaje: string) => {
    try {
      await accion
      toast.success(mensaje)
      lista.recargar()
    } catch (fallo) {
      toast.error(fallo instanceof Error ? fallo.message : 'No se pudo completar la operación.')
    }
  }

  const reiniciarAcceso = async (empleado: Empleado) => {
    try {
      const resultado = await api.empleados.reiniciarAcceso(empleado.id)
      setCredencial({ empleado: resultado.empleado, contrasena: resultado.contrasenaTemporal })
      lista.recargar()
    } catch (fallo) {
      toast.error(fallo instanceof Error ? fallo.message : 'No se pudo reiniciar el acceso.')
    }
  }

  const columnas: Columna<Empleado>[] = [
    {
      clave: 'empleado',
      encabezado: 'Empleado',
      ordenPor: 'nombre',
      celda: (empleado) => (
        <div>
          <p className="font-medium">{empleado.nombreCompleto}</p>
          <p className="text-xs text-muted-foreground">{empleado.correoCorporativo}</p>
        </div>
      ),
    },
    {
      clave: 'numero',
      encabezado: 'Número',
      ordenPor: 'numero',
      celda: (empleado) => <span className="font-mono text-xs">{empleado.numeroEmpleado}</span>,
    },
    {
      clave: 'puesto',
      encabezado: 'Puesto',
      ordenPor: 'puesto',
      celda: (empleado) => <span className="text-sm">{empleado.puesto ?? '—'}</span>,
    },
    {
      clave: 'adscripcion',
      encabezado: 'Adscripción',
      ordenPor: 'departamento',
      celda: (empleado) => (
        <div>
          <p className="text-sm">{empleado.departamentoNombre}</p>
          <p className="text-xs text-muted-foreground">{empleado.sedeNombre}</p>
        </div>
      ),
    },
    {
      clave: 'turno',
      encabezado: 'Turno',
      celda: (empleado) => <span className="text-sm">{empleado.turnoNombre ?? 'Sin asignar'}</span>,
    },
    {
      clave: 'rol',
      encabezado: 'Rol',
      celda: (empleado) => (
        <Badge variant={empleado.rol === 'Empleado' ? 'outline' : 'secondary'}>
          {empleado.rol}
        </Badge>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      ordenPor: 'activo',
      celda: (empleado) =>
        empleado.activo ? (
          <div className="space-y-1">
            <Badge variant="secondary">Activo</Badge>
            {empleado.debeCambiarContrasena && (
              <p className="text-[11px] text-amber-600">Contraseña temporal</p>
            )}
          </div>
        ) : (
          <div className="space-y-1">
            <Badge variant="outline">Baja</Badge>
            <p className="text-[11px] text-muted-foreground">
              {formatearFecha(empleado.fechaBaja)}
            </p>
          </div>
        ),
    },
    {
      clave: 'acciones',
      encabezado: '',
      className: 'text-right',
      celda: (empleado) =>
        puedeEditar(empleado) ? (
          <DropdownMenu>
            <DropdownMenuTrigger
              render={<Button variant="ghost" size="icon" aria-label="Acciones" />}
            >
              <MoreHorizontal className="size-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={() => abrir(empleado)}>
                <Pencil className="size-4" />
                Editar
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => reiniciarAcceso(empleado)}>
                <KeyRound className="size-4" />
                Reiniciar acceso
              </DropdownMenuItem>
              {empleado.activo ? (
                <DropdownMenuItem
                  variant="destructive"
                  onClick={() =>
                    ejecutar(
                      api.empleados.darDeBaja(empleado.id),
                      `${empleado.nombreCompleto} pasó a baja.`,
                    )
                  }
                >
                  <UserMinus className="size-4" />
                  Dar de baja
                </DropdownMenuItem>
              ) : (
                <DropdownMenuItem
                  onClick={() =>
                    ejecutar(
                      api.empleados.reactivar(empleado.id),
                      `${empleado.nombreCompleto} fue reactivado.`,
                    )
                  }
                >
                  <RotateCcw className="size-4" />
                  Reactivar
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null,
    },
  ]

  return (
    <>
      <EncabezadoPagina
        titulo="Empleados"
        descripcion={
          esAdmin
            ? 'Alta, edición y baja lógica. El alta crea también la cuenta de acceso.'
            : 'Ves a toda la organización, pero solo administras la plantilla de tu departamento.'
        }
      />

      <TablaDatos
        columnas={columnas}
        filas={lista.elementos}
        claveDeFila={(empleado) => empleado.id}
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
        buscarPlaceholder="Buscar por nombre, número, correo o puesto…"
        filtroActivo={lista.parametros.activo}
        onFiltroActivo={(activo) => lista.filtrar({ activo })}
        filtrosExtra={
          <SelectSimple
            valor={lista.parametros.departamentoId ?? 'todos'}
            etiquetaAccesible="Filtrar por departamento"
            className="w-52"
            opciones={[
              { valor: 'todos', etiqueta: 'Todos los departamentos' },
              ...departamentos.map((depto) => ({ valor: depto.id, etiqueta: depto.nombre })),
            ]}
            onCambio={(valor) =>
              lista.filtrar({ departamentoId: valor === 'todos' ? undefined : valor })
            }
          />
        }
        acciones={
          <Button onClick={() => abrir(null)}>
            <Plus className="size-4" />
            Alta de empleado
          </Button>
        }
      />

      <FormularioEmpleado
        abierto={abierto}
        empleado={enEdicion}
        departamentos={departamentos}
        turnos={turnos}
        onCerrar={() => setAbierto(false)}
        onGuardado={lista.recargar}
        onAlta={(empleado, contrasena) => setCredencial({ empleado, contrasena })}
      />

      <CredencialTemporal datos={credencial} onCerrar={() => setCredencial(null)} />
    </>
  )
}
