namespace Chronos.Domain.Reglas;

/// <summary>
/// Cuánto vive un código de kiosco. Es el parámetro que decide qué tan caro sale
/// defraudar por esta vía.
/// </summary>
public sealed class PoliticaQr
{
    public static PoliticaQr Predeterminada { get; } = new();

    /// <summary>
    /// Ventana de validez del token. Corta a propósito: es exactamente el tiempo que
    /// tendría un empleado para fotografiar el código de la pantalla y mandárselo a un
    /// compañero por mensajería. Alargarla hace el sistema más cómodo y más fácil de burlar.
    /// </summary>
    public TimeSpan Vigencia { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Margen extra al validar. No corrige relojes desfasados —el mismo servidor firma y
    /// verifica— sino que absorbe el viaje entre que la cámara decodifica el código y la
    /// petición llega: sin él, escanear en el último segundo fallaría por la latencia.
    /// </summary>
    public TimeSpan Gracia { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cada cuánto conviene que el kiosco pida un código nuevo. Se deja por debajo de la
    /// vigencia para que en pantalla nunca haya uno a punto de morir.
    /// </summary>
    public TimeSpan Refresco => Vigencia - TimeSpan.FromSeconds(10) is { Ticks: > 0 } holgura
        ? holgura
        : Vigencia;
}
