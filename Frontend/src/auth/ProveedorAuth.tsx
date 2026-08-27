import { api, establecerToken } from '@/api/cliente'
import type { Rol } from '@/api/tipos'
import { almacenSesion, type Sesion } from '@/auth/almacen'
import { ContextoAuth, type ValorAuth } from '@/auth/contexto'
import { useCallback, useEffect, useMemo, useState } from 'react'

function sesionGuardada() {
  const sesion = almacenSesion.leer()
  establecerToken(sesion?.accessToken ?? null)
  return sesion
}

export function ProveedorAuth({ children }: { children: React.ReactNode }) {
  const [sesion, setSesion] = useState<Sesion | null>(sesionGuardada)
  const [cargando, setCargando] = useState(() => almacenSesion.leer() !== null)

  useEffect(() => {
    // Sin sesión guardada no hay nada que revalidar y el arranque ya no está cargando.
    if (!almacenSesion.leer()) return

    let cancelado = false

    // El token pudo quedar huérfano si la base se recreó: se confirma contra la API.
    api
      .perfil()
      .then((usuario) => {
        if (!cancelado) setSesion((previa) => (previa ? { ...previa, usuario } : null))
      })
      .catch(() => {
        if (cancelado) return
        almacenSesion.limpiar()
        establecerToken(null)
        setSesion(null)
      })
      .finally(() => {
        if (!cancelado) setCargando(false)
      })

    return () => {
      cancelado = true
    }
  }, [])

  const iniciarSesion = useCallback(async (correo: string, contrasena: string) => {
    const respuesta = await api.iniciarSesion(correo, contrasena)

    const nueva: Sesion = {
      accessToken: respuesta.accessToken,
      expiraUtc: respuesta.expiraUtc,
      usuario: respuesta.usuario,
    }

    almacenSesion.guardar(nueva)
    establecerToken(nueva.accessToken)
    setSesion(nueva)
  }, [])

  const cerrarSesion = useCallback(() => {
    almacenSesion.limpiar()
    establecerToken(null)
    setSesion(null)
  }, [])

  const refrescarPerfil = useCallback(async () => {
    const usuario = await api.perfil()

    setSesion((previa) => {
      if (!previa) return null

      const actualizada = { ...previa, usuario }
      almacenSesion.guardar(actualizada)
      return actualizada
    })
  }, [])

  const valor = useMemo<ValorAuth>(
    () => ({
      usuario: sesion?.usuario ?? null,
      cargando,
      iniciarSesion,
      cerrarSesion,
      refrescarPerfil,
      tieneRol: (...roles: Rol[]) =>
        roles.some((rol) => sesion?.usuario.roles.includes(rol) ?? false),
    }),
    [sesion, cargando, iniciarSesion, cerrarSesion, refrescarPerfil],
  )

  return <ContextoAuth.Provider value={valor}>{children}</ContextoAuth.Provider>
}
