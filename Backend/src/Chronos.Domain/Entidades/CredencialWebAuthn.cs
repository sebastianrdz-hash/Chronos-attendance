using Chronos.Domain.Common;

namespace Chronos.Domain.Entidades;

/// <summary>
/// Credencial FIDO2 registrada por un empleado. Solo se almacena la clave pública:
/// la huella o el rostro nunca salen del enclave seguro del dispositivo.
/// </summary>
public class CredencialWebAuthn : EntidadBase
{
    public Guid EmpleadoId { get; set; }

    public Empleado? Empleado { get; set; }

    public required byte[] CredentialId { get; set; }

    public required byte[] ClavePublica { get; set; }

    public required byte[] IdUsuario { get; set; }

    /// <summary>
    /// Contador de firmas que reporta el autenticador. Si no crece entre usos,
    /// la credencial pudo haber sido clonada.
    /// </summary>
    public long ContadorFirmas { get; set; }

    public Guid AaGuid { get; set; }

    public string? NombreAmigable { get; set; }

    public string? TipoDispositivo { get; set; }

    public bool Activa { get; set; } = true;

    public DateTimeOffset? UltimoUsoUtc { get; set; }
}
