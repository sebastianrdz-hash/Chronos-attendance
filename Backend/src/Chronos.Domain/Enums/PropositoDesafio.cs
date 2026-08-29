namespace Chronos.Domain.Enums;

/// <summary>
/// Para qué se emitió un desafío FIDO2. Se distingue para que un reto pedido con la
/// excusa de enrolar un dispositivo no pueda canjearse por un fichaje, ni al revés.
/// </summary>
public enum PropositoDesafio
{
    /// <summary>Alta de una credencial nueva desde el perfil del empleado.</summary>
    Enrolamiento = 1,

    /// <summary>Verificación de una credencial ya registrada al momento de fichar.</summary>
    Autenticacion = 2
}
