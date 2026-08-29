import type { Checada, EstadoChecada, NivelConfianza, ResultadoSenal } from '@/api/tipos'
import { Badge } from '@/components/ui/badge'
import { CheckCircle2, Clock } from 'lucide-react'

export const etiquetaEstado: Record<EstadoChecada, string> = {
  Rechazada: 'Rechazada',
  RequiereRevision: 'Pendiente de revisión',
  Verificada: 'Verificada',
  AjustadaPorSupervisor: 'Ajustada por un supervisor',
}

const varianteEstado: Record<EstadoChecada, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Rechazada: 'destructive',
  RequiereRevision: 'secondary',
  Verificada: 'default',
  AjustadaPorSupervisor: 'outline',
}

const explicacionNivel: Record<NivelConfianza, string> = {
  Nula: 'No se reunió ninguna evidencia válida.',
  Baja: 'Una sola señal. Recursos Humanos tendrá que revisarlo.',
  Media: 'Hay evidencia, pero todavía por debajo del umbral de confianza alta.',
  Alta: 'Varias señales independientes coinciden.',
}

export function ResumenChecada({ checada }: { checada: Checada }) {
  const hora = new Date(checada.momentoUtc).toLocaleTimeString('es-MX', {
    hour: '2-digit',
    minute: '2-digit',
  })

  return (
    <div className="w-full space-y-5">
      <div className="flex flex-col items-center gap-2">
        <CheckCircle2 className="size-12 text-emerald-600" />
        <p className="text-2xl font-semibold">
          {checada.tipo === 'Entrada' ? 'Entrada' : 'Salida'} registrada
        </p>
        <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
          <Clock className="size-4" />
          {hora}
          {checada.sedeNombre && ` · ${checada.sedeNombre}`}
        </p>
      </div>

      <div className="rounded-lg border p-4">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">Confianza</span>
          <Badge variant={varianteEstado[checada.estado]}>{etiquetaEstado[checada.estado]}</Badge>
        </div>

        <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full bg-primary"
            style={{ width: `${checada.puntajeConfianza}%` }}
          />
        </div>

        <p className="mt-2 text-sm text-muted-foreground">
          {checada.puntajeConfianza} de 100. {explicacionNivel[checada.nivelConfianza]}
        </p>
      </div>

      <div>
        <p className="mb-2 text-sm font-medium">Señales aportadas</p>
        <ul className="space-y-2">
          {checada.senales.map((senal, indice) => (
            <li
              key={`${senal.tipo}-${indice}`}
              className="flex items-center justify-between rounded-md border px-3 py-2 text-sm"
            >
              <span>{senal.tipoNombre}</span>
              <span className="flex items-center gap-2">
                <span className="text-muted-foreground">{etiquetaResultado[senal.resultado]}</span>
                <Badge variant="outline">
                  {senal.pesoAplicado > 0 ? `+${senal.pesoAplicado}` : senal.pesoAplicado}
                </Badge>
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

const etiquetaResultado: Record<ResultadoSenal, string> = {
  Confirmada: 'Confirmada',
  Fallida: 'Falló',
  NoDisponible: 'No disponible',
  Sospechosa: 'Sospechosa',
}
