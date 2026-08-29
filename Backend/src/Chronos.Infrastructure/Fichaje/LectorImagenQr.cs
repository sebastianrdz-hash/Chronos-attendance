using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace Chronos.Infrastructure.Fichaje;

public interface ILectorImagenQr
{
    /// <summary>Devuelve el texto del primer QR que encuentre, o null si no hay ninguno.</summary>
    string? Decodificar(byte[] imagen);
}

/// <summary>
/// Decodifica un QR a partir de una foto.
/// <para>
/// El camino normal no pasa por aquí: el teléfono decodifica el código con html5-qrcode
/// y manda el texto ya leído. Esto cubre el caso en que la cámara en vivo no se puede
/// usar —permiso denegado, navegador viejo, WebView incrustada— y la única salida es
/// tomar una foto y subirla.
/// </para>
/// </summary>
public sealed class LectorImagenQr : ILectorImagenQr
{
    // Una imagen de más de 40 megapíxeles no es una foto de un QR, es un intento de
    // agotar la memoria del servidor: comprimida puede ocupar poco y expandirse a cientos
    // de megabytes al decodificarse.
    private const int MaximoPixeles = 40_000_000;

    private static readonly BarcodeReaderGeneric Lector = new()
    {
        Options = new DecodingOptions
        {
            PossibleFormats = [BarcodeFormat.QR_CODE],

            // Una foto de una pantalla llega torcida, con reflejos y desenfocada. Vale la
            // pena el intento extra: la alternativa es que el empleado repita la foto.
            TryHarder = true
        },
        AutoRotate = true
    };

    public string? Decodificar(byte[] imagen)
    {
        try
        {
            // Leer solo la cabecera permite descartar una imagen desmesurada antes de
            // reservar memoria para sus píxeles.
            var informacion = Image.Identify(imagen);
            if (informacion is null || (long)informacion.Width * informacion.Height > MaximoPixeles)
            {
                return null;
            }

            using var mapa = Image.Load<Rgba32>(imagen);

            var pixeles = new byte[mapa.Width * mapa.Height * 4];
            mapa.CopyPixelDataTo(pixeles);

            var fuente = new RGBLuminanceSource(
                pixeles,
                mapa.Width,
                mapa.Height,
                RGBLuminanceSource.BitmapFormat.RGBA32);

            return Lector.Decode(fuente)?.Text;
        }
        catch (Exception excepcion) when (excepcion is UnknownImageFormatException or InvalidImageContentException)
        {
            // Lo que subieron no es una imagen que se pueda interpretar. Para quien ficha
            // es indistinguible de una foto sin QR, así que se trata igual.
            return null;
        }
    }
}
