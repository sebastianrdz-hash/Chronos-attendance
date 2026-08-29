using Chronos.Domain.Enums;

namespace Chronos.Api.Contratos;

/// <param name="HorasTrabajadas">
/// En horas decimales y no en un intervalo con formato: es lo que un reporte necesita para
/// sumar, y evita que cada cliente reinvente el análisis de "08:30:00".
/// </param>
public sealed record AsistenciaDelDiaDto(
    Guid EmpleadoId,
    string NombreCompleto,
    string NumeroEmpleado,
    string DepartamentoNombre,
    string SedeNombre,
    string? TurnoNombre,
    EstadoAsistencia Estado,
    string EstadoNombre,
    DateTimeOffset? Entrada,
    DateTimeOffset? Salida,
    double HorasTrabajadas,
    double HorasExtra,
    int MinutosRetardo,
    int MinutosSalidaAnticipada,
    bool RequiereRevision,
    NivelConfianza ConfianzaMinima);

/// <summary>
/// Los totales del día, calculados en el servidor. Si cada pantalla los sumara por su
/// cuenta, el dashboard y el reporte acabarían discrepando sobre qué cuenta como retardo.
/// </summary>
public sealed record ResumenAsistenciaDto(
    DateOnly Dia,
    int Plantilla,
    int Presentes,
    int Faltas,
    int Retardos,
    int JornadasIncompletas,
    int PendientesDeRevision,
    double HorasTrabajadas,
    double HorasExtra);

public sealed record AsistenciaDto(ResumenAsistenciaDto Resumen, IReadOnlyList<AsistenciaDelDiaDto> Empleados);
