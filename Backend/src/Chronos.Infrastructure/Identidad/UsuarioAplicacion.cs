using Microsoft.AspNetCore.Identity;

namespace Chronos.Infrastructure.Identidad;

public class UsuarioAplicacion : IdentityUser<Guid>
{
    public string? NombreParaMostrar { get; set; }

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UltimoAccesoUtc { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Se marca al dar de alta con contraseña temporal. El login sigue emitiendo token
    /// (hace falta para poder llamar al cambio de contraseña), pero el cliente encierra
    /// al usuario en esa pantalla hasta que la reemplace.
    /// </summary>
    public bool DebeCambiarContrasena { get; set; }
}
