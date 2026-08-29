using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using Chronos.Domain.Enums;
using Chronos.Domain.ValueObjects;

namespace Chronos.Domain.Reglas;

/// <param name="Valido">Si el token puede aceptarse. No cubre la reutilización del nonce,
/// que exige consultar qué se ha usado antes y por eso vive fuera del dominio.</param>
public readonly record struct ResultadoLecturaQr(
    bool Valido,
    MotivoRechazoQr Motivo,
    TokenQr Token)
{
    public static ResultadoLecturaQr Rechazo(MotivoRechazoQr motivo) => new(false, motivo, default);
}

/// <summary>
/// Firma y verifica los códigos que muestra un kiosco.
/// <para>
/// El token va firmado con HMAC-SHA256 y no cifrado: su contenido —sede, nonce y
/// vigencia— no es secreto. Lo que hace falta impedir es que alguien <em>fabrique</em>
/// uno, y para eso basta con que la firma no se pueda reproducir sin la llave.
/// </para>
/// <para>
/// Vive en el dominio, y no en la infraestructura, porque decidir si un código es válido
/// es una regla de negocio: así se prueba con relojes y llaves de mentira, sin levantar
/// base de datos ni servidor.
/// </para>
/// </summary>
public static class SelloQr
{
    private const byte Version = 1;

    // 1 versión + 16 sede + 16 nonce + 8 emitido + 8 expira.
    private const int TamanoPayload = 49;
    private const int TamanoFirma = 32;
    private const int TamanoTotal = TamanoPayload + TamanoFirma;

    // El formato es binario y no JSON para que el código quepa holgado: 81 bytes se
    // vuelven 108 caracteres en base64url, y un QR de ese tamaño se lee de lejos y con
    // mala luz. Un JSON equivalente triplicaría la densidad de módulos.
    public const int LargoTexto = 108;

    public static (TokenQr Token, string Texto) Emitir(
        Guid sedeId,
        DateTimeOffset ahora,
        ReadOnlySpan<byte> llave,
        PoliticaQr? politica = null)
    {
        politica ??= PoliticaQr.Predeterminada;

        var token = new TokenQr(
            sedeId,
            Guid.CreateVersion7(),
            ahora,
            ahora + politica.Vigencia);

        return (token, Emitir(token, llave));
    }

    public static string Emitir(TokenQr token, ReadOnlySpan<byte> llave)
    {
        Span<byte> completo = stackalloc byte[TamanoTotal];

        Escribir(token, completo[..TamanoPayload]);
        HMACSHA256.HashData(llave, completo[..TamanoPayload], completo[TamanoPayload..]);

        return Base64Url.EncodeToString(completo);
    }

    public static ResultadoLecturaQr Leer(
        string? texto,
        ReadOnlySpan<byte> llave,
        DateTimeOffset ahora,
        PoliticaQr? politica = null)
    {
        politica ??= PoliticaQr.Predeterminada;

        if (string.IsNullOrWhiteSpace(texto) || texto.Length != LargoTexto)
        {
            return ResultadoLecturaQr.Rechazo(MotivoRechazoQr.FormatoInvalido);
        }

        Span<byte> completo = stackalloc byte[TamanoTotal];

        if (!Base64Url.TryDecodeFromChars(texto, completo, out var escritos)
            || escritos != TamanoTotal
            || completo[0] != Version)
        {
            return ResultadoLecturaQr.Rechazo(MotivoRechazoQr.FormatoInvalido);
        }

        Span<byte> firmaEsperada = stackalloc byte[TamanoFirma];
        HMACSHA256.HashData(llave, completo[..TamanoPayload], firmaEsperada);

        // Comparación de tiempo fijo: una comparación normal se detiene en el primer byte
        // distinto, y ese tiempo filtra cuántos bytes se acertaron. Con suficientes
        // intentos eso permite reconstruir una firma válida byte por byte.
        if (!CryptographicOperations.FixedTimeEquals(firmaEsperada, completo[TamanoPayload..]))
        {
            return ResultadoLecturaQr.Rechazo(MotivoRechazoQr.FirmaInvalida);
        }

        var token = Interpretar(completo[..TamanoPayload]);

        return ahora > token.ExpiraUtc + politica.Gracia
            ? new ResultadoLecturaQr(false, MotivoRechazoQr.Caducado, token)
            : new ResultadoLecturaQr(true, MotivoRechazoQr.Ninguno, token);
    }

    private static void Escribir(TokenQr token, Span<byte> destino)
    {
        destino[0] = Version;
        token.SedeId.TryWriteBytes(destino.Slice(1, 16), bigEndian: true, out _);
        token.Nonce.TryWriteBytes(destino.Slice(17, 16), bigEndian: true, out _);
        BinaryPrimitives.WriteInt64BigEndian(destino.Slice(33, 8), token.EmitidoUtc.ToUnixTimeSeconds());
        BinaryPrimitives.WriteInt64BigEndian(destino.Slice(41, 8), token.ExpiraUtc.ToUnixTimeSeconds());
    }

    private static TokenQr Interpretar(ReadOnlySpan<byte> payload) => new(
        new Guid(payload.Slice(1, 16), bigEndian: true),
        new Guid(payload.Slice(17, 16), bigEndian: true),
        DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64BigEndian(payload.Slice(33, 8))),
        DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64BigEndian(payload.Slice(41, 8))));
}
