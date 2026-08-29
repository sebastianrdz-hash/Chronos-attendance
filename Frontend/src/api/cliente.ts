import type {
  AsientoBitacora,
  Asistencia,
  AsistenciaDelDia,
  Checada,
  ChecadaPorRevisar,
  CodigoKiosco,
  Credencial,
  Departamento,
  Empleado,
  MiPerfil,
  MotivoRechazoQr,
  OpcionesAutenticacion,
  OpcionesEnrolamiento,
  ParametrosLista,
  PerfilUsuario,
  PoliticaConfianza,
  ProblemDetails,
  RechazoChecada,
  RespuestaAltaEmpleado,
  RespuestaLogin,
  ResultadoPaginado,
  Sede,
  TipoChecada,
  Turno,
} from './tipos'

const BASE = import.meta.env.VITE_API_URL ?? '/api/v1'

export class ErrorApi extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ErrorApi'
    this.status = status
  }
}

/**
 * Errores de validación devueltos por el servidor, ya agrupados por campo. El
 * formulario los inyecta en react-hook-form para pintarlos junto al input que
 * corresponde, en vez de mostrar un mensaje suelto arriba.
 */
export class ErrorValidacion extends ErrorApi {
  readonly errores: Record<string, string[]>

  constructor(message: string, errores: Record<string, string[]>) {
    super(message, 400)
    this.name = 'ErrorValidacion'
    this.errores = errores
  }
}

/**
 * Un fichaje que el servidor no aceptó. Conserva el motivo del dominio porque cada caso
 * pide una reacción distinta de quien está frente a la cámara.
 */
export class ErrorFichaje extends ErrorApi {
  readonly motivo: MotivoRechazoQr

  constructor(rechazo: RechazoChecada) {
    super(rechazo.mensaje, 422)
    this.name = 'ErrorFichaje'
    this.motivo = rechazo.motivo
  }
}

let tokenActual: string | null = null

/** El proveedor de autenticación lo actualiza al iniciar y cerrar sesión. */
export function establecerToken(token: string | null) {
  tokenActual = token
}

async function pedir<T>(ruta: string, init: RequestInit = {}): Promise<T> {
  const cabeceras = new Headers(init.headers)
  cabeceras.set('Accept', 'application/json')

  if (init.body) {
    cabeceras.set('Content-Type', 'application/json')
  }

  if (tokenActual) {
    cabeceras.set('Authorization', `Bearer ${tokenActual}`)
  }

  let respuesta: Response

  try {
    respuesta = await fetch(`${BASE}${ruta}`, { ...init, headers: cabeceras })
  } catch {
    throw new ErrorApi('No se pudo contactar al servidor. ¿Está corriendo la API?', 0)
  }

  if (!respuesta.ok) {
    throw await construirError(respuesta)
  }

  return respuesta.status === 204 ? (undefined as T) : ((await respuesta.json()) as T)
}

async function construirError(respuesta: Response): Promise<ErrorApi> {
  let problema: ProblemDetails

  try {
    problema = (await respuesta.json()) as ProblemDetails
  } catch {
    return new ErrorApi(`Error ${respuesta.status}`, respuesta.status)
  }

  // El rechazo de un fichaje no es un ProblemDetails: trae el motivo del dominio para que
  // la pantalla pueda distinguir un código vencido —que se arregla volviendo a escanear—
  // de una firma inválida, que no.
  const posibleRechazo = problema as unknown as RechazoChecada
  if (posibleRechazo.motivo && posibleRechazo.mensaje) {
    return new ErrorFichaje(posibleRechazo)
  }

  if (problema.errors && Object.keys(problema.errors).length > 0) {
    return new ErrorValidacion(
      problema.detail ?? problema.title ?? 'Revisa los datos del formulario.',
      problema.errors,
    )
  }

  return new ErrorApi(
    problema.detail ?? problema.title ?? `Error ${respuesta.status}`,
    respuesta.status,
  )
}

/** Omite los parámetros vacíos para no ensuciar la URL con `buscar=`. */
function consulta(parametros: object = {}): string {
  const query = new URLSearchParams()

  for (const [clave, valor] of Object.entries(parametros)) {
    if (valor !== undefined && valor !== null && valor !== '') {
      query.set(clave, String(valor))
    }
  }

  const texto = query.toString()
  return texto ? `?${texto}` : ''
}

const json = (cuerpo: unknown) => JSON.stringify(cuerpo)

function recurso<TEntidad, TSolicitud>(ruta: string) {
  return {
    listar: (parametros?: ParametrosLista) =>
      pedir<ResultadoPaginado<TEntidad>>(`${ruta}${consulta(parametros)}`),
    obtener: (id: string) => pedir<TEntidad>(`${ruta}/${id}`),
    crear: (cuerpo: TSolicitud) => pedir<TEntidad>(ruta, { method: 'POST', body: json(cuerpo) }),
    actualizar: (id: string, cuerpo: TSolicitud) =>
      pedir<TEntidad>(`${ruta}/${id}`, { method: 'PUT', body: json(cuerpo) }),
    desactivar: (id: string) => pedir<void>(`${ruta}/${id}`, { method: 'DELETE' }),
  }
}

export interface SolicitudSede {
  nombre: string
  codigo: string
  direccion?: string | null
  zonaHoraria: string
  latitud?: number | null
  longitud?: number | null
  radioMetros?: number | null
  activa: boolean
}

export interface SolicitudDepartamento {
  nombre: string
  codigo: string
  sedeId: string
  activo: boolean
}

export interface SolicitudTurno {
  nombre: string
  horaEntrada: string
  horaSalida: string
  toleranciaMinutos: number
  minutosDescanso: number
  diasLaborales: string[]
  activo: boolean
}

export interface SolicitudEmpleado {
  numeroEmpleado: string
  nombres: string
  apellidoPaterno: string
  apellidoMaterno?: string | null
  correoCorporativo: string
  puesto?: string | null
  fechaIngreso: string
  departamentoId: string
  sedeId: string
  turnoId?: string | null
  rol: string
  activo?: boolean
}

export const api = {
  iniciarSesion: (correo: string, contrasena: string) =>
    pedir<RespuestaLogin>('/auth/login', {
      method: 'POST',
      body: json({ correo, contrasena }),
    }),

  perfil: () => pedir<PerfilUsuario>('/auth/yo'),

  politicaConfianza: () => pedir<PoliticaConfianza>('/meta/politica-confianza'),

  sedes: recurso<Sede, SolicitudSede>('/sedes'),
  departamentos: recurso<Departamento, SolicitudDepartamento>('/departamentos'),
  turnos: recurso<Turno, SolicitudTurno>('/turnos'),

  empleados: {
    ...recurso<Empleado, SolicitudEmpleado>('/empleados'),

    // El alta devuelve además la contraseña temporal, así que no encaja en el CRUD genérico.
    crearConCuenta: (cuerpo: SolicitudEmpleado) =>
      pedir<RespuestaAltaEmpleado>('/empleados', { method: 'POST', body: json(cuerpo) }),

    darDeBaja: (id: string) => pedir<Empleado>(`/empleados/${id}`, { method: 'DELETE' }),

    reactivar: (id: string) => pedir<Empleado>(`/empleados/${id}/reactivar`, { method: 'POST' }),

    reiniciarAcceso: (id: string) =>
      pedir<RespuestaAltaEmpleado>(`/empleados/${id}/reiniciar-acceso`, { method: 'POST' }),
  },

  kiosco: {
    codigo: (sedeId: string) => pedir<CodigoKiosco>(`/kiosco/${sedeId}/codigo`),
  },

  checadas: {
    porQr: (token: string, tipo?: TipoChecada, asercion?: unknown) =>
      pedir<Checada>('/checadas/qr', { method: 'POST', body: json({ token, tipo, asercion }) }),

    mias: (parametros?: ParametrosLista) =>
      pedir<ResultadoPaginado<Checada>>(`/checadas/mias${consulta(parametros)}`),
  },

  webauthn: {
    opcionesEnrolamiento: (nombreAmigable: string) =>
      pedir<{ opciones: OpcionesEnrolamiento }>('/webauthn/enrolamiento/opciones', {
        method: 'POST',
        body: json({ nombreAmigable }),
      }),

    completarEnrolamiento: (respuesta: unknown) =>
      pedir<Credencial>('/webauthn/enrolamiento', {
        method: 'POST',
        body: json({ respuesta }),
      }),

    opcionesAutenticacion: () =>
      pedir<{ opciones: OpcionesAutenticacion }>('/webauthn/autenticacion/opciones', {
        method: 'POST',
      }),

    credenciales: () => pedir<Credencial[]>('/webauthn/credenciales'),

    revocar: (id: string) => pedir<void>(`/webauthn/credenciales/${id}`, { method: 'DELETE' }),
  },

  revision: {
    pendientes: (parametros?: ParametrosLista) =>
      pedir<ResultadoPaginado<ChecadaPorRevisar>>(`/revision/pendientes${consulta(parametros)}`),

    aprobar: (id: string, motivo: string) =>
      pedir<ChecadaPorRevisar>(`/revision/${id}/aprobar`, { method: 'POST', body: json({ motivo }) }),

    rechazar: (id: string, motivo: string) =>
      pedir<ChecadaPorRevisar>(`/revision/${id}/rechazar`, { method: 'POST', body: json({ motivo }) }),

    bitacora: (parametros?: ParametrosLista) =>
      pedir<ResultadoPaginado<AsientoBitacora>>(`/revision/bitacora${consulta(parametros)}`),
  },

  asistencia: {
    delDia: (filtros: { fecha?: string; sedeId?: string; departamentoId?: string } = {}) =>
      pedir<Asistencia>(`/asistencia/dia${consulta(filtros)}`),

    mia: (desde?: string, hasta?: string) =>
      pedir<AsistenciaDelDia[]>(`/asistencia/mia${consulta({ desde, hasta })}`),
  },

  miPerfil: () => pedir<MiPerfil>('/perfil'),

  cambiarContrasena: (cuerpo: {
    contrasenaActual: string
    contrasenaNueva: string
    confirmacion: string
  }) => pedir<void>('/perfil/contrasena', { method: 'POST', body: json(cuerpo) }),
}
