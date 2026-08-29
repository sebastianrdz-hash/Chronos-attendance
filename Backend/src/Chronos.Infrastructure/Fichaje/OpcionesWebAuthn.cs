using System.ComponentModel.DataAnnotations;

namespace Chronos.Infrastructure.Fichaje;

/// <summary>
/// Identidad de este servicio frente a los autenticadores FIDO2.
/// <para>
/// Es la configuración que más errores confusos provoca, así que conviene tenerla clara:
/// el RP ID y los orígenes describen lo que ve el <em>navegador</em> —el host de Vite—,
/// nunca el puerto en el que escucha la API. Si el usuario entra por
/// <c>https://localhost:5173</c> y aquí dijera <c>localhost:5080</c>, el enrolamiento
/// fallaría con un error que no menciona ninguna de las dos cosas.
/// </para>
/// </summary>
public sealed class OpcionesWebAuthn
{
    public const string Seccion = "WebAuthn";

    /// <summary>
    /// Dominio al que quedan atadas las credenciales. Sin puerto ni esquema.
    /// <para>
    /// Tiene que ser un nombre de dominio: la norma prohíbe las direcciones IP y el
    /// navegador responde con SecurityError si se intenta. <c>localhost</c> es la única
    /// excepción, y por eso es el valor de desarrollo.
    /// </para>
    /// </summary>
    [Required]
    public string RpId { get; set; } = "localhost";

    /// <summary>Nombre que el sistema operativo muestra en el diálogo de biometría.</summary>
    [Required]
    public string NombreRp { get; set; } = "Chronos";

    /// <summary>
    /// Orígenes completos, con esquema y puerto, desde los que se acepta una ceremonia.
    /// Se comprueban aparte del RP ID porque el navegador manda el origen exacto dentro
    /// del clientDataJSON firmado.
    /// </summary>
    public string[] OrigenesPermitidos { get; set; } = [];

    /// <summary>
    /// Cuánto vive un desafío. Suficiente para que alguien encuentre el lector y ponga el
    /// dedo, y lo bastante corto para que uno interceptado no sirva más tarde.
    /// </summary>
    [Range(30, 600)]
    public int SegundosVigenciaDesafio { get; set; } = 120;

    /// <summary>
    /// Máximo de credenciales por empleado. Se limita para que una cuenta comprometida no
    /// pueda sembrar decenas de llaves y conservar el acceso tras revocar la legítima.
    /// </summary>
    [Range(1, 20)]
    public int MaximoCredenciales { get; set; } = 5;
}
