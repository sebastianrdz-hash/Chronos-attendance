using System.Text;
using Chronos.Domain.Reglas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using QRCoder;

namespace Chronos.Infrastructure.Fichaje;

/// <param name="SegundosRefresco">Cada cuánto debe pedir el kiosco un código nuevo.</param>
public sealed record CodigoQrEmitido(
    string Texto,
    byte[] ImagenPng,
    DateTimeOffset EmitidoUtc,
    DateTimeOffset ExpiraUtc,
    int SegundosRefresco);

public interface IServicioQr
{
    PoliticaQr Politica { get; }

    CodigoQrEmitido Emitir(Guid sedeId, DateTimeOffset ahora);

    ResultadoLecturaQr Leer(string? texto, DateTimeOffset ahora);
}

public sealed class ServicioQr : IServicioQr
{
    private readonly OpcionesQr _opciones;
    private readonly byte[] _llave;

    public ServicioQr(IOptions<OpcionesQr> opciones)
    {
        _opciones = opciones.Value;
        _llave = Encoding.UTF8.GetBytes(_opciones.Llave);

        Politica = new PoliticaQr
        {
            Vigencia = TimeSpan.FromSeconds(_opciones.SegundosVigencia),
            Gracia = TimeSpan.FromSeconds(_opciones.SegundosGracia)
        };
    }

    public PoliticaQr Politica { get; }

    public CodigoQrEmitido Emitir(Guid sedeId, DateTimeOffset ahora)
    {
        var (token, texto) = SelloQr.Emitir(sedeId, ahora, _llave, Politica);

        return new CodigoQrEmitido(
            texto,
            Dibujar(texto),
            token.EmitidoUtc,
            token.ExpiraUtc,
            (int)Politica.Refresco.TotalSeconds);
    }

    public ResultadoLecturaQr Leer(string? texto, DateTimeOffset ahora) =>
        SelloQr.Leer(texto, _llave, ahora, Politica);

    /// <summary>
    /// PngByteQRCode escribe el PNG byte a byte en código administrado. Los demás
    /// renderizadores de QRCoder pasan por System.Drawing, que no existe en la imagen
    /// Alpine con la que se despliega la API.
    /// </summary>
    private byte[] Dibujar(string texto)
    {
        using var generador = new QRCodeGenerator();

        // Corrección media: tolera cerca de un 15% de daño, que es lo que hace falta para
        // leer una pantalla con reflejos sin inflar el número de módulos.
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);

        var modulos = datos.ModuleMatrix.Count;
        var pixelesPorModulo = Math.Max(1, _opciones.PixelesImagen / modulos);

        return new PngByteQRCode(datos).GetGraphic(pixelesPorModulo);
    }

    /// <summary>
    /// Distingue el choque contra la llave primaria de la tabla de nonces de cualquier
    /// otro fallo al guardar. Sin esta comprobación, un error de red se confundiría con
    /// un código reutilizado y el empleado recibiría una acusación falsa.
    /// </summary>
    public static bool EsNonceDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.TableName == "nonces_qr_consumidos";
}
