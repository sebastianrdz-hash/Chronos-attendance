import { api } from '@/api/cliente'
import type { ParametrosLista, Sede } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TablaDatos, type Columna } from '@/componentes/TablaDatos'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useListaPaginada } from '@/hooks/useListaPaginada'
import { MapPin, Pencil, Plus, Power } from 'lucide-react'
import { useCallback, useState } from 'react'
import { toast } from 'sonner'
import { FormularioSede } from './FormularioSede'

export function Sedes() {
  const { usuario } = useAuth()
  const puedeEditar = usuario?.rol === 'Admin'

  const consultar = useCallback((parametros: ParametrosLista) => api.sedes.listar(parametros), [])
  const lista = useListaPaginada<Sede>(consultar)

  const [enEdicion, setEnEdicion] = useState<Sede | null>(null)
  const [abierto, setAbierto] = useState(false)

  const abrir = (sede: Sede | null) => {
    setEnEdicion(sede)
    setAbierto(true)
  }

  const desactivar = async (sede: Sede) => {
    try {
      await api.sedes.desactivar(sede.id)
      toast.success(`"${sede.nombre}" pasó a baja.`)
      lista.recargar()
    } catch (fallo) {
      toast.error(fallo instanceof Error ? fallo.message : 'No se pudo dar de baja.')
    }
  }

  const columnas: Columna<Sede>[] = [
    {
      clave: 'nombre',
      encabezado: 'Sede',
      ordenPor: 'nombre',
      celda: (sede) => (
        <div>
          <p className="font-medium">{sede.nombre}</p>
          <p className="text-xs text-muted-foreground">{sede.direccion ?? 'Sin dirección'}</p>
        </div>
      ),
    },
    {
      clave: 'codigo',
      encabezado: 'Código',
      ordenPor: 'codigo',
      celda: (sede) => <span className="font-mono text-xs">{sede.codigo}</span>,
    },
    {
      clave: 'zona',
      encabezado: 'Zona horaria',
      ordenPor: 'zonaHoraria',
      celda: (sede) => <span className="text-sm">{sede.zonaHoraria}</span>,
    },
    {
      clave: 'geocerca',
      encabezado: 'Geocerca',
      celda: (sede) =>
        sede.radioMetros ? (
          <span className="inline-flex items-center gap-1 text-sm text-muted-foreground">
            <MapPin className="size-3.5" />
            {sede.radioMetros} m
          </span>
        ) : (
          <span className="text-sm text-muted-foreground">—</span>
        ),
    },
    {
      clave: 'plantilla',
      encabezado: 'Plantilla',
      celda: (sede) => (
        <span className="text-sm">
          {sede.totalEmpleados} empleados · {sede.totalDepartamentos} deptos.
        </span>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      ordenPor: 'activa',
      celda: (sede) => (
        <Badge variant={sede.activa ? 'secondary' : 'outline'}>
          {sede.activa ? 'Activa' : 'Baja'}
        </Badge>
      ),
    },
  ]

  if (puedeEditar) {
    columnas.push({
      clave: 'acciones',
      encabezado: '',
      className: 'text-right',
      celda: (sede) => (
        <div className="flex justify-end gap-1">
          <Button variant="ghost" size="icon" onClick={() => abrir(sede)} aria-label="Editar">
            <Pencil className="size-4" />
          </Button>
          {sede.activa && (
            <Button
              variant="ghost"
              size="icon"
              onClick={() => desactivar(sede)}
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
        titulo="Sedes"
        descripcion="Centros de trabajo. Cada uno define su zona horaria y, opcionalmente, una geocerca."
      />

      <TablaDatos
        columnas={columnas}
        filas={lista.elementos}
        claveDeFila={(sede) => sede.id}
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
        buscarPlaceholder="Buscar por nombre, código o dirección…"
        filtroActivo={lista.parametros.activo}
        onFiltroActivo={(activo) => lista.filtrar({ activo })}
        acciones={
          puedeEditar && (
            <Button onClick={() => abrir(null)}>
              <Plus className="size-4" />
              Nueva sede
            </Button>
          )
        }
      />

      <FormularioSede
        abierto={abierto}
        sede={enEdicion}
        onCerrar={() => setAbierto(false)}
        onGuardado={lista.recargar}
      />
    </>
  )
}
