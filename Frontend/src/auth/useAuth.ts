import { ContextoAuth } from '@/auth/contexto'
import { useContext } from 'react'

/**
 * El nombre va en inglés porque React exige que todo hook empiece con "use";
 * es requisito de las reglas de hooks, no una inconsistencia de estilo.
 */
export function useAuth() {
  const contexto = useContext(ContextoAuth)

  if (!contexto) {
    throw new Error('useAuth debe usarse dentro de <ProveedorAuth>.')
  }

  return contexto
}
