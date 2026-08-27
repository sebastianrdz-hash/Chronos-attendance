import type { PerfilUsuario, Rol } from '@/api/tipos'
import { createContext } from 'react'

export interface ValorAuth {
  usuario: PerfilUsuario | null
  cargando: boolean
  iniciarSesion: (correo: string, contrasena: string) => Promise<void>
  cerrarSesion: () => void
  /** Vuelve a leer el perfil del servidor; el cambio de contraseña lo usa para
   *  levantar la marca de cambio obligatorio sin obligar a reiniciar sesión. */
  refrescarPerfil: () => Promise<void>
  tieneRol: (...roles: Rol[]) => boolean
}

/** Vive aparte del proveedor para no romper el fast refresh de Vite. */
export const ContextoAuth = createContext<ValorAuth | null>(null)
