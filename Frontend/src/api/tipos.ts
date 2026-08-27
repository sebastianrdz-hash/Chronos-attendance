export type Rol = 'Admin' | 'Supervisor' | 'Empleado'

export interface PerfilUsuario {
  id: string
  correo: string
  nombre: string
  roles: Rol[]
  /** Rol efectivo (el de mayor privilegio). Es el que decide la navegación. */
  rol: Rol
  empleadoId: string | null
  numeroEmpleado: string | null
  puesto: string | null
  departamentoId: string | null
  departamento: string | null
  sedeId: string | null
  sede: string | null
  debeCambiarContrasena: boolean
}

export interface RespuestaLogin {
  accessToken: string
  tipoToken: string
  expiraEnSegundos: number
  expiraUtc: string
  usuario: PerfilUsuario
}

export interface DescripcionSenal {
  tipo: string
  peso: number
  prueba: string
  disponibleEnFase1: boolean
}

export interface PoliticaConfianza {
  umbralAlta: number
  umbralMedia: number
  penalizacionFallida: number
  penalizacionSospechosa: number
  senales: DescripcionSenal[]
}

/** Forma de RFC 9457 que devuelve ASP.NET Core en los errores. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export interface ResultadoPaginado<T> {
  elementos: T[]
  pagina: number
  tamano: number
  total: number
  totalPaginas: number
  hayPaginaSiguiente: boolean
  hayPaginaAnterior: boolean
}

export interface ParametrosLista {
  pagina?: number
  tamano?: number
  buscar?: string
  ordenarPor?: string
  descendente?: boolean
  activo?: boolean
  departamentoId?: string
  sedeId?: string
  turnoId?: string
}

// --- Catálogos ---

export interface Sede {
  id: string
  nombre: string
  codigo: string
  direccion: string | null
  zonaHoraria: string
  latitud: number | null
  longitud: number | null
  radioMetros: number | null
  activa: boolean
  totalDepartamentos: number
  totalEmpleados: number
}

export interface Departamento {
  id: string
  nombre: string
  codigo: string
  sedeId: string
  sedeNombre: string
  activo: boolean
  totalEmpleados: number
}

export type DiaSemana =
  | 'Domingo'
  | 'Lunes'
  | 'Martes'
  | 'Miercoles'
  | 'Jueves'
  | 'Viernes'
  | 'Sabado'

export interface Turno {
  id: string
  nombre: string
  /** Formato "HH:mm:ss" tal como serializa TimeOnly. */
  horaEntrada: string
  horaSalida: string
  toleranciaMinutos: number
  minutosDescanso: number
  diasLaborales: DiaSemana[]
  cruzaMedianoche: boolean
  horasProgramadas: number
  activo: boolean
  totalEmpleados: number
}

// --- Empleados ---

export interface Empleado {
  id: string
  numeroEmpleado: string
  nombres: string
  apellidoPaterno: string
  apellidoMaterno: string | null
  nombreCompleto: string
  correoCorporativo: string
  puesto: string | null
  fechaIngreso: string
  fechaBaja: string | null
  activo: boolean
  departamentoId: string
  departamentoNombre: string
  sedeId: string
  sedeNombre: string
  turnoId: string | null
  turnoNombre: string | null
  rol: Rol
  debeCambiarContrasena: boolean
}

export interface RespuestaAltaEmpleado {
  empleado: Empleado
  contrasenaTemporal: string
}

export interface MiPerfil {
  empleado: Empleado
  turno: Turno | null
  correo: string
  ultimoAccesoUtc: string | null
}
