import { SelectSimple } from '@/componentes/SelectSimple'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Search } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

export interface Columna<T> {
  clave: string
  encabezado: string
  /** Nombre del campo de orden que entiende la API. Sin él la columna no es ordenable. */
  ordenPor?: string
  celda: (fila: T) => ReactNode
  className?: string
}

interface Props<T> {
  columnas: Columna<T>[]
  filas: T[]
  claveDeFila: (fila: T) => string
  cargando: boolean
  error?: string | null
  vacio?: string
  total: number
  pagina: number
  totalPaginas: number
  hayPaginaAnterior: boolean
  hayPaginaSiguiente: boolean
  ordenarPor?: string
  descendente?: boolean
  onOrdenar: (campo: string) => void
  onPagina: (pagina: number) => void
  onBuscar: (texto: string) => void
  buscarPlaceholder?: string
  filtroActivo?: boolean | undefined
  onFiltroActivo?: (activo: boolean | undefined) => void
  acciones?: ReactNode
  filtrosExtra?: ReactNode
}

/** Retrasa la búsqueda para no lanzar una petición por cada tecla. */
function useTextoDiferido(valor: string, onCambio: (texto: string) => void, ms = 350) {
  useEffect(() => {
    const temporizador = setTimeout(() => onCambio(valor), ms)
    return () => clearTimeout(temporizador)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [valor, ms])
}

export function TablaDatos<T>({
  columnas,
  filas,
  claveDeFila,
  cargando,
  error,
  vacio = 'No hay registros que coincidan con los filtros.',
  total,
  pagina,
  totalPaginas,
  hayPaginaAnterior,
  hayPaginaSiguiente,
  ordenarPor,
  descendente,
  onOrdenar,
  onPagina,
  onBuscar,
  buscarPlaceholder = 'Buscar…',
  filtroActivo,
  onFiltroActivo,
  acciones,
  filtrosExtra,
}: Props<T>) {
  const [texto, setTexto] = useState('')
  useTextoDiferido(texto, onBuscar)

  const estadoSeleccionado = filtroActivo === undefined ? 'todos' : filtroActivo ? 'activos' : 'bajas'

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 flex-wrap items-center gap-2">
          <div className="relative min-w-52 flex-1 sm:max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder={buscarPlaceholder}
              className="pl-9"
              aria-label="Buscar"
            />
          </div>

          {onFiltroActivo && (
            <SelectSimple
              valor={estadoSeleccionado}
              etiquetaAccesible="Filtrar por estado"
              className="w-36"
              opciones={[
                { valor: 'todos', etiqueta: 'Todos' },
                { valor: 'activos', etiqueta: 'Activos' },
                { valor: 'bajas', etiqueta: 'Bajas' },
              ]}
              onCambio={(valor) =>
                onFiltroActivo(valor === 'todos' ? undefined : valor === 'activos')
              }
            />
          )}

          {filtrosExtra}
        </div>

        {acciones}
      </div>

      <div className="overflow-hidden rounded-lg border bg-background">
        <Table>
          <TableHeader>
            <TableRow>
              {columnas.map((columna) => (
                <TableHead key={columna.clave} className={columna.className}>
                  {columna.ordenPor ? (
                    <button
                      type="button"
                      onClick={() => onOrdenar(columna.ordenPor!)}
                      className="inline-flex items-center gap-1 font-medium hover:text-foreground"
                    >
                      {columna.encabezado}
                      {ordenarPor === columna.ordenPor &&
                        (descendente ? (
                          <ArrowDown className="size-3.5" />
                        ) : (
                          <ArrowUp className="size-3.5" />
                        ))}
                    </button>
                  ) : (
                    columna.encabezado
                  )}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {cargando &&
              Array.from({ length: 5 }).map((_, indice) => (
                <TableRow key={`esqueleto-${indice}`}>
                  {columnas.map((columna) => (
                    <TableCell key={columna.clave}>
                      <Skeleton className="h-4 w-full max-w-32" />
                    </TableCell>
                  ))}
                </TableRow>
              ))}

            {!cargando && error && (
              <TableRow>
                <TableCell colSpan={columnas.length} className="py-10 text-center text-destructive">
                  {error}
                </TableCell>
              </TableRow>
            )}

            {!cargando && !error && filas.length === 0 && (
              <TableRow>
                <TableCell
                  colSpan={columnas.length}
                  className="py-10 text-center text-muted-foreground"
                >
                  {vacio}
                </TableCell>
              </TableRow>
            )}

            {!cargando &&
              !error &&
              filas.map((fila) => (
                <TableRow key={claveDeFila(fila)}>
                  {columnas.map((columna) => (
                    <TableCell key={columna.clave} className={cn(columna.className)}>
                      {columna.celda(fila)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-muted-foreground">
          {total === 0
            ? 'Sin resultados'
            : `${total} registro${total === 1 ? '' : 's'} · página ${pagina} de ${totalPaginas}`}
        </p>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!hayPaginaAnterior || cargando}
            onClick={() => onPagina(pagina - 1)}
          >
            <ChevronLeft className="size-4" />
            Anterior
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!hayPaginaSiguiente || cargando}
            onClick={() => onPagina(pagina + 1)}
          >
            Siguiente
            <ChevronRight className="size-4" />
          </Button>
        </div>
      </div>
    </div>
  )
}
