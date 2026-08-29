using Chronos.Domain.Enums;

namespace Chronos.Domain.Reglas;

/// <summary>
/// Reglas de cuándo un fichaje es aceptable con independencia de las señales que traiga.
/// </summary>
public sealed class PoliticaFichaje
{
    public static PoliticaFichaje Predeterminada { get; } = new();

    /// <summary>
    /// Tiempo mínimo entre dos fichajes del mismo tipo. El nonce ya impide reutilizar un
    /// código, pero el kiosco emite uno nuevo cada pocos segundos: sin esta ventana, un
    /// empleado parado frente a la pantalla registraría varias entradas seguidas y el
    /// cálculo de jornada tendría que adivinar cuál vale.
    /// </summary>
    public TimeSpan VentanaAntiduplicados { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Decide si un fichaje nuevo repite uno anterior. Se compara contra el último del
    /// mismo tipo: entrar y salir con un minuto de diferencia es raro pero legítimo
    /// —alguien que se equivocó y corrige—, mientras que entrar dos veces no lo es.
    /// </summary>
    public bool EsDuplicada(DateTimeOffset? ultimaDelMismoTipo, DateTimeOffset momento) =>
        ultimaDelMismoTipo is { } previa && momento - previa < VentanaAntiduplicados;

    /// <summary>
    /// Qué tipo de fichaje corresponde cuando el cliente no lo dice. Alternar es la
    /// lectura más segura: si lo último fue una entrada, lo siguiente es una salida.
    /// </summary>
    public static TipoChecada SiguienteTipo(TipoChecada? ultimo) =>
        ultimo == TipoChecada.Entrada ? TipoChecada.Salida : TipoChecada.Entrada;
}
