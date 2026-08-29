namespace Chronos.Domain.Entidades;

/// <summary>
/// Constancia de que un código QR ya se usó. La firma del token prueba que lo emitió
/// este servidor, pero no que sea la primera vez que alguien lo presenta: eso solo se
/// puede saber recordando los que ya pasaron.
/// <para>
/// No hereda de <c>EntidadBase</c> a propósito. No es una entidad del negocio con vida
/// propia sino un asiento de un libro que solo crece: se inserta una vez, nunca se
/// modifica y se purga cuando deja de importar.
/// </para>
/// </summary>
public class NonceQrConsumido
{
    /// <summary>
    /// Nonce del token, y también la llave primaria. Que la unicidad la imponga la base
    /// es deliberado: dos peticiones simultáneas con el mismo código no se pueden coordinar
    /// consultando antes de insertar, porque ambas verían la tabla vacía. Aquí una gana y
    /// la otra choca contra la llave, que es justo el rechazo que se busca.
    /// </summary>
    public Guid Nonce { get; set; }

    public Guid SedeId { get; set; }

    public Guid EmpleadoId { get; set; }

    public Guid ChecadaId { get; set; }

    public DateTimeOffset ConsumidoUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Caducidad del token que originó el asiento. Pasada esa fecha el registro sobra:
    /// el propio control de vigencia ya rechazaría el código aunque nadie lo recordara.
    /// </summary>
    public DateTimeOffset ExpiraUtc { get; set; }
}
