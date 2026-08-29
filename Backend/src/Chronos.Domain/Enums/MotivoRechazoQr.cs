namespace Chronos.Domain.Enums;

/// <summary>
/// Por qué se rechazó un token de QR. Se distingue el motivo en lugar de devolver un
/// booleano porque cada caso significa algo distinto para quien opera el sistema: un
/// token caducado es un empleado que tardó en escanear, mientras que una firma inválida
/// es alguien fabricando códigos.
/// </summary>
public enum MotivoRechazoQr
{
    Ninguno = 0,

    /// <summary>El texto no tiene la forma de un token: largo, versión o base64 inválidos.</summary>
    FormatoInvalido = 1,

    /// <summary>La firma no corresponde al contenido. El token no lo emitió este servidor.</summary>
    FirmaInvalida = 2,

    /// <summary>El token es auténtico pero se pasó de su ventana de vigencia.</summary>
    Caducado = 3,

    /// <summary>El token ya se usó antes. Alguien reenvió una captura del código.</summary>
    NonceReusado = 4,

    /// <summary>La sede del token no es la que se esperaba para esta operación.</summary>
    SedeNoCorresponde = 5
}
