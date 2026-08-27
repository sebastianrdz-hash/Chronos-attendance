using System.Security.Cryptography;

namespace Chronos.Infrastructure.Seguridad;

/// <summary>
/// Contraseñas temporales para las altas. Se arman garantizando un carácter de cada
/// familia que exige la política de Identity, en vez de generar al azar y reintentar
/// hasta que pase la validación.
/// </summary>
public static class GeneradorContrasenas
{
    private const string Mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnopqrstuvwxyz";
    private const string Digitos = "23456789";
    private const string Simbolos = "!@#$%&*?";

    /// <summary>Se omiten I, l, O, 0 y 1 porque estas claves se dictan o se copian a mano.</summary>
    private const string Todos = Mayusculas + Minusculas + Digitos + Simbolos;

    public static string Temporal(int longitud = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longitud, 10);

        var caracteres = new char[longitud];
        caracteres[0] = Elegir(Mayusculas);
        caracteres[1] = Elegir(Minusculas);
        caracteres[2] = Elegir(Digitos);
        caracteres[3] = Elegir(Simbolos);

        for (var i = 4; i < longitud; i++)
        {
            caracteres[i] = Elegir(Todos);
        }

        RandomNumberGenerator.Shuffle<char>(caracteres);
        return new string(caracteres);
    }

    private static char Elegir(string alfabeto) => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
}
