import { api } from '@/api/cliente'
import type { AsientoBitacora, ParametrosLista } from '@/api/tipos'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TablaDatos, type Columna } from '@/componentes/TablaDatos'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { useListaPaginada } from '@/hooks/useListaPaginada'
import { Lock } from 'lucide-react'
import { useCallback } from 'react'

export function Bitacora() {
  const consultar = useCallback(
    (parametros: ParametrosLista) => api.revision.bitacora(parametros),
    [],
  )

  const lista = useListaPaginada<AsientoBitacora>(consultar)

  const columnas: Columna<AsientoBitacora>[] = [
    {
      clave: 'cuando',
      encabezado: 'Cuándo',
      celda: (fila) =>
        new Date(fila.ocurridoUtc).toLocaleString('es-MX', {
          dateStyle: 'medium',
          timeStyle: 'short',
        }),
    },
    {
      clave: 'accion',
      encabezado: 'Acción',
      celda: (fila) => (
        <Badge variant={fila.accion === 'ChecadaRechazada' ? 'destructive' : 'secondary'}>
          {fila.accionNombre}
        </Badge>
      ),
    },
    {
      clave: 'quien',
      encabezado: 'Quién',
      celda: (fila) => fila.usuarioCorreo ?? 'Sistema',
    },
    {
      clave: 'motivo',
      encabezado: 'Motivo',
      celda: (fila) => (
        <span className="text-sm text-muted-foreground">{fila.motivo ?? '—'}</span>
      ),
    },
  ]

  return (
    <div>
      <EncabezadoPagina
        titulo="Bitácora"
        descripcion="Registro de quién decidió qué y por qué, del más reciente al más antiguo."
      />

      <Alert className="mb-6">
        <Lock />
        <AlertDescription>
          La bitácora solo admite inserciones. Un disparador de la base de datos rechaza
          cualquier intento de modificar o borrar un asiento, incluso desde la propia
          aplicación.
        </AlertDescription>
      </Alert>

      <TablaDatos
        columnas={columnas}
        filas={lista.elementos}
        claveDeFila={(fila) => fila.id}
        cargando={lista.cargando}
        error={lista.error}
        vacio="Todavía no hay asientos registrados."
        total={lista.total}
        pagina={lista.parametros.pagina ?? 1}
        totalPaginas={lista.totalPaginas}
        hayPaginaAnterior={lista.hayPaginaAnterior}
        hayPaginaSiguiente={lista.hayPaginaSiguiente}
        onOrdenar={lista.ordenar}
        onPagina={lista.irAPagina}
        onBuscar={(texto) => lista.filtrar({ buscar: texto })}
        buscarPlaceholder="Buscar…"
      />
    </div>
  )
}
