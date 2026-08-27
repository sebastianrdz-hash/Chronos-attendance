using System.ComponentModel.DataAnnotations;
using Chronos.Domain.Enums;

namespace Chronos.Api.Contratos;

public sealed record EmpleadoDto(
    Guid Id,
    string NumeroEmpleado,
    string Nombres,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string NombreCompleto,
    string CorreoCorporativo,
    string? Puesto,
    DateOnly FechaIngreso,
    DateOnly? FechaBaja,
    bool Activo,
    Guid DepartamentoId,
    string DepartamentoNombre,
    Guid SedeId,
    string SedeNombre,
    Guid? TurnoId,
    string? TurnoNombre,
    RolChronos Rol,
    bool DebeCambiarContrasena);

/// <summary>
/// La contraseña temporal solo viaja en la respuesta del alta: no se guarda en claro ni
/// se puede volver a consultar. Si se pierde, el admin reinicia el acceso.
/// </summary>
public sealed record RespuestaAltaEmpleado(EmpleadoDto Empleado, string ContrasenaTemporal);

public sealed record SolicitudCrearEmpleado : SolicitudEmpleadoBase
{
    [Required(ErrorMessage = "El número de empleado es obligatorio.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "El número de empleado debe tener entre 3 y 20 caracteres.")]
    public string NumeroEmpleado { get; init; } = string.Empty;

    [Required(ErrorMessage = "Asigna un rol.")]
    public RolChronos Rol { get; init; } = RolChronos.Empleado;
}

public sealed record SolicitudActualizarEmpleado : SolicitudEmpleadoBase
{
    [Required(ErrorMessage = "El número de empleado es obligatorio.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "El número de empleado debe tener entre 3 y 20 caracteres.")]
    public string NumeroEmpleado { get; init; } = string.Empty;

    [Required(ErrorMessage = "Asigna un rol.")]
    public RolChronos Rol { get; init; } = RolChronos.Empleado;

    public bool Activo { get; init; } = true;

    /// <summary>Obligatoria al desactivar; el servidor la exige aunque el cliente no la mande.</summary>
    public DateOnly? FechaBaja { get; init; }
}

public abstract record SolicitudEmpleadoBase
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 80 caracteres.")]
    public string Nombres { get; init; } = string.Empty;

    [Required(ErrorMessage = "El apellido paterno es obligatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El apellido paterno debe tener entre 2 y 80 caracteres.")]
    public string ApellidoPaterno { get; init; } = string.Empty;

    [StringLength(80, ErrorMessage = "El apellido materno no puede exceder 80 caracteres.")]
    public string? ApellidoMaterno { get; init; }

    [Required(ErrorMessage = "El correo corporativo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(160, ErrorMessage = "El correo no puede exceder 160 caracteres.")]
    public string CorreoCorporativo { get; init; } = string.Empty;

    [StringLength(100, ErrorMessage = "El puesto no puede exceder 100 caracteres.")]
    public string? Puesto { get; init; }

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    public DateOnly FechaIngreso { get; init; }

    [Required(ErrorMessage = "Selecciona un departamento.")]
    public Guid DepartamentoId { get; init; }

    [Required(ErrorMessage = "Selecciona una sede.")]
    public Guid SedeId { get; init; }

    public Guid? TurnoId { get; init; }
}

public sealed record SolicitudCambioContrasena
{
    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    public string ContrasenaActual { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe la contraseña nueva.")]
    [StringLength(128, MinimumLength = 10, ErrorMessage = "La contraseña nueva debe tener al menos 10 caracteres.")]
    public string ContrasenaNueva { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirma la contraseña nueva.")]
    [Compare(nameof(ContrasenaNueva), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; init; } = string.Empty;
}

/// <summary>Vista propia del empleado: sus datos más el turno con el que se le califica.</summary>
public sealed record MiPerfilDto(
    EmpleadoDto Empleado,
    TurnoDto? Turno,
    string Correo,
    DateTimeOffset? UltimoAccesoUtc);
