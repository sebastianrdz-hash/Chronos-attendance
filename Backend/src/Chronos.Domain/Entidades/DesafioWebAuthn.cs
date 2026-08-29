using Chronos.Domain.Enums;

namespace Chronos.Domain.Entidades;

/// <summary>
/// Desafío pendiente de una ceremonia FIDO2.
/// <para>
/// Una ceremonia WebAuthn son dos viajes: el servidor propone un reto aleatorio y el
/// navegador vuelve con la firma de ese reto. Entre ambos hay que recordar qué se pidió,
/// y la API no tiene sesión —autentica con JWT— así que el estado va aquí y no en memoria.
/// Guardarlo en la base tiene además la ventaja de que sigue funcionando si algún día
/// corren varias instancias detrás de un balanceador.
/// </para>
/// <para>
/// Se guarda el JSON completo de las opciones y no solo el reto: la verificación necesita
/// confrontar la respuesta contra todo lo que se pidió, incluidos el tipo de verificación
/// de usuario y las credenciales admitidas. Reconstruirlo sería fiarse de que el cliente
/// no cambió nada por el camino, que es justo lo que se quiere evitar.
/// </para>
/// </summary>
public class DesafioWebAuthn
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EmpleadoId { get; set; }

    public PropositoDesafio Proposito { get; set; }

    public required string OpcionesJson { get; set; }

    /// <summary>
    /// Nombre que el empleado le puso al dispositivo. Se retiene entre los dos viajes de
    /// la ceremonia porque el segundo solo trae la respuesta del autenticador, y volver a
    /// pedirlo sería un formulario de más justo después de poner el dedo.
    /// </summary>
    public string? NombreDispositivo { get; set; }

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiraUtc { get; set; }

    public bool Vigente(DateTimeOffset ahora) => ahora <= ExpiraUtc;
}
