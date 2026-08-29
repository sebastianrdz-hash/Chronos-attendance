using System.Net;
using System.Net.Http.Json;
using System.Text;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;
using Chronos.Domain.ValueObjects;
using Chronos.Infrastructure.Persistencia;

namespace Chronos.Api.Tests;

[Collection(nameof(ColeccionApi))]
public class FichajeQrTests(FabricaApiPruebas fabrica)
{
    private static readonly byte[] Llave = Encoding.UTF8.GetBytes(FabricaApiPruebas.LlaveQr);

    private static readonly byte[] LlaveImpostora = Encoding.UTF8.GetBytes("llave-de-un-atacante-con-el-largo-minimo");

    private static readonly Guid Monterrey = SembradorDatos.Ids.SedeMonterrey;

    private static readonly Guid Guadalajara = SembradorDatos.Ids.SedeGuadalajara;

    // ---------- Kiosco ----------

    [Fact]
    public async Task ElKioscoEntregaCodigoImagenYVigencia()
    {
        var codigo = await CodigoDeAsync(Monterrey);

        Assert.Equal(SelloQr.LargoTexto, codigo.Token.Length);
        Assert.NotEmpty(codigo.ImagenPng);
        Assert.True(codigo.ExpiraUtc > codigo.EmitidoUtc);
        Assert.InRange(codigo.SegundosRefresco, 1, 30);
        Assert.Equal("Corporativo Monterrey", codigo.SedeNombre);

        // La imagen debe ser un PNG de verdad, no un base64 cualquiera.
        var bytes = Convert.FromBase64String(codigo.ImagenPng);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], bytes[..4]);
    }

    [Fact]
    public async Task CadaLlamadaAlKioscoEmiteUnCodigoDistinto()
    {
        var primero = await CodigoDeAsync(Monterrey);
        var segundo = await CodigoDeAsync(Monterrey);

        Assert.NotEqual(primero.Token, segundo.Token);
    }

    [Fact]
    public async Task UnEmpleadoNoPuedeAbrirElKiosco()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await cliente.GetAsync($"/api/v1/kiosco/{Monterrey}/codigo");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnSupervisorSoloAbreElKioscoDeSuPropiaSede()
    {
        // Diego lidera Operaciones, en Guadalajara.
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.SupervisorOperaciones);

        var propia = await cliente.GetAsync($"/api/v1/kiosco/{Guadalajara}/codigo");
        var ajena = await cliente.GetAsync($"/api/v1/kiosco/{Monterrey}/codigo");

        Assert.Equal(HttpStatusCode.OK, propia.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
    }

    // ---------- Fichaje correcto ----------

    [Fact]
    public async Task UnCodigoValidoRegistraLaChecadaConSuSenalDeQr()
    {
        var codigo = await CodigoDeAsync(Monterrey);
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await Fichar(cliente, codigo.Token);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var checada = await respuesta.LeerAsync<ChecadaDto>();

        Assert.Equal(TipoChecada.Entrada, checada.Tipo);
        Assert.Equal(Monterrey, checada.SedeId);

        var senal = Assert.Single(checada.Senales);
        Assert.Equal(TipoSenal.CodigoQr, senal.Tipo);
        Assert.Equal(ResultadoSenal.Confirmada, senal.Resultado);
        Assert.Equal(25, senal.PesoAplicado);
    }

    [Fact]
    public async Task UnFichajeSoloConQrNoAlcanzaElUmbralYQuedaParaRevision()
    {
        var codigo = await CodigoDeAsync(Monterrey);
        var cliente = await fabrica.ComoAsync("gabriela.ponce@chronos.mx");

        var checada = await (await Fichar(cliente, codigo.Token)).LeerAsync<ChecadaDto>();

        // Es el corazón del modelo: el QR prueba que se estuvo frente a la pantalla, no
        // quién estuvo. Sin WebAuthn se queda en 25 de los 70 que exige la confianza alta.
        Assert.Equal(25, checada.PuntajeConfianza);
        Assert.Equal(NivelConfianza.Baja, checada.NivelConfianza);
        Assert.Equal(EstadoChecada.RequiereRevision, checada.Estado);
    }

    [Fact]
    public async Task ElSegundoFichajeDelDiaSeInterpretaComoSalida()
    {
        var cliente = await fabrica.ComoAsync("irene.rosales@chronos.mx");

        var entrada = await (await Fichar(cliente, (await CodigoDeAsync(Guadalajara)).Token)).LeerAsync<ChecadaDto>();
        var salida = await (await Fichar(cliente, (await CodigoDeAsync(Guadalajara)).Token)).LeerAsync<ChecadaDto>();

        Assert.Equal(TipoChecada.Entrada, entrada.Tipo);
        Assert.Equal(TipoChecada.Salida, salida.Tipo);
    }

    // ---------- Rechazos ----------

    [Fact]
    public async Task ReusarUnCodigoYaConsumidoSeRechaza()
    {
        var codigo = await CodigoDeAsync(Monterrey);
        var cliente = await fabrica.ComoAsync("fernando.ochoa@chronos.mx");

        var primera = await Fichar(cliente, codigo.Token);
        var segunda = await Fichar(cliente, codigo.Token);

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        await EsperarRechazo(segunda, MotivoRechazoQr.NonceReusado);
    }

    [Fact]
    public async Task OtroEmpleadoTampocoPuedeReusarUnCodigoAjeno()
    {
        var codigo = await CodigoDeAsync(Monterrey);

        var primero = await fabrica.ComoAsync("hector.quintero@chronos.mx");
        var segundo = await fabrica.ComoAsync("nestor.alarcon@chronos.mx");

        var suya = await Fichar(primero, codigo.Token);

        // El escenario que se quiere impedir: una foto del código reenviada por mensajería.
        var reenviada = await Fichar(segundo, codigo.Token);

        Assert.Equal(HttpStatusCode.Created, suya.StatusCode);
        await EsperarRechazo(reenviada, MotivoRechazoQr.NonceReusado);
    }

    [Fact]
    public async Task UnCodigoCaducadoSeRechaza()
    {
        var cliente = await fabrica.ComoAsync("javier.trejo@chronos.mx");
        var vencido = Fabricar(Guadalajara, DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-9));

        await EsperarRechazo(await Fichar(cliente, vencido), MotivoRechazoQr.Caducado);
    }

    [Fact]
    public async Task UnCodigoFirmadoConOtraLlaveSeRechaza()
    {
        var cliente = await fabrica.ComoAsync("javier.trejo@chronos.mx");

        var ahora = DateTimeOffset.UtcNow;
        var falsificado = Fabricar(Guadalajara, ahora, ahora.AddSeconds(30), LlaveImpostora);

        await EsperarRechazo(await Fichar(cliente, falsificado), MotivoRechazoQr.FirmaInvalida);
    }

    [Fact]
    public async Task UnCodigoDeOtraSedeSeRechaza()
    {
        // Karina trabaja en Guadalajara y presenta un código emitido en Monterrey.
        var cliente = await fabrica.ComoAsync("karina.uribe@chronos.mx");
        var codigo = await CodigoDeAsync(Monterrey);

        await EsperarRechazo(await Fichar(cliente, codigo.Token), MotivoRechazoQr.SedeNoCorresponde);
    }

    [Fact]
    public async Task UnTextoQueNoEsCodigoSeRechazaPorFormato()
    {
        var cliente = await fabrica.ComoAsync("javier.trejo@chronos.mx");

        await EsperarRechazo(await Fichar(cliente, "esto-no-es-un-codigo"), MotivoRechazoQr.FormatoInvalido);
    }

    [Fact]
    public async Task RepetirElMismoTipoDeFichajeDentroDeLaVentanaSeRechaza()
    {
        var cliente = await fabrica.ComoAsync("mariana.zamora@chronos.mx");

        var primera = await Fichar(cliente, (await CodigoDeAsync(Guadalajara)).Token, TipoChecada.Entrada);
        var repetida = await Fichar(cliente, (await CodigoDeAsync(Guadalajara)).Token, TipoChecada.Entrada);

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task SinSesionNoSePuedeFichar()
    {
        var cliente = fabrica.CreateClient();
        var codigo = await CodigoDeAsync(Monterrey);

        var respuesta = await Fichar(cliente, codigo.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // ---------- Historial ----------

    [Fact]
    public async Task ElHistorialPropioDevuelveLaChecadaConElDetalleDeSusSenales()
    {
        var cliente = await fabrica.ComoAsync("luis.valdes@chronos.mx");
        await Fichar(cliente, (await CodigoDeAsync(Guadalajara)).Token);

        var historial = await (await cliente.GetAsync("/api/v1/checadas/mias"))
            .LeerAsync<ResultadoPaginado<ChecadaDto>>();

        Assert.True(historial.Total >= 1);

        var reciente = historial.Elementos[0];
        Assert.NotEmpty(reciente.Senales);
        Assert.Equal(TipoSenal.CodigoQr, reciente.Senales[0].Tipo);
        Assert.Equal("Código QR", reciente.Senales[0].TipoNombre);
        Assert.Contains("nonce", reciente.Senales[0].DetalleJson);
    }

    [Fact]
    public async Task ElHistorialNoDejaVerLasChecadasDeOtroEmpleado()
    {
        var ajena = await fabrica.ComoAsync("elena.navarro@chronos.mx");
        var deElena = await (await Fichar(ajena, (await CodigoDeAsync(Guadalajara)).Token))
            .LeerAsync<ChecadaDto>();

        var propia = await fabrica.ComoAsync("gabriela.ponce@chronos.mx");
        var historial = await (await propia.GetAsync("/api/v1/checadas/mias"))
            .LeerAsync<ResultadoPaginado<ChecadaDto>>();

        Assert.DoesNotContain(historial.Elementos, checada => checada.Id == deElena.Id);
    }

    // ---------- Ayudantes ----------

    private async Task<CodigoKioscoDto> CodigoDeAsync(Guid sedeId)
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var respuesta = await admin.GetAsync($"/api/v1/kiosco/{sedeId}/codigo");

        respuesta.EnsureSuccessStatusCode();

        return await respuesta.LeerAsync<CodigoKioscoDto>();
    }

    private static Task<HttpResponseMessage> Fichar(HttpClient cliente, string token, TipoChecada? tipo = null) =>
        cliente.PostAsJsonAsync(
            "/api/v1/checadas/qr",
            new SolicitudChecadaQr { Token = token, Tipo = tipo },
            ClienteAutenticado.Json);

    private static string Fabricar(
        Guid sedeId,
        DateTimeOffset emitido,
        DateTimeOffset expira,
        byte[]? llave = null) =>
        SelloQr.Emitir(new TokenQr(sedeId, Guid.CreateVersion7(), emitido, expira), llave ?? Llave);

    private static async Task EsperarRechazo(HttpResponseMessage respuesta, MotivoRechazoQr esperado)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);

        var rechazo = await respuesta.LeerAsync<RechazoChecadaDto>();

        Assert.Equal(esperado, rechazo.Motivo);
        Assert.NotEmpty(rechazo.Mensaje);
    }
}
