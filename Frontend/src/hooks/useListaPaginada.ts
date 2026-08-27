import type { ParametrosLista, ResultadoPaginado } from '@/api/tipos'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

const VACIO: ParametrosLista = { pagina: 1, tamano: 10 }

/**
 * Estado de una tabla: filtros, paginación, carga y recarga. Al cambiar cualquier filtro
 * se vuelve a la página 1, porque conservar la página con otro filtro suele dejar la
 * tabla vacía sin explicación.
 */
export function useListaPaginada<T>(
  consultar: (parametros: ParametrosLista) => Promise<ResultadoPaginado<T>>,
  inicial: ParametrosLista = {},
) {
  const [parametros, setParametros] = useState<ParametrosLista>({ ...VACIO, ...inicial })
  const [datos, setDatos] = useState<ResultadoPaginado<T> | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // La función de consulta suele venir en línea desde el componente; guardarla en una ref
  // evita que su nueva identidad en cada render dispare una recarga infinita. El efecto que
  // la actualiza va declarado antes que el de carga para que este siempre vea la última.
  const consultarRef = useRef(consultar)

  useEffect(() => {
    consultarRef.current = consultar
  }, [consultar])

  const [ciclo, setCiclo] = useState(0)
  const recargar = useCallback(() => setCiclo((n) => n + 1), [])

  useEffect(() => {
    let cancelado = false
    setCargando(true)

    consultarRef
      .current(parametros)
      .then((resultado) => {
        if (cancelado) return
        setDatos(resultado)
        setError(null)
      })
      .catch((fallo: unknown) => {
        if (cancelado) return
        setError(fallo instanceof Error ? fallo.message : 'No se pudo cargar la información.')
      })
      .finally(() => {
        if (!cancelado) setCargando(false)
      })

    return () => {
      cancelado = true
    }
  }, [parametros, ciclo])

  const filtrar = useCallback((cambios: Partial<ParametrosLista>) => {
    setParametros((previos) => ({ ...previos, ...cambios, pagina: 1 }))
  }, [])

  const irAPagina = useCallback((pagina: number) => {
    setParametros((previos) => ({ ...previos, pagina }))
  }, [])

  const ordenar = useCallback((campo: string) => {
    setParametros((previos) => ({
      ...previos,
      ordenarPor: campo,
      descendente: previos.ordenarPor === campo ? !previos.descendente : false,
      pagina: 1,
    }))
  }, [])

  return useMemo(
    () => ({
      parametros,
      elementos: datos?.elementos ?? [],
      total: datos?.total ?? 0,
      totalPaginas: datos?.totalPaginas ?? 0,
      hayPaginaSiguiente: datos?.hayPaginaSiguiente ?? false,
      hayPaginaAnterior: datos?.hayPaginaAnterior ?? false,
      cargando,
      error,
      filtrar,
      irAPagina,
      ordenar,
      recargar,
    }),
    [parametros, datos, cargando, error, filtrar, irAPagina, ordenar, recargar],
  )
}
