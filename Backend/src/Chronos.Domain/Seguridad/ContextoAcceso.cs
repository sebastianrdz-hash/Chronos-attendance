using Chronos.Domain.Enums;

namespace Chronos.Domain.Seguridad;

/// <summary>
/// Quién está pidiendo algo. Se arma una vez por petición a partir del token y del
/// expediente vigente en la base, nunca solo de los claims: el departamento de un
/// supervisor es una frontera de autorización y un token viejo la dejaría desfasada.
/// </summary>
public sealed record ContextoAcceso
{
    public required RolChronos Rol { get; init; }

    public Guid? EmpleadoId { get; init; }

    public Guid? DepartamentoId { get; init; }

    public Guid? SedeId { get; init; }

    public bool EsAdmin => Rol == RolChronos.Admin;

    public bool EsSupervisor => Rol == RolChronos.Supervisor;

    public bool EsEmpleado => Rol == RolChronos.Empleado;

    public static ContextoAcceso Para(RolChronos rol, Guid? empleadoId = null, Guid? departamentoId = null) =>
        new() { Rol = rol, EmpleadoId = empleadoId, DepartamentoId = departamentoId };
}
