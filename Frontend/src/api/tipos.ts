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

// --- Fichaje ---

export type TipoChecada = 'Entrada' | 'Salida' | 'InicioDescanso' | 'FinDescanso'

export type EstadoChecada =
  | 'Rechazada'
  | 'RequiereRevision'
  | 'Verificada'
  | 'AjustadaPorSupervisor'

export type NivelConfianza = 'Nula' | 'Baja' | 'Media' | 'Alta'

export type TipoSenal =
  | 'CodigoQr'
  | 'WebAuthn'
  | 'BeaconBle'
  | 'Geocerca'
  | 'RedWifi'
  | 'RegistroManual'

export type ResultadoSenal = 'Confirmada' | 'Fallida' | 'NoDisponible' | 'Sospechosa'

export type MotivoRechazoQr =
  | 'Ninguno'
  | 'FormatoInvalido'
  | 'FirmaInvalida'
  | 'Caducado'
  | 'NonceReusado'
  | 'SedeNoCorresponde'

export interface CodigoKiosco {
  token: string
  /** PNG en base64, sin el prefijo de data URI. */
  imagenPng: string
  emitidoUtc: string
  expiraUtc: string
  segundosRefresco: number
  sedeId: string
  sedeNombre: string
}

export interface Senal {
  tipo: TipoSenal
  tipoNombre: string
  resultado: ResultadoSenal
  pesoAplicado: number
  capturadaUtc: string
  detalleJson: string | null
}

export interface Checada {
  id: string
  tipo: TipoChecada
  momentoUtc: string
  diaLaboral: string
  estado: EstadoChecada
  puntajeConfianza: number
  nivelConfianza: NivelConfianza
  sedeId: string | null
  sedeNombre: string | null
  observaciones: string | null
  senales: Senal[]
}

export interface RechazoChecada {
  motivo: MotivoRechazoQr
  mensaje: string
}

export interface Credencial {
  id: string
  nombreAmigable: string | null
  tipoDispositivo: string | null
  creadoUtc: string
  ultimoUsoUtc: string | null
  contadorFirmas: number
  activa: boolean
}

/**
 * Las opciones de WebAuthn se reenvían casi tal cual al navegador. Solo se declaran los
 * campos que hay que traducir de base64url a binario; el resto viaja intacto y tiparlo
 * aquí sería duplicar la norma sin ganar nada.
 */
export interface OpcionesEnrolamiento {
  challenge: string
  user: { id: string; name: string; displayName: string }
  excludeCredentials?: { id: string; type: string }[]
}

export interface OpcionesAutenticacion {
  challenge: string
  allowCredentials?: { id: string; type: string }[]
}

// --- Revisión y bitácora ---

export interface ChecadaPorRevisar {
  id: string
  empleadoId: string
  empleadoNombre: string
  numeroEmpleado: string
  departamentoNombre: string
  tipo: TipoChecada
  momentoUtc: string
  diaLaboral: string
  estado: EstadoChecada
  puntajeConfianza: number
  nivelConfianza: NivelConfianza
  sedeNombre: string | null
  senales: Senal[]
}

export type AccionAuditada =
  | 'ChecadaAprobada'
  | 'ChecadaRechazada'
  | 'CredencialRevocada'
  | 'EmpleadoDadoDeAlta'
  | 'EmpleadoDadoDeBaja'
  | 'AccesoReiniciado'

export interface AsientoBitacora {
  id: string
  ocurridoUtc: string
  accion: AccionAuditada
  accionNombre: string
  entidad: string
  entidadId: string | null
  usuarioCorreo: string | null
  motivo: string | null
}

// --- Asistencia ---

export type EstadoAsistencia =
  | 'Descanso'
  | 'Completa'
  | 'Retardo'
  | 'SalidaAnticipada'
  | 'JornadaIncompleta'
  | 'Falta'

export interface AsistenciaDelDia {
  empleadoId: string
  nombreCompleto: string
  numeroEmpleado: string
  departamentoNombre: string
  sedeNombre: string
  turnoNombre: string | null
  estado: EstadoAsistencia
  estadoNombre: string
  entrada: string | null
  salida: string | null
  horasTrabajadas: number
  horasExtra: number
  minutosRetardo: number
  minutosSalidaAnticipada: number
  requiereRevision: boolean
  confianzaMinima: NivelConfianza
}

export interface ResumenAsistencia {
  dia: string
  plantilla: number
  presentes: number
  faltas: number
  retardos: number
  jornadasIncompletas: number
  pendientesDeRevision: number
  horasTrabajadas: number
  horasExtra: number
}

export interface Asistencia {
  resumen: ResumenAsistencia
  empleados: AsistenciaDelDia[]
}
