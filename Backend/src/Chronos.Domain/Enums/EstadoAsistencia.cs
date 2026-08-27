namespace Chronos.Domain.Enums;

public enum EstadoAsistencia
{
    /// <summary>El día no es laboral para el turno asignado.</summary>
    Descanso = 0,

    Completa = 1,

    Retardo = 2,

    SalidaAnticipada = 3,

    /// <summary>Hay entrada pero nunca se registró la salida.</summary>
    JornadaIncompleta = 4,

    Falta = 5
}
