namespace Chronos.Domain.Enums;

/// <summary>
/// Acciones que dejan constancia. Es un enum y no texto libre para que la bandeja pueda
/// filtrar por tipo sin depender de que todos escriban la misma cadena.
/// </summary>
public enum AccionAuditada
{
    ChecadaAprobada = 1,

    ChecadaRechazada = 2,

    CredencialRevocada = 3,

    EmpleadoDadoDeAlta = 4,

    EmpleadoDadoDeBaja = 5,

    AccesoReiniciado = 6
}
