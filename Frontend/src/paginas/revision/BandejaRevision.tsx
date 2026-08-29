import { api, ErrorApi } from '@/api/cliente'
import type { ChecadaPorRevisar, ParametrosLista } from '@/api/tipos'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { TablaDatos, type Columna } from '@/componentes/TablaDatos'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
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
import { Label } from '@/components/ui/label'
import { useListaPaginada } from '@/hooks/useListaPaginada'
import { Check, Info, Loader2, ScrollText, X } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'

type Dictamen = 'aprobar' | 'rechazar'

export function BandejaRevision() {
  const consultar = useCallback(
    (parametros: ParametrosLista) => api.revision.pendientes(parametros),
    [],
  )

  const lista = useListaPaginada<ChecadaPorRevisar>(consultar)
  const [enDictamen, setEnDictamen] = useState<{ checada: ChecadaPorRevisar; accion: Dictamen } | null>(
    null,
  )

  const columnas: Columna<ChecadaPorRevisar>[] = [
    {
      clave: 'empleado',
      encabezado: 'Empleado',
      celda: (fila) => (
        <div>
          <p className="font-medium">{fila.empleadoNombre}</p>
          <p className="text-xs text-muted-foreground">
            {fila.numeroEmpleado} · {fila.departamentoNombre}
          </p>
        </div>
      ),
    },
    {
      clave: 'momento',
      encabezado: 'Fichaje',
      celda: (fila) => (
        <div>
          <p>{fila.tipo === 'Entrada' ? 'Entrada' : 'Salida'}</p>
          <p className="text-xs text-muted-foreground">
            {new Date(fila.momentoUtc).toLocaleString('es-MX', {
              dateStyle: 'medium',
              timeStyle: 'short',
            })}
          </p>
        </div>
      ),
    },
    {
      clave: 'sede',
      encabezado: 'Sede',
      className: 'hidden lg:table-cell',
      celda: (fila) => fila.sedeNombre ?? '—',
    },
    {
      clave: 'senales',
      encabezado: 'Señales',
      celda: (fila) => (
        <div className="flex flex-wrap gap-1.5">
          {fila.senales.map((senal, indice) => (
            <Badge
              key={`${senal.tipo}-${indice}`}
              variant={senal.resultado === 'Confirmada' ? 'secondary' : 'destructive'}
            >
              {senal.tipoNombre}
            </Badge>
          ))}
        </div>
      ),
    },
    {
      clave: 'confianza',
      encabezado: 'Confianza',
      celda: (fila) => (
        <span className="tabular-nums">
          {fila.puntajeConfianza} <span className="text-muted-foreground">de 100</span>
        </span>
      ),
    },
    {
      clave: 'acciones',
      encabezado: '',
      className: 'text-right',
      celda: (fila) => (
        <div className="flex justify-end gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => setEnDictamen({ checada: fila, accion: 'aprobar' })}
          >
            <Check className="size-4" />
            Aprobar
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={() => setEnDictamen({ checada: fila, accion: 'rechazar' })}
          >
            <X className="size-4" />
            Rechazar
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div>
      <EncabezadoPagina
        titulo="Revisión de fichajes"
        descripcion="Checadas que no reunieron evidencia suficiente y esperan un dictamen."
        acciones={
          <Button variant="outline" render={<Link to="/bitacora" />}>
            <ScrollText />
            Ver bitácora
          </Button>
        }
      />

      <Alert className="mb-6">
        <Info />
        <AlertDescription>
          Un código QR prueba que alguien estuvo frente a la pantalla de la sede, no quién. Estas
          checadas se quedaron con esa única señal. Tu dictamen y su motivo quedan registrados de
          forma permanente en la bitácora.
        </AlertDescription>
      </Alert>

      <TablaDatos
        columnas={columnas}
        filas={lista.elementos}
        claveDeFila={(fila) => fila.id}
        cargando={lista.cargando}
        error={lista.error}
        vacio="No hay fichajes esperando revisión."
        total={lista.total}
        pagina={lista.parametros.pagina ?? 1}
        totalPaginas={lista.totalPaginas}
        hayPaginaAnterior={lista.hayPaginaAnterior}
        hayPaginaSiguiente={lista.hayPaginaSiguiente}
        onOrdenar={lista.ordenar}
        onPagina={lista.irAPagina}
        onBuscar={(texto) => lista.filtrar({ buscar: texto })}
        buscarPlaceholder="Buscar por empleado…"
      />

      {enDictamen && (
        <DialogoDictamen
          checada={enDictamen.checada}
          accion={enDictamen.accion}
          onCerrar={() => setEnDictamen(null)}
          onResuelto={() => {
            setEnDictamen(null)
            lista.recargar()
          }}
        />
      )}
    </div>
  )
}

function DialogoDictamen({
  checada,
  accion,
  onCerrar,
  onResuelto,
}: {
  checada: ChecadaPorRevisar
  accion: Dictamen
  onCerrar: () => void
  onResuelto: () => void
}) {
  const [motivo, setMotivo] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const aprobar = accion === 'aprobar'

  async function enviar() {
    setEnviando(true)
    setError(null)

    try {
      if (aprobar) {
        await api.revision.aprobar(checada.id, motivo.trim())
      } else {
        await api.revision.rechazar(checada.id, motivo.trim())
      }

      toast.success(`Fichaje de ${checada.empleadoNombre} ${aprobar ? 'aprobado' : 'rechazado'}.`)
      onResuelto()
    } catch (fallo) {
      setError(fallo instanceof ErrorApi ? fallo.message : 'No se pudo registrar el dictamen.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && onCerrar()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{aprobar ? 'Aprobar fichaje' : 'Rechazar fichaje'}</DialogTitle>
          <DialogDescription>
            {aprobar
              ? `El fichaje de ${checada.empleadoNombre} contará para su jornada.`
              : `El fichaje de ${checada.empleadoNombre} dejará de contar para su jornada, pero no se borra.`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <Label htmlFor="motivo">Motivo</Label>
          <Input
            id="motivo"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="Qué comprobaste y cómo"
            maxLength={500}
            autoFocus
          />
          <p className="text-xs text-muted-foreground">
            Entre 5 y 500 caracteres. Es lo que responderá la pregunta dentro de seis meses.
          </p>
        </div>

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Button>
          <Button onClick={enviar} disabled={enviando || motivo.trim().length < 5}>
            {enviando && <Loader2 className="animate-spin" />}
            {aprobar ? 'Aprobar' : 'Rechazar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
