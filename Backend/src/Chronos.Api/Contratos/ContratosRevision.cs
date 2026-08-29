using System.ComponentModel.DataAnnotations;
using Chronos.Domain.Enums;

namespace Chronos.Api.Contratos;

/// <summary>
/// Una checada pendiente de dictamen, con el contexto que el revisor necesita para
/// decidir sin abrir otra pantalla: de quién es, cuándo fue y qué señales la respaldan.
/// </summary>
public sealed record ChecadaPorRevisarDto(
    Guid Id,
    Guid EmpleadoId,
    string EmpleadoNombre,
    string NumeroEmpleado,
    string DepartamentoNombre,
    TipoChecada Tipo,
    DateTimeOffset MomentoUtc,
    DateOnly DiaLaboral,
    EstadoChecada Estado,
    int PuntajeConfianza,
    NivelConfianza NivelConfianza,
    string? SedeNombre,
    IReadOnlyList<SenalDto> Senales);

public sealed record SolicitudDictamen
{
    /// <summary>
    /// Obligatorio en ambos sentidos. Aprobar sin explicar por qué deja una bitácora que
    /// no responde la única pregunta que se le hará dentro de seis meses.
    /// </summary>
    [Required(ErrorMessage = "Escribe el motivo del dictamen.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

public sealed record AsientoBitacoraDto(
    Guid Id,
    DateTimeOffset OcurridoUtc,
    AccionAuditada Accion,
    string AccionNombre,
    string Entidad,
    Guid? EntidadId,
    string? UsuarioCorreo,
    string? Motivo);
