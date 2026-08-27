namespace Chronos.Domain.Enums;

/// <summary>
/// Los roles son una regla de negocio, no un detalle de ASP.NET Identity: viven aquí
/// para que las políticas de acceso se puedan probar sin tocar el framework.
/// El orden refleja el nivel de privilegio.
/// </summary>
public enum RolChronos
{
    Empleado = 1,
    Supervisor = 2,
    Admin = 3
}
