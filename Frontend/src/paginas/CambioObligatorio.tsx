import { useAuth } from '@/auth/useAuth'
import { MarcaChronos } from '@/componentes/MarcaChronos'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { KeyRound, LogOut } from 'lucide-react'
import { Navigate } from 'react-router-dom'
import { FormularioContrasena } from './MiPerfil'

/**
 * Pantalla sin navegación: mientras la cuenta traiga contraseña temporal, `RutaProtegida`
 * empuja aquí cualquier otra ruta. Al cambiarla se refresca el perfil y el candado cae.
 */
export function CambioObligatorio() {
  const { usuario, refrescarPerfil, cerrarSesion } = useAuth()

  if (!usuario) return null
  if (!usuario.debeCambiarContrasena) return <Navigate to="/panel" replace />

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/30 p-6">
      <div className="w-full max-w-md space-y-6">
        <MarcaChronos className="justify-center" />

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <KeyRound className="size-5 text-primary" />
              Cambia tu contraseña
            </CardTitle>
            <CardDescription>
              Entraste con una contraseña temporal, {usuario.nombre.split(' ')[0]}. Define una
              propia para continuar.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Alert>
              <AlertDescription>
                Quien te dio de alta conoce la contraseña temporal. Hasta que la cambies, nadie
                puede afirmar que las checadas hechas con tu cuenta son tuyas.
              </AlertDescription>
            </Alert>

            <FormularioContrasena alCambiar={refrescarPerfil} />
          </CardContent>
        </Card>

        <Button variant="ghost" size="sm" className="w-full" onClick={cerrarSesion}>
          <LogOut className="size-4" />
          Salir sin cambiarla
        </Button>
      </div>
    </div>
  )
}
