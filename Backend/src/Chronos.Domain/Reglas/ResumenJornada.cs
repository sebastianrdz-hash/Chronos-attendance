using Chronos.Domain.Enums;

namespace Chronos.Domain.Reglas;

public sealed record ResumenJornada
{
    public required DateOnly Dia { get; init; }

    public DateTimeOffset? Entrada { get; init; }

    public DateTimeOffset? Salida { get; init; }

    public TimeSpan HorasTrabajadas { get; init; }

    public TimeSpan HorasExtra { get; init; }

    public TimeSpan TiempoDescanso { get; init; }

    public int MinutosRetardo { get; init; }

    public int MinutosSalidaAnticipada { get; init; }

    public required EstadoAsistencia Estado { get; init; }

    /// <summary>Alguna checada del día se apoyó en señales débiles.</summary>
    public bool RequiereRevision { get; init; }

    public NivelConfianza ConfianzaMinima { get; init; } = NivelConfianza.Nula;
}
