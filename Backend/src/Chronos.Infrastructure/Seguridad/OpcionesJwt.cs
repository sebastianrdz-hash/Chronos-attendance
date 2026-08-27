using System.ComponentModel.DataAnnotations;

namespace Chronos.Infrastructure.Seguridad;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    [Required]
    public string Emisor { get; set; } = "chronos-api";

    [Required]
    public string Audiencia { get; set; } = "chronos-client";

    /// <summary>
    /// Se inyecta por variable de entorno (Jwt__Llave). Necesita al menos 32 bytes
    /// porque HMAC-SHA256 rechaza llaves más cortas.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "La llave JWT debe tener al menos 32 caracteres.")]
    public string Llave { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int MinutosVigencia { get; set; } = 60;
}
