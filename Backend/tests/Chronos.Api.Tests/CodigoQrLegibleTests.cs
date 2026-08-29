using Chronos.Domain.Enums;
using Chronos.Infrastructure.Fichaje;
using Microsoft.Extensions.Options;

namespace Chronos.Api.Tests;

/// <summary>
/// Cierra el círculo entre las dos bibliotecas: lo que dibuja QRCoder tiene que poder
/// leerlo un decodificador de verdad. Las pruebas de integración validan el token como
/// texto, así que sin esto un PNG corrupto o demasiado denso pasaría desapercibido hasta
/// que alguien apuntara un teléfono a la pantalla.
/// </summary>
public class CodigoQrLegibleTests
{
    private static readonly ServicioQr Servicio = new(Options.Create(new OpcionesQr
    {
        Llave = FabricaApiPruebas.LlaveQr,
        SegundosVigencia = 30,
        PixelesImagen = 512
    }));

    private static readonly LectorImagenQr Lector = new();

    [Fact]
    public void LaImagenDelKioscoSePuedeDecodificarYValidar()
    {
        var sede = Guid.CreateVersion7();
        var ahora = DateTimeOffset.UtcNow;

        var codigo = Servicio.Emitir(sede, ahora);

        var leido = Lector.Decodificar(codigo.ImagenPng);

        Assert.Equal(codigo.Texto, leido);

        // Y el texto recuperado de la imagen sigue siendo un token válido.
        var lectura = Servicio.Leer(leido, ahora);

        Assert.True(lectura.Valido);
        Assert.Equal(sede, lectura.Token.SedeId);
    }

    [Fact]
    public void UnaImagenSinCodigoDevuelveNulo()
    {
        // PNG de 1x1 píxel transparente.
        var vacia = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        Assert.Null(Lector.Decodificar(vacia));
    }

    [Fact]
    public void UnArchivoQueNoEsImagenNoRevienta()
    {
        Assert.Null(Lector.Decodificar("esto es un archivo de texto"u8.ToArray()));
    }

    [Fact]
    public void LaImagenPesaLoRazonableParaRefrescarseCadaPocosSegundos()
    {
        var codigo = Servicio.Emitir(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        // El kiosco la vuelve a pedir cada 20 segundos: si pesara cientos de kilobytes,
        // una sede con varias pantallas saturaría el enlace sin necesidad.
        Assert.InRange(codigo.ImagenPng.Length, 100, 60_000);
    }

    [Fact]
    public void ElServicioRespetaLaVigenciaConfigurada()
    {
        var servicio = new ServicioQr(Options.Create(new OpcionesQr
        {
            Llave = FabricaApiPruebas.LlaveQr,
            SegundosVigencia = 45,
            SegundosGracia = 0
        }));

        var ahora = DateTimeOffset.UtcNow;
        var codigo = servicio.Emitir(Guid.CreateVersion7(), ahora);

        Assert.Equal(45, (codigo.ExpiraUtc - codigo.EmitidoUtc).TotalSeconds, 1);
        Assert.Equal(MotivoRechazoQr.Caducado, servicio.Leer(codigo.Texto, ahora.AddSeconds(46)).Motivo);
    }
}
