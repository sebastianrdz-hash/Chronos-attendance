using System.ComponentModel.DataAnnotations;
using Fido2NetLib;

namespace Chronos.Api.Contratos;

public sealed record SolicitudOpcionesEnrolamiento
{
    /// <summary>
    /// Cómo llamará el empleado a este dispositivo en su lista. Sin un nombre, revocar la
    /// credencial correcta entre varias sería adivinar.
    /// </summary>
    [Required(ErrorMessage = "Ponle un nombre al dispositivo.")]
    [StringLength(120, MinimumLength = 2)]
    public string NombreAmigable { get; init; } = string.Empty;
}

/// <param name="Opciones">
/// Las opciones tal como las define la norma WebAuthn. El cliente se las pasa casi tal
/// cual a <c>navigator.credentials.create()</c>.
/// </param>
public sealed record RespuestaOpcionesEnrolamiento(CredentialCreateOptions Opciones);

public sealed record SolicitudEnrolamiento
{
    [Required]
    public AuthenticatorAttestationRawResponse Respuesta { get; init; } = null!;
}

public sealed record CredencialDto(
    Guid Id,
    string? NombreAmigable,
    string? TipoDispositivo,
    DateTimeOffset CreadoUtc,
    DateTimeOffset? UltimoUsoUtc,
    long ContadorFirmas,
    bool Activa);

public sealed record RespuestaOpcionesAutenticacion(AssertionOptions Opciones);
