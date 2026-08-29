using System.ComponentModel.DataAnnotations;

namespace Chronos.Infrastructure.Fichaje;

public sealed class OpcionesQr
{
    public const string Seccion = "Qr";

    /// <summary>
    /// Llave con la que se firman los códigos de kiosco. Es distinta de la del JWT a
    /// propósito: son dos secretos con vidas y alcances distintos, y compartirlos
    /// obligaría a invalidar todas las sesiones para poder rotar la firma de los QR.
    /// Se inyecta por variable de entorno (Qr__Llave).
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "La llave del QR debe tener al menos 32 caracteres.")]
    public string Llave { get; set; } = string.Empty;

    /// <summary>
    /// Segundos que vive cada código. El valor por omisión sale de la política del
    /// dominio; bajarlo endurece el sistema y subirlo lo vuelve más cómodo de burlar.
    /// </summary>
    [Range(10, 300)]
    public int SegundosVigencia { get; set; } = 30;

    [Range(0, 60)]
    public int SegundosGracia { get; set; } = 5;

    /// <summary>
    /// Tamaño en píxeles del lado del PNG que se manda al kiosco. Suficiente para que se
    /// lea desde lejos sin que la respuesta pese de más.
    /// </summary>
    [Range(128, 2048)]
    public int PixelesImagen { get; set; } = 512;
}
