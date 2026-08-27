import { ErrorApi } from '@/api/cliente'
import { LogotipoChronos } from '@/componentes/MarcaChronos'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/auth/useAuth'
import { AlertCircle, Fingerprint, Loader2, QrCode, Radio, ShieldCheck } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'

const senales = [
  { icono: QrCode, titulo: 'Código QR', texto: 'Prueba que se tenía la credencial vigente.' },
  { icono: Fingerprint, titulo: 'WebAuthn', texto: 'Prueba el dispositivo y la biometría del empleado.' },
  { icono: Radio, titulo: 'Beacon BLE', texto: 'Corrobora la presencia física en la zona.' },
  { icono: ShieldCheck, titulo: 'Geocerca', texto: 'Verificación gruesa por coordenadas.' },
]

export function PaginaLogin() {
  const { usuario, iniciarSesion } = useAuth()
  const navegar = useNavigate()
  const ubicacion = useLocation()

  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (usuario) {
    return <Navigate to="/panel" replace />
  }

  const destino = (ubicacion.state as { desde?: string } | null)?.desde ?? '/panel'

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()
    setError(null)
    setEnviando(true)

    try {
      await iniciarSesion(correo, contrasena)
      navegar(destino, { replace: true })
    } catch (fallo) {
      setError(
        fallo instanceof ErrorApi ? fallo.message : 'Ocurrió un error inesperado al iniciar sesión.',
      )
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="grid min-h-svh lg:grid-cols-[1.1fr_1fr]">
      <aside className="relative hidden flex-col justify-between overflow-hidden bg-slate-950 p-12 text-slate-100 lg:flex">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-32 -left-24 size-[28rem] rounded-full bg-indigo-500/20 blur-3xl"
        />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-32 -bottom-40 size-[32rem] rounded-full bg-sky-500/10 blur-3xl"
        />

        <div className="relative flex items-center gap-3">
          <LogotipoChronos className="size-9 text-indigo-400" />
          <span className="text-xl font-semibold tracking-tight">Chronos</span>
        </div>

        <div className="relative max-w-lg space-y-8">
          <div className="space-y-4">
            <h1 className="text-4xl font-semibold tracking-tight text-balance">
              Una checada no es un botón que dice «presente».
            </h1>
            <p className="text-lg leading-relaxed text-slate-400 text-pretty">
              Cada fichaje acumula señales independientes. Defraudar el sistema exige vencer
              varias a la vez, y lo que llega débil se marca para revisión de Recursos Humanos.
            </p>
          </div>

          <ul className="grid gap-3 sm:grid-cols-2">
            {senales.map(({ icono: Icono, titulo, texto }) => (
              <li
                key={titulo}
                className="rounded-xl border border-white/10 bg-white/5 p-4 backdrop-blur-sm"
              >
                <Icono className="mb-2 size-5 text-indigo-400" aria-hidden="true" />
                <p className="text-sm font-medium text-slate-100">{titulo}</p>
                <p className="mt-1 text-xs leading-relaxed text-slate-400">{texto}</p>
              </li>
            ))}
          </ul>
        </div>

        <p className="relative text-xs text-slate-500">
          Los datos biométricos nunca salen del dispositivo: WebAuthn solo comparte una clave pública.
        </p>
      </aside>

      <main className="flex items-center justify-center px-6 py-12">
        <div className="w-full max-w-sm space-y-8">
          <div className="space-y-2">
            <div className="flex items-center gap-2 lg:hidden">
              <LogotipoChronos className="size-7 text-indigo-600" />
              <span className="text-lg font-semibold tracking-tight">Chronos</span>
            </div>
            <h2 className="text-2xl font-semibold tracking-tight">Iniciar sesión</h2>
            <p className="text-sm text-muted-foreground">
              Usa tu correo corporativo para entrar al panel.
            </p>
          </div>

          <form onSubmit={alEnviar} className="space-y-5" noValidate>
            <div className="space-y-2">
              <Label htmlFor="correo">Correo corporativo</Label>
              <Input
                id="correo"
                type="email"
                autoComplete="username"
                placeholder="nombre@chronos.mx"
                value={correo}
                onChange={(e) => setCorreo(e.target.value)}
                required
                autoFocus
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="contrasena">Contraseña</Label>
              <Input
                id="contrasena"
                type="password"
                autoComplete="current-password"
                value={contrasena}
                onChange={(e) => setContrasena(e.target.value)}
                required
              />
            </div>

            {error && (
              <Alert variant="destructive" role="alert">
                <AlertCircle className="size-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <Button type="submit" className="w-full" disabled={enviando}>
              {enviando && <Loader2 className="size-4 animate-spin" aria-hidden="true" />}
              {enviando ? 'Verificando…' : 'Entrar'}
            </Button>
          </form>

          <div className="rounded-lg border bg-muted/40 p-4">
            <p className="text-xs font-medium text-foreground">Cuentas de demostración</p>
            <dl className="mt-2 space-y-1 text-xs text-muted-foreground">
              {[
                ['Admin', 'admin@chronos.mx'],
                ['Supervisor', 'supervisor@chronos.mx'],
                ['Empleado', 'empleado@chronos.mx'],
              ].map(([rol, cuenta]) => (
                <div key={cuenta} className="flex items-center justify-between gap-4">
                  <dt>{rol}</dt>
                  <dd>
                    <button
                      type="button"
                      onClick={() => setCorreo(cuenta)}
                      className="font-mono underline-offset-2 hover:underline"
                    >
                      {cuenta}
                    </button>
                  </dd>
                </div>
              ))}
              <p className="pt-1">
                Contraseña sembrada: <code className="font-mono">Chronos#2026</code>
              </p>
            </dl>
          </div>
        </div>
      </main>
    </div>
  )
}
