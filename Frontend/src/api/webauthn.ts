import { api } from './cliente'
import type { Credencial } from './tipos'

/**
 * Puente entre la API del navegador y la nuestra.
 *
 * WebAuthn trabaja con ArrayBuffer y el JSON viaja en base64url, así que cada ceremonia
 * necesita traducir campos en ambos sentidos. Se concentra aquí para que las pantallas no
 * tengan que saber cuáles son binarios.
 */

function aBytes(base64url: string): Uint8Array {
  const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/')
  const relleno = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  const crudo = atob(relleno)

  return Uint8Array.from(crudo, (caracter) => caracter.charCodeAt(0))
}

function aTexto(bytes: ArrayBuffer): string {
  const binario = String.fromCharCode(...new Uint8Array(bytes))

  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/**
 * Un contexto inseguro o una IP en la barra de direcciones dejan a WebAuthn fuera de
 * juego, y conviene decirlo antes de abrir un diálogo que fallará con un error opaco.
 */
export function motivoNoDisponible(): string | null {
  if (!window.isSecureContext) {
    return 'WebAuthn necesita HTTPS. Abre la aplicación por https:// o por localhost.'
  }

  if (!window.PublicKeyCredential) {
    return 'Este navegador no admite WebAuthn.'
  }

  // La norma exige que el identificador del sitio sea un nombre de dominio. Una IP nunca
  // lo es, y el navegador responde con un SecurityError que no explica el motivo.
  if (/^\d{1,3}(\.\d{1,3}){3}$/.test(window.location.hostname)) {
    return 'WebAuthn no funciona sobre una dirección IP. Entra por un nombre de dominio o por localhost.'
  }

  return null
}

export async function hayAutenticadorEnEsteAparato(): Promise<boolean> {
  if (motivoNoDisponible()) return false

  try {
    return await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()
  } catch {
    return false
  }
}

/** Da de alta el sensor de este dispositivo como credencial del empleado. */
export async function enrolar(nombreAmigable: string): Promise<Credencial> {
  const { opciones } = await api.webauthn.opcionesEnrolamiento(nombreAmigable)

  // El resto de las opciones se reenvía sin tocar, así que se pasa por `unknown`: son las
  // que dicta la norma y describirlas aquí solo serviría para que se desincronizaran.
  const publicKey = {
    ...opciones,
    challenge: aBytes(opciones.challenge),
    user: { ...opciones.user, id: aBytes(opciones.user.id) },
    excludeCredentials: opciones.excludeCredentials?.map((c) => ({
      ...c,
      id: aBytes(c.id),
    })),
  } as unknown as PublicKeyCredentialCreationOptions

  const credencial = (await navigator.credentials.create({ publicKey })) as PublicKeyCredential | null

  if (!credencial) {
    throw new Error('El navegador no devolvió ninguna credencial.')
  }

  const respuesta = credencial.response as AuthenticatorAttestationResponse

  return api.webauthn.completarEnrolamiento({
    id: credencial.id,
    rawId: aTexto(credencial.rawId),
    type: credencial.type,
    response: {
      attestationObject: aTexto(respuesta.attestationObject),
      clientDataJSON: aTexto(respuesta.clientDataJSON),
    },
    extensions: credencial.getClientExtensionResults(),
  })
}

/**
 * Firma el desafío de fichaje. Devuelve el objeto tal como lo espera la API, listo para
 * viajar junto al token del QR.
 */
export async function firmar(): Promise<unknown> {
  const { opciones } = await api.webauthn.opcionesAutenticacion()

  const publicKey = {
    ...opciones,
    challenge: aBytes(opciones.challenge),
    allowCredentials: opciones.allowCredentials?.map((c) => ({
      ...c,
      id: aBytes(c.id),
    })),
  } as unknown as PublicKeyCredentialRequestOptions

  const credencial = (await navigator.credentials.get({ publicKey })) as PublicKeyCredential | null

  if (!credencial) {
    throw new Error('El navegador no devolvió ninguna firma.')
  }

  const respuesta = credencial.response as AuthenticatorAssertionResponse

  return {
    id: credencial.id,
    rawId: aTexto(credencial.rawId),
    type: credencial.type,
    response: {
      authenticatorData: aTexto(respuesta.authenticatorData),
      clientDataJSON: aTexto(respuesta.clientDataJSON),
      signature: aTexto(respuesta.signature),
      userHandle: respuesta.userHandle ? aTexto(respuesta.userHandle) : null,
    },
    extensions: credencial.getClientExtensionResults(),
  }
}
