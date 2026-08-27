import { useAuth } from '@/auth/useAuth'
import { PanelAdmin } from './paneles/PanelAdmin'
import { PanelEmpleado } from './paneles/PanelEmpleado'
import { PanelSupervisor } from './paneles/PanelSupervisor'

/**
 * Una sola ruta, tres pantallas. Repartir aquí evita duplicar `/panel-admin`,
 * `/panel-supervisor` y demás, y que un enlace compartido lleve a un panel que no
 * corresponde a quien lo abre.
 */
export function Panel() {
  const { usuario } = useAuth()

  if (!usuario) return null

  switch (usuario.rol) {
    case 'Admin':
      return <PanelAdmin nombre={usuario.nombre.split(' ')[0]} />
    case 'Supervisor':
      return <PanelSupervisor usuario={usuario} />
    default:
      return <PanelEmpleado usuario={usuario} />
  }
}
