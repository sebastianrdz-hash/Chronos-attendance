namespace Chronos.Infrastructure.Seguridad;

/// <summary>Claims propios que viajan en el token además de los estándar.</summary>
public static class ClaimsChronos
{
    /// <summary>
    /// Nombres cortos al estilo OIDC. Identity y la validación del token se configuran
    /// para leer estos, en lugar de los URIs largos de ClaimTypes.
    /// </summary>
    public const string Rol = "role";

    public const string EmpleadoId = "empleado_id";
    public const string NumeroEmpleado = "numero_empleado";
    public const string SedeId = "sede_id";
    public const string NombreCompleto = "nombre";
}
