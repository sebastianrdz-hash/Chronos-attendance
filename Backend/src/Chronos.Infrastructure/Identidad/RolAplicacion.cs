using Chronos.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Chronos.Infrastructure.Identidad;

public class RolAplicacion : IdentityRole<Guid>
{
    public string? Descripcion { get; set; }
}

/// <summary>
/// Puente entre los nombres de rol que guarda Identity y el enum del dominio, que es
/// el que consultan las políticas de acceso.
/// </summary>
public static class Roles
{
    public const string Admin = nameof(RolChronos.Admin);
    public const string Supervisor = nameof(RolChronos.Supervisor);
    public const string Empleado = nameof(RolChronos.Empleado);

    public static readonly IReadOnlyList<string> Todos = [Admin, Supervisor, Empleado];

    public static string Nombre(RolChronos rol) => rol.ToString();

    public static RolChronos? Interpretar(string? nombre) =>
        Enum.TryParse<RolChronos>(nombre, ignoreCase: true, out var rol) && Enum.IsDefined(rol)
            ? rol
            : null;

    /// <summary>
    /// Un usuario puede tener varios roles en Identity; para autorizar manda el más alto.
    /// </summary>
    public static RolChronos MayorPrivilegio(IEnumerable<string> nombres) =>
        nombres
            .Select(Interpretar)
            .Where(rol => rol is not null)
            .Select(rol => rol!.Value)
            .DefaultIfEmpty(RolChronos.Empleado)
            .Max();
}
