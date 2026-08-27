import { ErrorValidacion } from '@/api/cliente'
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'

/**
 * Traslada los errores del servidor al formulario. La API los devuelve con el nombre de
 * la propiedad de C# (PascalCase) y react-hook-form registra los campos en camelCase,
 * así que hay que bajar la primera letra para que cada mensaje caiga bajo su input.
 *
 * Lo que no corresponde a ningún campo (reglas de negocio como "no puedes darte de baja
 * a ti mismo") se devuelve para mostrarlo como aviso general del formulario.
 */
export function aplicarErroresDelServidor<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  camposConocidos: readonly string[],
): string | null {
  if (!(error instanceof ErrorValidacion)) {
    return error instanceof Error ? error.message : 'Ocurrió un error inesperado.'
  }

  const sueltos: string[] = []

  for (const [campo, mensajes] of Object.entries(error.errores)) {
    const nombre = campo.charAt(0).toLowerCase() + campo.slice(1)
    const texto = mensajes.join(' ')

    if (camposConocidos.includes(nombre)) {
      setError(nombre as Path<T>, { type: 'server', message: texto })
    } else {
      sueltos.push(texto)
    }
  }

  return sueltos.length > 0 ? sueltos.join(' ') : null
}

/** TimeOnly llega como "HH:mm:ss" y el input type="time" espera "HH:mm". */
export function aHoraCorta(hora: string): string {
  return hora.slice(0, 5)
}

export function aHoraLarga(hora: string): string {
  return hora.length === 5 ? `${hora}:00` : hora
}

export function formatearFecha(fecha: string | null | undefined): string {
  if (!fecha) return '—'

  // Las fechas de la API son DateOnly ("2026-08-01"); interpretarlas como UTC evita que
  // el navegador las recorra un día hacia atrás según su zona horaria.
  const fmt = new Intl.DateTimeFormat('es-MX', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  })

  return fmt.format(new Date(`${fecha}T00:00:00Z`))
}
