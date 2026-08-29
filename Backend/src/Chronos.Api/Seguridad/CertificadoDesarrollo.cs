using System.Security.Cryptography.X509Certificates;

namespace Chronos.Api.Seguridad;

/// <summary>
/// Localiza el certificado de desarrollo que emite <c>scripts/certificados-dev.ps1</c>.
/// Sirve para que la API hable HTTPS en local sin obligar a generarlo: si no está, el
/// arranque sigue en HTTP plano.
/// </summary>
internal static class CertificadoDesarrollo
{
    private const string Carpeta = "certificados";
    private const string Archivo = "chronos-dev.pfx";

    // mkcert protege todos sus PKCS#12 con esta contraseña fija y la anuncia al generarlos.
    // No es un secreto: el certificado solo vale para esta máquina y para nombres locales.
    private const string Contrasena = "changeit";

    public static X509Certificate2? Cargar(string rutaContenido)
    {
        var ruta = Localizar(rutaContenido);

        return ruta is null
            ? null
            : X509CertificateLoader.LoadPkcs12FromFile(ruta, Contrasena);
    }

    /// <summary>
    /// La API arranca desde Backend/src/Chronos.Api y los certificados viven en la raíz
    /// del repositorio, así que se sube por el árbol hasta encontrarlos.
    /// </summary>
    private static string? Localizar(string rutaContenido)
    {
        var directorio = new DirectoryInfo(rutaContenido);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, Carpeta, Archivo);
            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        return null;
    }
}
