using Chronos.Domain.Enums;

namespace Chronos.Domain.Entidades;

/// <summary>
/// Un asiento de la bitácora: quién hizo qué, sobre qué y por qué.
/// <para>
/// Solo se inserta. No hay forma de corregir un asiento porque una bitácora que se puede
/// editar no sirve para lo único que se le pide: sostener una versión de los hechos
/// cuando alguien la discute. Si un asiento quedó mal, se escribe otro que lo explique.
/// </para>
/// <para>
/// La restricción no se queda en la intención: la migración instala un disparador que
/// aborta cualquier UPDATE o DELETE sobre la tabla. Que el código de la aplicación no los
/// intente es fácil de garantizar hoy y fácil de romper dentro de un año.
/// </para>
/// </summary>
public class AsientoBitacora
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OcurridoUtc { get; init; }

    public Guid? UsuarioId { get; init; }

    /// <summary>
    /// El correo se copia en vez de resolverse por la relación. Un asiento tiene que
    /// seguir siendo legible aunque la cuenta se renombre o se dé de baja después.
    /// </summary>
    public string? UsuarioCorreo { get; init; }

    public AccionAuditada Accion { get; init; }

    /// <summary>Tipo de entidad afectada, en singular y como lo nombra el dominio.</summary>
    public required string Entidad { get; init; }

    public Guid? EntidadId { get; init; }

    /// <summary>Justificación que dio el usuario. Es el corazón del asiento.</summary>
    public string? Motivo { get; init; }

    /// <summary>Contexto adicional en JSON: el antes y el después de lo que cambió.</summary>
    public string? DatosJson { get; init; }

    public string? DireccionIp { get; init; }
}
