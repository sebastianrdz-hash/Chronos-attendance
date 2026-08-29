import { api, ErrorApi } from '@/api/cliente'
import type { CodigoKiosco, Sede } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { SelectSimple } from '@/componentes/SelectSimple'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Maximize2, Minimize2, TriangleAlert } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Pantalla que se deja puesta en la entrada de la sede. Muestra un código que caduca en
 * segundos y se renueva solo.
 *
 * El sentido del flujo es lo que sostiene la seguridad: la sede muestra y el empleado
 * escanea. Si fuera al revés, bastaría con mandar una captura por mensajería para que
 * otro fichara por ti.
 */
export function Kiosco() {
  const { usuario } = useAuth()
  const [sedes, setSedes] = useState<Sede[]>([])
  const [sedeId, setSedeId] = useState('')
  const [codigo, setCodigo] = useState<CodigoKiosco | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pantallaCompleta, setPantallaCompleta] = useState(false)
  const contenedor = useRef<HTMLDivElement>(null)

  useEffect(() => {
    api.sedes
      .listar({ activo: true, tamano: 100 })
      .then((pagina) => {
        setSedes(pagina.elementos)

        // Un supervisor solo puede abrir el kiosco de su sede, así que no tiene sentido
        // hacerle elegir: se preselecciona la única que la API le va a permitir.
        const propia = pagina.elementos.find((sede) => sede.id === usuario?.sedeId)
        setSedeId(propia?.id ?? pagina.elementos[0]?.id ?? '')
      })
      .catch((fallo: ErrorApi) => setError(fallo.message))
  }, [usuario?.sedeId])

  const pedirCodigo = useCallback(async (id: string) => {
    try {
      setCodigo(await api.kiosco.codigo(id))
      setError(null)
    } catch (fallo) {
      setError(fallo instanceof ErrorApi ? fallo.message : 'No se pudo emitir el código.')
    }
  }, [])

  useEffect(() => {
    if (!sedeId) return

    void pedirCodigo(sedeId)
  }, [sedeId, pedirCodigo])

  // El temporizador se reprograma con cada código en vez de usar un intervalo fijo: así
  // el ritmo lo marca la vigencia que decidió el servidor y no una constante del cliente
  // que podría quedar desfasada.
  useEffect(() => {
    if (!codigo || !sedeId) return

    const temporizador = window.setTimeout(
      () => void pedirCodigo(sedeId),
      codigo.segundosRefresco * 1000,
    )

    return () => window.clearTimeout(temporizador)
  }, [codigo, sedeId, pedirCodigo])

  useEffect(() => {
    const alCambiar = () => setPantallaCompleta(Boolean(document.fullscreenElement))

    document.addEventListener('fullscreenchange', alCambiar)
    return () => document.removeEventListener('fullscreenchange', alCambiar)
  }, [])

  const alternarPantallaCompleta = async () => {
    if (document.fullscreenElement) {
      await document.exitFullscreen()
    } else {
      await contenedor.current?.requestFullscreen()
    }
  }

  const opciones = sedes.map((sede) => ({ valor: sede.id, etiqueta: sede.nombre }))

  return (
    <div>
      <EncabezadoPagina
        titulo="Kiosco de fichaje"
        descripcion="Deja esta pantalla visible en la entrada. El código se renueva solo y caduca en segundos."
        acciones={
          <Button variant="outline" onClick={() => void alternarPantallaCompleta()}>
            {pantallaCompleta ? <Minimize2 /> : <Maximize2 />}
            {pantallaCompleta ? 'Salir de pantalla completa' : 'Pantalla completa'}
          </Button>
        }
      />

      {error && (
        <Alert variant="destructive" className="mb-6">
          <TriangleAlert />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {opciones.length > 1 && (
        <div className="mb-6 max-w-sm">
          <Label htmlFor="sede-kiosco">Sede</Label>
          <SelectSimple
            id="sede-kiosco"
            valor={sedeId}
            opciones={opciones}
            onCambio={setSedeId}
            placeholder="Elige una sede"
            className="mt-1.5"
          />
        </div>
      )}

      <div ref={contenedor} className="bg-background">
        <Card className="border-2">
          <CardContent className="flex flex-col items-center gap-6 py-10">
            {/* La `key` fuerza un remontaje con cada código nuevo, de modo que la cuenta
                regresiva arranque limpia sin tener que reiniciarla desde un efecto. */}
            {codigo ? (
              <CodigoVigente key={codigo.token} codigo={codigo} />
            ) : (
              <p className="text-muted-foreground">Emitiendo código…</p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function CodigoVigente({ codigo }: { codigo: CodigoKiosco }) {
  const restante = useSegundosRestantes(codigo.expiraUtc)
  const total = Math.max(
    1,
    Math.round((Date.parse(codigo.expiraUtc) - Date.parse(codigo.emitidoUtc)) / 1000),
  )

  return (
    <>
      <p className="text-xl font-semibold">{codigo.sedeNombre}</p>

      <img
        src={`data:image/png;base64,${codigo.imagenPng}`}
        alt="Código QR de fichaje"
        className="size-64 rounded-lg sm:size-80"
        // El QR cambia cada pocos segundos y el navegador interpolaría los píxeles al
        // escalarlo, emborronando los módulos justo cuando la cámara intenta leerlos.
        style={{ imageRendering: 'pixelated' }}
      />

      <div className="w-full max-w-xs">
        <div className="h-2 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full bg-primary transition-[width] duration-1000 ease-linear"
            style={{ width: `${Math.max(0, (restante / total) * 100)}%` }}
          />
        </div>
        <p className="mt-2 text-center text-sm text-muted-foreground">
          {restante > 0 ? `Caduca en ${restante} s` : 'Renovando…'}
        </p>
      </div>

      <p className="max-w-md text-center text-sm text-muted-foreground">
        Escanea este código desde la app con tu sesión iniciada. Cada código sirve una sola
        vez.
      </p>
    </>
  )
}

function useSegundosRestantes(expiraUtc: string) {
  const calcular = useCallback(
    () => Math.max(0, Math.round((Date.parse(expiraUtc) - Date.now()) / 1000)),
    [expiraUtc],
  )

  const [restante, setRestante] = useState(calcular)

  useEffect(() => {
    const intervalo = window.setInterval(() => setRestante(calcular()), 1000)
    return () => window.clearInterval(intervalo)
  }, [calcular])

  return restante
}
