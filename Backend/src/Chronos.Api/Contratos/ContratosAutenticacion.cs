using Chronos.Domain.Enums;

namespace Chronos.Api.Contratos;

public sealed record SolicitudLogin(string Correo, string Contrasena);

public sealed record RespuestaLogin(
    string AccessToken,
    string TipoToken,
    int ExpiraEnSegundos,
    DateTimeOffset ExpiraUtc,
    PerfilUsuario Usuario);

public sealed record PerfilUsuario(
    Guid Id,
    string Correo,
    string Nombre,
    IReadOnlyList<string> Roles,
    /// <summary>Rol efectivo: el de mayor privilegio. El cliente enruta con este.</summary>
    RolChronos Rol,
    Guid? EmpleadoId,
    string? NumeroEmpleado,
    string? Puesto,
    Guid? DepartamentoId,
    string? Departamento,
    Guid? SedeId,
    string? Sede,
    bool DebeCambiarContrasena);
