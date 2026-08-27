import type { PerfilUsuario } from '@/api/tipos'

const CLAVE = 'chronos.sesion'

export interface Sesion {
  accessToken: string
  expiraUtc: string
  usuario: PerfilUsuario
}

/**
 * El token vive en localStorage por simplicidad del prototipo. En producción
 * conviene un refresh token en cookie httpOnly para que un XSS no pueda leerlo;
 * queda anotado como deuda técnica consciente en el README.
 */
export const almacenSesion = {
  leer(): Sesion | null {
    const crudo = localStorage.getItem(CLAVE)
    if (!crudo) return null

    try {
      const sesion = JSON.parse(crudo) as Sesion
      return this.vigente(sesion) ? sesion : null
    } catch {
      return null
    }
  },

  guardar(sesion: Sesion) {
    localStorage.setItem(CLAVE, JSON.stringify(sesion))
  },

  limpiar() {
    localStorage.removeItem(CLAVE)
  },

  vigente(sesion: Sesion) {
    return new Date(sesion.expiraUtc).getTime() > Date.now()
  },
}
