import { api, ErrorApi, ErrorFichaje } from '@/api/cliente'
import type { Checada } from '@/api/tipos'
import { firmar, motivoNoDisponible } from '@/api/webauthn'
import { EncabezadoPagina } from '@/componentes/EncabezadoPagina'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Html5Qrcode } from 'html5-qrcode'
import { CameraOff, Fingerprint, QrCode, TriangleAlert } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ResumenChecada } from './ResumenChecada'

const CONTENEDOR = 'lector-qr'

type Estado =
  | { fase: 'inicio' }
  | { fase: 'escaneando' }
  | { fase: 'firmando' }
  | { fase: 'enviando' }
  | { fase: 'listo'; checada: Checada }
  | { fase: 'error'; mensaje: string; recuperable: boolean }

export function Fichar() {
  const [estado, setEstado] = useState<Estado>({ fase: 'inicio' })
  const [tieneCredencial, setTieneCredencial] = useState(false)
  const lector = useRef<Html5Qrcode | null>(null)

  // Se consulta al entrar y no al escanear: para cuando el código aparece frente a la
  // cámara ya no hay margen para una llamada de ida y vuelta.
  useEffect(() => {
    if (motivoNoDisponible()) return

    api.webauthn
      .credenciales()
      .then((lista) => setTieneCredencial(lista.length > 0))
      .catch(() => setTieneCredencial(false))
  }, [])

  const detener = useCallback(async () => {
    const instancia = lector.current
    if (!instancia) return

    lector.current = null

    try {
      if (instancia.isScanning) await instancia.stop()
      instancia.clear()
    } catch {
      // Si la cámara ya se soltó sola, no hay nada que reportar.
    }
  }, [])

  // Soltar la cámara al salir de la pantalla no es opcional: si el componente se
  // desmonta con el lector activo, el indicador del dispositivo se queda encendido.
  useEffect(() => () => void detener(), [detener])

  const enviar = useCallback(
    async (token: string) => {
      await detener()

      // Se pide la firma antes de enviar nada. Si el empleado no tiene credencial, o
      // cancela el diálogo, el fichaje sigue adelante solo con el QR: valdrá menos y
      // pasará por revisión, pero nadie se queda sin poder registrar su entrada.
      let asercion: unknown

      if (tieneCredencial) {
        setEstado({ fase: 'firmando' })

        try {
          asercion = await firmar()
        } catch {
          asercion = undefined
        }
      }

      setEstado({ fase: 'enviando' })

      try {
        setEstado({ fase: 'listo', checada: await api.checadas.porQr(token, undefined, asercion) })
      } catch (fallo) {
        if (fallo instanceof ErrorFichaje) {
          // Un código vencido o ya usado se arregla volviendo a escanear; una firma
          // inválida o una sede ajena, no.
          const recuperable = fallo.motivo === 'Caducado' || fallo.motivo === 'NonceReusado'
          setEstado({ fase: 'error', mensaje: fallo.message, recuperable })
          return
        }

        setEstado({
          fase: 'error',
          mensaje: fallo instanceof ErrorApi ? fallo.message : 'No se pudo registrar el fichaje.',
          recuperable: true,
        })
      }
    },
    [detener, tieneCredencial],
  )

  const escanear = useCallback(async () => {
    setEstado({ fase: 'escaneando' })

    try {
      const instancia = new Html5Qrcode(CONTENEDOR)
      lector.current = instancia

      await instancia.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        (texto) => void enviar(texto),
        // El segundo callback se dispara en cada cuadro sin código. Ignorarlo es lo
        // correcto: no es un fallo, es que todavía no aparece el QR.
        () => {},
      )
    } catch {
      setEstado({
        fase: 'error',
        mensaje:
          'No se pudo abrir la cámara. Revisa el permiso del navegador y que la página esté en HTTPS.',
        recuperable: true,
      })
    }
  }, [enviar])

  return (
    <div className="mx-auto max-w-2xl">
      <EncabezadoPagina
        titulo="Fichar"
        descripcion="Escanea el código que muestra la pantalla de tu sede."
      />

      <Card>
        <CardContent className="flex flex-col items-center gap-6 py-8">
          {/* El contenedor vive siempre en el árbol: html5-qrcode lo busca por id al
              arrancar y monta el vídeo dentro, así que no puede aparecer después. */}
          <div
            id={CONTENEDOR}
            className={
              estado.fase === 'escaneando'
                ? 'w-full max-w-sm overflow-hidden rounded-lg border'
                : 'hidden'
            }
          />

          {estado.fase === 'inicio' && (
            <>
              <QrCode className="size-16 text-muted-foreground" />
              <p className="text-center text-sm text-muted-foreground">
                Necesitas permiso de cámara. El código de la pantalla caduca en segundos y
                sirve una sola vez.
              </p>

              {tieneCredencial ? (
                <p className="flex items-center gap-2 text-center text-sm text-muted-foreground">
                  <Fingerprint className="size-4 shrink-0" />
                  Tras escanear se te pedirá confirmar con tu dispositivo.
                </p>
              ) : (
                <p className="text-center text-sm text-muted-foreground">
                  No has registrado ningún dispositivo, así que esta checada quedará pendiente
                  de revisión.{' '}
                  <Link to="/perfil" className="underline underline-offset-4">
                    Registrar uno
                  </Link>
                </p>
              )}

              <Button size="lg" onClick={() => void escanear()}>
                Abrir cámara
              </Button>
            </>
          )}

          {estado.fase === 'escaneando' && (
            <>
              <p className="text-sm text-muted-foreground">Apunta al código de la pantalla…</p>
              <Button variant="outline" onClick={() => void detener().then(() => setEstado({ fase: 'inicio' }))}>
                <CameraOff />
                Cancelar
              </Button>
            </>
          )}

          {estado.fase === 'firmando' && (
            <p className="flex items-center gap-2 text-muted-foreground">
              <Fingerprint className="size-4" />
              Confirma con tu huella, tu rostro o tu PIN…
            </p>
          )}

          {estado.fase === 'enviando' && <p className="text-muted-foreground">Registrando…</p>}

          {estado.fase === 'error' && (
            <>
              <Alert variant="destructive">
                <TriangleAlert />
                <AlertTitle>No se registró el fichaje</AlertTitle>
                <AlertDescription>{estado.mensaje}</AlertDescription>
              </Alert>

              {estado.recuperable && (
                <Button onClick={() => void escanear()}>Volver a escanear</Button>
              )}
            </>
          )}

          {estado.fase === 'listo' && (
            <>
              <ResumenChecada checada={estado.checada} />

              <div className="flex gap-3">
                <Button variant="outline" onClick={() => setEstado({ fase: 'inicio' })}>
                  Fichar de nuevo
                </Button>
                <Button render={<Link to="/mis-checadas" />}>Ver mi historial</Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
