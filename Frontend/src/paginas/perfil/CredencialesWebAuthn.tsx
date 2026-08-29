import { ErrorApi } from '@/api/cliente'
import { api } from '@/api/cliente'
import type { Credencial } from '@/api/tipos'
import { enrolar, motivoNoDisponible } from '@/api/webauthn'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Fingerprint, Loader2, Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'

/**
 * Alta y baja de los dispositivos con los que el empleado confirma que es él al fichar.
 *
 * Conviene que el texto sea explícito sobre qué se guarda: la palabra «biometría» hace
 * pensar a mucha gente que la empresa se queda con su huella, y aquí lo único que viaja
 * al servidor es una clave pública.
 */
export function CredencialesWebAuthn() {
  const [credenciales, setCredenciales] = useState<Credencial[] | null>(null)
  const [nombre, setNombre] = useState('')
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const impedimento = motivoNoDisponible()

  useEffect(() => {
    api
      .webauthn.credenciales()
      .then(setCredenciales)
      .catch(() => setCredenciales([]))
  }, [])

  const agregar = async () => {
    setError(null)
    setOcupado(true)

    try {
      const nueva = await enrolar(nombre.trim())
      setCredenciales((previas) => [...(previas ?? []), nueva])
      setNombre('')
      toast.success('Dispositivo registrado.')
    } catch (fallo) {
      // Cancelar el diálogo del sistema no es un fallo que merezca una alerta roja.
      if (fallo instanceof DOMException && fallo.name === 'NotAllowedError') {
        setError('Se canceló la operación o se agotó el tiempo. Inténtalo otra vez.')
      } else if (fallo instanceof ErrorApi) {
        setError(fallo.message)
      } else {
        setError('No se pudo registrar el dispositivo.')
      }
    } finally {
      setOcupado(false)
    }
  }

  const revocar = async (credencial: Credencial) => {
    try {
      await api.webauthn.revocar(credencial.id)
      setCredenciales((previas) => (previas ?? []).filter((c) => c.id !== credencial.id))
      toast.success('Dispositivo revocado.')
    } catch {
      toast.error('No se pudo revocar el dispositivo.')
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Fingerprint className="size-5" />
          Dispositivos de confianza
        </CardTitle>
        <CardDescription>
          Al fichar, tu huella o tu rostro desbloquean una llave guardada en este aparato. El
          rasgo biométrico nunca sale de él: aquí solo se almacena la clave pública.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {impedimento && (
          <Alert>
            <AlertDescription>{impedimento}</AlertDescription>
          </Alert>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {credenciales === null ? (
          <Skeleton className="h-20 w-full" />
        ) : credenciales.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Todavía no registras ningún dispositivo. Sin uno, tus checadas solo suman la señal
            del código QR y quedan pendientes de revisión.
          </p>
        ) : (
          <ul className="divide-y rounded-lg border">
            {credenciales.map((credencial) => (
              <li key={credencial.id} className="flex items-center justify-between gap-3 p-3">
                <div className="min-w-0">
                  <p className="truncate font-medium">{credencial.nombreAmigable ?? 'Dispositivo'}</p>
                  <p className="text-xs text-muted-foreground">
                    Registrado el {new Date(credencial.creadoUtc).toLocaleDateString('es-MX')}
                    {credencial.ultimoUsoUtc
                      ? ` · Último uso: ${new Date(credencial.ultimoUsoUtc).toLocaleString('es-MX')}`
                      : ' · Sin usar todavía'}
                  </p>
                </div>

                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Revocar ${credencial.nombreAmigable ?? 'dispositivo'}`}
                  onClick={() => void revocar(credencial)}
                >
                  <Trash2 className="size-4" />
                </Button>
              </li>
            ))}
          </ul>
        )}

        {!impedimento && (
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              value={nombre}
              onChange={(evento) => setNombre(evento.target.value)}
              placeholder="Nombre del dispositivo, por ejemplo: mi celular"
              maxLength={120}
              aria-label="Nombre del dispositivo"
            />
            <Button
              onClick={() => void agregar()}
              disabled={ocupado || nombre.trim().length < 2}
              className="shrink-0"
            >
              {ocupado ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />}
              Registrar este aparato
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
