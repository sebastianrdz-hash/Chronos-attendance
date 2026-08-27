using Chronos.Domain.Enums;

namespace Chronos.Domain.Reglas;

/// <summary>
/// Cuánto vale cada evidencia y a partir de qué puntaje una checada se da por buena.
/// Los pesos están calibrados para que el flujo completo de la fase 1
/// (QR + WebAuthn = 70) alcance justo el umbral de confianza alta.
/// </summary>
public sealed class PoliticaConfianza
{
    // Declarado antes que Predeterminada: los inicializadores estáticos corren en orden
    // y la instancia predeterminada lee este diccionario al construirse.
    private static readonly Dictionary<TipoSenal, int> PesosPredeterminados = new()
    {
        [TipoSenal.WebAuthn] = 45,
        [TipoSenal.CodigoQr] = 25,
        [TipoSenal.BeaconBle] = 20,
        [TipoSenal.Geocerca] = 10,
        [TipoSenal.RedWifi] = 10,
        [TipoSenal.RegistroManual] = 30
    };

    public static PoliticaConfianza Predeterminada { get; } = new();

    public IReadOnlyDictionary<TipoSenal, int> Pesos { get; init; } = PesosPredeterminados;

    public int UmbralAlta { get; init; } = 70;

    public int UmbralMedia { get; init; } = 40;

    public int PenalizacionFallida { get; init; } = 10;

    public int PenalizacionSospechosa { get; init; } = 25;

    public int PesoDe(TipoSenal tipo) => Pesos.TryGetValue(tipo, out var peso) ? peso : 0;

    public NivelConfianza Clasificar(int puntaje) => puntaje switch
    {
        <= 0 => NivelConfianza.Nula,
        var p when p >= UmbralAlta => NivelConfianza.Alta,
        var p when p >= UmbralMedia => NivelConfianza.Media,
        _ => NivelConfianza.Baja
    };
}
