using System.ComponentModel.DataAnnotations;
using Chronos.Domain.Enums;
using Fido2NetLib;

namespace Chronos.Api.Contratos;

/// <param name="ImagenPng">
/// PNG en base64, listo para un <c>src</c> de imagen. Viaja incrustado en la respuesta y
/// no como una URL aparte para que el kiosco reciba el código y su vigencia en un solo
/// viaje: si fueran dos peticiones, la imagen podría llegar de un token distinto al de la
/// vigencia que está mostrando.
/// </param>
public sealed record CodigoKioscoDto(
    string Token,
    string ImagenPng,
    DateTimeOffset EmitidoUtc,
    DateTimeOffset ExpiraUtc,
    int SegundosRefresco,
    Guid SedeId,
    string SedeNombre);

/// <param name="Tipo">
/// Si no se manda, el servidor alterna a partir del último fichaje del día. Dejarlo
/// explícito permite corregir cuando alguien olvidó marcar la salida anterior.
/// </param>
public sealed record SolicitudChecadaQr
{
    [Required(ErrorMessage = "Falta el código escaneado.")]
    public string Token { get; init; } = string.Empty;

    public TipoChecada? Tipo { get; init; }

    /// <summary>
    /// Firma del desafío WebAuthn, si el dispositivo pudo aportarla. Es opcional a
    /// propósito: un empleado sin credencial enrolada, o con el sensor averiado, debe
    /// poder fichar igual. Su checada valdrá menos y quedará marcada para revisión, que es
    /// exactamente lo que el modelo de señales pretende expresar.
    /// </summary>
    public AuthenticatorAssertionRawResponse? Asercion { get; init; }
}

public sealed record SenalDto(
    TipoSenal Tipo,
    string TipoNombre,
    ResultadoSenal Resultado,
    int PesoAplicado,
    DateTimeOffset CapturadaUtc,
    string? DetalleJson);

public sealed record ChecadaDto(
    Guid Id,
    TipoChecada Tipo,
    DateTimeOffset MomentoUtc,
    DateOnly DiaLaboral,
    EstadoChecada Estado,
    int PuntajeConfianza,
    NivelConfianza NivelConfianza,
    Guid? SedeId,
    string? SedeNombre,
    string? Observaciones,
    IReadOnlyList<SenalDto> Senales);

/// <summary>
/// Detalle de por qué no se aceptó un fichaje. Se devuelve el motivo del dominio además
/// del mensaje para que el cliente pueda reaccionar distinto a cada caso: un código
/// caducado se resuelve volviendo a escanear, una firma inválida no.
/// </summary>
public sealed record RechazoChecadaDto(MotivoRechazoQr Motivo, string Mensaje);
