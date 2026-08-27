using Chronos.Domain.Common;

namespace Chronos.Domain.Entidades;

public class Empleado : EntidadBase
{
    public required string NumeroEmpleado { get; set; }

    public required string Nombres { get; set; }

    public required string ApellidoPaterno { get; set; }

    public string? ApellidoMaterno { get; set; }

    public required string CorreoCorporativo { get; set; }

    public string? Puesto { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public DateOnly? FechaBaja { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Enlace con la cuenta de ASP.NET Core Identity. Se guarda como identificador suelto
    /// para que el dominio no dependa del framework de autenticación.
    /// </summary>
    public Guid? UsuarioId { get; set; }

    public Guid DepartamentoId { get; set; }

    public Departamento? Departamento { get; set; }

    public Guid SedeId { get; set; }

    public Sede? Sede { get; set; }

    public Guid? TurnoId { get; set; }

    public Turno? Turno { get; set; }

    public ICollection<Checada> Checadas { get; set; } = [];

    public ICollection<CredencialWebAuthn> Credenciales { get; set; } = [];

    public string NombreCompleto =>
        string.Join(' ', new[] { Nombres, ApellidoPaterno, ApellidoMaterno }
            .Where(parte => !string.IsNullOrWhiteSpace(parte)));
}
