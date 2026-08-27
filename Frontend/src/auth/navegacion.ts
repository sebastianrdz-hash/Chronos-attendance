import type { Rol } from '@/api/tipos'
import {
  Building2,
  CalendarClock,
  LayoutDashboard,
  Network,
  UserRound,
  Users,
  type LucideIcon,
} from 'lucide-react'

export interface EntradaNavegacion {
  ruta: string
  etiqueta: string
  icono: LucideIcon
  roles: Rol[]
}

/**
 * Fuente única de la navegación: el menú lateral la recorre para pintarse y el guardián
 * de rutas la consulta para autorizar. Así no puede aparecer un enlace hacia una pantalla
 * a la que el rol no tiene acceso.
 *
 * Esto es comodidad de interfaz, no seguridad: la API vuelve a comprobar cada permiso.
 */
export const navegacion: EntradaNavegacion[] = [
  {
    ruta: '/panel',
    etiqueta: 'Panel',
    icono: LayoutDashboard,
    roles: ['Admin', 'Supervisor', 'Empleado'],
  },
  { ruta: '/empleados', etiqueta: 'Empleados', icono: Users, roles: ['Admin', 'Supervisor'] },
  { ruta: '/departamentos', etiqueta: 'Departamentos', icono: Network, roles: ['Admin', 'Supervisor'] },
  { ruta: '/sedes', etiqueta: 'Sedes', icono: Building2, roles: ['Admin', 'Supervisor'] },
  { ruta: '/turnos', etiqueta: 'Turnos', icono: CalendarClock, roles: ['Admin', 'Supervisor'] },
  {
    ruta: '/perfil',
    etiqueta: 'Mi perfil',
    icono: UserRound,
    roles: ['Admin', 'Supervisor', 'Empleado'],
  },
]

export function navegacionPara(rol: Rol): EntradaNavegacion[] {
  return navegacion.filter((entrada) => entrada.roles.includes(rol))
}

export const etiquetaRol: Record<Rol, string> = {
  Admin: 'Administración',
  Supervisor: 'Supervisión',
  Empleado: 'Empleado',
}
