using Chronos.Domain.Common;
using Chronos.Domain.Enums;

namespace Chronos.Domain.Entidades;

/// <summary>
/// Evidencia individual que respalda una checada. Una checada acumula varias:
/// cada tipo prueba algo distinto y ninguno basta por sí solo.
/// </summary>
public class SenalPresencia : EntidadBase
{
    public Guid ChecadaId { get; set; }

    public Checada? Checada { get; set; }

    public TipoSenal Tipo { get; set; }

    public ResultadoSenal Resultado { get; set; }

    /// <summary>Puntos que esta señal aportó (o restó) al puntaje de la checada.</summary>
    public int PesoAplicado { get; set; }

    public DateTimeOffset CapturadaUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Carga específica del tipo de señal, serializada como JSON y guardada en una
    /// columna jsonb. Un beacon guarda aquí UUID/major/minor/RSSI y un QR el folio del
    /// token, sin que agregar un tipo nuevo obligue a migrar el esquema.
    /// </summary>
    public string? DetalleJson { get; set; }
}
