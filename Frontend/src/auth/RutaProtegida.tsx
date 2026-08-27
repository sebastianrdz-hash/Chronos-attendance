import type { Rol } from '@/api/tipos'
import { useAuth } from '@/auth/useAuth'
import { Loader2 } from 'lucide-react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'

function Esperando() {
  return (
    <div className="flex min-h-svh items-center justify-center">
      <Loader2 className="size-6 animate-spin text-muted-foreground" aria-label="Cargando" />
    </div>
  )
}

export function RutaProtegida({ roles }: { roles?: Rol[] }) {
  const { usuario, cargando } = useAuth()
  const ubicacion = useLocation()

  if (cargando) return <Esperando />

  if (!usuario) {
    return <Navigate to="/login" replace state={{ desde: ubicacion.pathname }} />
  }

  // Una contraseña temporal solo sirve para cambiarla: hasta entonces no se llega a
  // ninguna otra pantalla, ni escribiendo la URL a mano.
  if (usuario.debeCambiarContrasena && ubicacion.pathname !== '/cambiar-contrasena') {
    return <Navigate to="/cambiar-contrasena" replace />
  }

  if (roles && !roles.includes(usuario.rol)) {
    return <Navigate to="/panel" replace />
  }

  return <Outlet />
}
