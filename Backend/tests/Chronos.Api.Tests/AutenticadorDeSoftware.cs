using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronos.Api.Tests;

/// <summary>
/// Autenticador FIDO2 falso que hace de Windows Hello o del sensor de un teléfono.
/// <para>
/// Sin esto, WebAuthn solo se podría comprobar con un dedo humano encima de un lector, y
/// el flujo quedaría fuera de la red de pruebas justo por ser el más delicado. Aquí se
/// genera una clave P-256 de verdad y se firman desafíos de verdad: lo que la API valida
/// es criptografía auténtica, no un doble complaciente. Lo único simulado es el hardware.
/// </para>
/// <para>
/// Que se pueda equivocar a propósito —firmar con otra clave, mentir sobre el origen, no
/// avanzar el contador— es la mitad de su utilidad: así se comprueba que el servidor
/// rechaza lo que debe rechazar.
/// </para>
/// </summary>
public sealed class AutenticadorDeSoftware : IDisposable
{
    private readonly ECDsa _clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public AutenticadorDeSoftware(string rpId, string origen)
    {
        RpId = rpId;
        Origen = origen;
        CredentialId = RandomNumberGenerator.GetBytes(32);
        AaGuid = new byte[16];
    }

    public string RpId { get; }

    public string Origen { get; }

    public byte[] CredentialId { get; }

    public byte[] AaGuid { get; }

    /// <summary>
    /// Cuántas veces ha firmado. El autenticador real lo incrementa en cada uso y el
    /// servidor vigila que nunca retroceda, porque un contador que va hacia atrás delata
    /// una copia de la llave circulando en paralelo.
    /// </summary>
    public uint ContadorFirmas { get; set; } = 1;

    /// <summary>Respuesta a <c>navigator.credentials.create()</c>.</summary>
    public object Registrar(string desafioBase64Url, string? origenFalso = null)
    {
        var clientData = ClientDataJson("webauthn.create", desafioBase64Url, origenFalso ?? Origen);

        // El bit AT anuncia que el authData lleva pegados los datos de la credencial nueva.
        var authData = ArmarAuthData(banderas: 0x45, incluirCredencial: true);

        var attestationObject = new CborWriter();
        attestationObject.WriteStartMap(3);
        attestationObject.WriteTextString("fmt");

        // "none" es la constancia que emite un autenticador que no quiere identificar a su
        // fabricante. Es lo que Chronos pide, así que es lo que se debe probar.
        attestationObject.WriteTextString("none");
        attestationObject.WriteTextString("attStmt");
        attestationObject.WriteStartMap(0);
        attestationObject.WriteEndMap();
        attestationObject.WriteTextString("authData");
        attestationObject.WriteByteString(authData);
        attestationObject.WriteEndMap();

        return new
        {
            id = Base64Url(CredentialId),
            rawId = Base64Url(CredentialId),
            type = "public-key",
            response = new
            {
                clientDataJSON = Base64Url(clientData),
                attestationObject = Base64Url(attestationObject.Encode())
            },
            extensions = new { }
        };
    }

    /// <summary>Respuesta a <c>navigator.credentials.get()</c>.</summary>
    public object Firmar(
        string desafioBase64Url,
        string? origenFalso = null,
        ECDsa? claveFalsa = null,
        uint? contadorFalso = null)
    {
        var clientData = ClientDataJson("webauthn.get", desafioBase64Url, origenFalso ?? Origen);

        var contador = contadorFalso ?? ++ContadorFirmas;

        // Sin bit AT: al autenticar no se vuelven a enviar los datos de la credencial.
        var authData = ArmarAuthData(banderas: 0x05, incluirCredencial: false, contador: contador);

        // Se firma el authData seguido del hash del clientData. Ese encadenamiento es lo
        // que ata la firma al desafío y al origen concretos de esta ceremonia.
        var aFirmar = new byte[authData.Length + 32];
        authData.CopyTo(aFirmar, 0);
        SHA256.HashData(clientData).CopyTo(aFirmar, authData.Length);

        var firma = (claveFalsa ?? _clave).SignData(aFirmar, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new
        {
            id = Base64Url(CredentialId),
            rawId = Base64Url(CredentialId),
            type = "public-key",
            response = new
            {
                clientDataJSON = Base64Url(clientData),
                authenticatorData = Base64Url(authData),
                signature = Base64Url(firma),
                userHandle = (string?)null
            },
            extensions = new { }
        };
    }

    private byte[] ClientDataJson(string tipo, string desafioBase64Url, string origen) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = tipo,
            challenge = desafioBase64Url,
            origin = origen,
            crossOrigin = false
        });

    private byte[] ArmarAuthData(byte banderas, bool incluirCredencial, uint? contador = null)
    {
        using var flujo = new MemoryStream();

        flujo.Write(SHA256.HashData(Encoding.UTF8.GetBytes(RpId)));
        flujo.WriteByte(banderas);

        var contadorBytes = BitConverter.GetBytes(contador ?? ContadorFirmas);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(contadorBytes);
        }

        flujo.Write(contadorBytes);

        if (!incluirCredencial)
        {
            return flujo.ToArray();
        }

        flujo.Write(AaGuid);

        var largo = BitConverter.GetBytes((ushort)CredentialId.Length);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(largo);
        }

        flujo.Write(largo);
        flujo.Write(CredentialId);
        flujo.Write(ClavePublicaCose());

        return flujo.ToArray();
    }

    /// <summary>
    /// La clave pública en el formato COSE_Key que espera la norma: un mapa CBOR con el
    /// tipo de clave, el algoritmo, la curva y las dos coordenadas del punto.
    /// </summary>
    private byte[] ClavePublicaCose()
    {
        var parametros = _clave.ExportParameters(includePrivateParameters: false);

        var escritor = new CborWriter();
        escritor.WriteStartMap(5);

        escritor.WriteInt32(1);
        escritor.WriteInt32(2); // kty: EC2

        escritor.WriteInt32(3);
        escritor.WriteInt32(-7); // alg: ES256

        escritor.WriteInt32(-1);
        escritor.WriteInt32(1); // crv: P-256

        escritor.WriteInt32(-2);
        escritor.WriteByteString(parametros.Q.X!);

        escritor.WriteInt32(-3);
        escritor.WriteByteString(parametros.Q.Y!);

        escritor.WriteEndMap();

        return escritor.Encode();
    }

    public static string Base64Url(byte[] datos) => Base64Url(datos.AsSpan());

    private static string Base64Url(ReadOnlySpan<byte> datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _clave.Dispose();
}
