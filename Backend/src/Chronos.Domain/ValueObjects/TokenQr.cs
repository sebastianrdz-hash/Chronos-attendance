namespace Chronos.Domain.ValueObjects;

/// <summary>
/// Contenido de un código QR de kiosco. Lo emite el servidor firmado y el empleado lo
/// escanea con su teléfono; nunca viaja al revés.
/// </summary>
/// <param name="SedeId">Sede cuyo kiosco mostró el código.</param>
/// <param name="Nonce">
/// Identificador único de esta emisión. Es lo que permite detectar que un mismo código
/// se está usando dos veces: la firma prueba que el token es auténtico, pero no que sea
/// la primera vez que se presenta.
/// </param>
/// <param name="EmitidoUtc">Momento en que el kiosco pidió el código.</param>
/// <param name="ExpiraUtc">Momento a partir del cual deja de valer.</param>
public readonly record struct TokenQr(
    Guid SedeId,
    Guid Nonce,
    DateTimeOffset EmitidoUtc,
    DateTimeOffset ExpiraUtc);
