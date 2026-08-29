using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Api.Tests;

/// <summary>
/// Flujo completo de WebAuthn contra un autenticador de software que firma de verdad.
/// <para>
/// Cada prueba da de alta su propio empleado en lugar de reutilizar la semilla. Una
/// credencial enrolada queda pegada al expediente y la ventana antiduplicados dura cinco
/// minutos, así que compartir cuentas con las pruebas de QR acabaría en fallos que
/// dependen del orden de ejecución.
/// </para>
/// </summary>
[Collection(nameof(ColeccionApi))]
public class FichajeWebAuthnTests(FabricaApiPruebas fabrica)
{
    private static readonly Guid Monterrey = SembradorDatos.Ids.SedeMonterrey;

    // ---------- Enrolamiento ----------

    [Fact]
    public async Task ElEnrolamientoGuardaLaClavePublicaYNoUnRasgoBiometrico()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        var credencial = await EnrolarAsync(cliente, llavero, "Laptop del trabajo");

        Assert.Equal("Laptop del trabajo", credencial.NombreAmigable);
        Assert.True(credencial.Activa);

        using var alcance = fabrica.Services.CreateScope();
        var bd = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

        var guardada = await bd.CredencialesWebAuthn.AsNoTracking().FirstAsync(c => c.Id == credencial.Id);

        // Lo que queda en la base es la clave pública que emitió el autenticador y su
        // identificador. No hay ningún campo donde pudiera haberse colado una huella.
        Assert.NotEmpty(guardada.ClavePublica);
        Assert.Equal(llavero.CredentialId, guardada.CredentialId);
    }

    [Fact]
    public async Task UnEmpleadoPuedeRegistrarVariosDispositivosYVerlosListados()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var laptop = NuevoAutenticador();
        using var celular = NuevoAutenticador();

        await EnrolarAsync(cliente, laptop, "Laptop");
        await EnrolarAsync(cliente, celular, "Celular");

        var lista = await (await cliente.GetAsync("/api/v1/webauthn/credenciales"))
            .LeerAsync<List<CredencialDto>>();

        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, c => c.NombreAmigable == "Laptop");
        Assert.Contains(lista, c => c.NombreAmigable == "Celular");
    }

    [Fact]
    public async Task UnEnrolamientoFirmadoDesdeOtroOrigenSeRechaza()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        var desafio = await DesafioDeEnrolamientoAsync(cliente, "Impostor");

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/webauthn/enrolamiento",
            new { respuesta = llavero.Registrar(desafio, origenFalso: "https://chronos-falso.mx") },
            ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Empty(await (await cliente.GetAsync("/api/v1/webauthn/credenciales")).LeerAsync<List<CredencialDto>>());
    }

    [Fact]
    public async Task ElEnrolamientoExigeUnNombreDeDispositivo()
    {
        var cliente = await NuevoEmpleadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/webauthn/enrolamiento/opciones",
            new { nombreAmigable = "" },
            ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ---------- Fichaje con las dos señales ----------

    [Fact]
    public async Task ElQrConFirmaBiometricaAlcanzaConfianzaAlta()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        await EnrolarAsync(cliente, llavero, "Celular");

        var checada = await FicharConFirmaAsync(cliente, llavero);

        // Veinticinco del QR más cuarenta y cinco de WebAuthn: setenta puntos, justo el
        // umbral de confianza alta. Ninguna de las dos señales llega sola.
        Assert.Equal(70, checada.PuntajeConfianza);
        Assert.Equal(NivelConfianza.Alta, checada.NivelConfianza);
        Assert.Equal(EstadoChecada.Verificada, checada.Estado);

        var webAuthn = Assert.Single(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn);

        Assert.Equal(ResultadoSenal.Confirmada, webAuthn.Resultado);
        Assert.Equal(45, webAuthn.PesoAplicado);
        Assert.Equal("Biometría del dispositivo", webAuthn.TipoNombre);
    }

    [Fact]
    public async Task SoloConElQrLaChecadaSeQuedaEsperandoRevision()
    {
        var cliente = await NuevoEmpleadoAsync();

        var checada = await FicharAsync(cliente, (await CodigoAsync()).Token);

        Assert.Equal(25, checada.PuntajeConfianza);
        Assert.Equal(EstadoChecada.RequiereRevision, checada.Estado);
        Assert.DoesNotContain(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn);
    }

    [Fact]
    public async Task ElUsoDeLaCredencialQuedaFechadoYElContadorAvanza()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        var credencial = await EnrolarAsync(cliente, llavero, "Llave");
        Assert.Null(credencial.UltimoUsoUtc);

        await FicharConFirmaAsync(cliente, llavero);

        var despues = (await (await cliente.GetAsync("/api/v1/webauthn/credenciales"))
                .LeerAsync<List<CredencialDto>>())
            .Single(c => c.Id == credencial.Id);

        Assert.NotNull(despues.UltimoUsoUtc);
        Assert.True(despues.ContadorFirmas > credencial.ContadorFirmas);
    }

    // ---------- Lo que tiene que fallar ----------

    [Fact]
    public async Task UnaFirmaHechaConOtraClaveNoSumaPuntos()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        await EnrolarAsync(cliente, llavero, "Llave");

        // El identificador de la credencial viaja en claro en cada ceremonia, así que un
        // atacante puede conocerlo. Lo que no puede es tener la clave privada.
        using var claveRobada = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var checada = await FicharConFirmaAsync(
            cliente, llavero, personalizar: d => llavero.Firmar(d, claveFalsa: claveRobada));

        var senal = Assert.Single(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn);

        Assert.Equal(ResultadoSenal.Fallida, senal.Resultado);
        Assert.Contains("FirmaInvalida", senal.DetalleJson);
        Assert.NotEqual(NivelConfianza.Alta, checada.NivelConfianza);
    }

    [Fact]
    public async Task UnaFirmaDesdeUnSitioSuplantadoNoSumaPuntos()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        await EnrolarAsync(cliente, llavero, "Llave");

        // El origen va dentro de lo que se firma: una página de phishing no puede mentir
        // sobre él sin invalidar la firma. Es la defensa que un OTP por SMS no tiene.
        var checada = await FicharConFirmaAsync(
            cliente, llavero, personalizar: d => llavero.Firmar(d, origenFalso: "https://chronos-falso.mx"));

        Assert.Equal(
            ResultadoSenal.Fallida,
            Assert.Single(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn).Resultado);
    }

    [Fact]
    public async Task UnContadorQueNoAvanzaDelataUnaCredencialClonada()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        await EnrolarAsync(cliente, llavero, "Llave");
        await FicharConFirmaAsync(cliente, llavero);

        // Una copia de la llave firmaría con el contador que tenía al ser clonada, sin
        // saber cuántas veces se usó la original desde entonces.
        var checada = await FicharConFirmaAsync(
            cliente, llavero, TipoChecada.Salida, d => llavero.Firmar(d, contadorFalso: 1));

        Assert.Equal(
            ResultadoSenal.Fallida,
            Assert.Single(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn).Resultado);
    }

    [Fact]
    public async Task UnDesafioNoSePuedeCanjearDosVeces()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        await EnrolarAsync(cliente, llavero, "Llave");

        var firma = llavero.Firmar(await DesafioDeAutenticacionAsync(cliente));

        var primera = await FicharAsync(cliente, (await CodigoAsync()).Token, firma);
        var segunda = await FicharAsync(cliente, (await CodigoAsync()).Token, firma, TipoChecada.Salida);

        Assert.Equal(
            ResultadoSenal.Confirmada,
            Assert.Single(primera.Senales, s => s.Tipo == TipoSenal.WebAuthn).Resultado);

        var reintento = Assert.Single(segunda.Senales, s => s.Tipo == TipoSenal.WebAuthn);

        Assert.Equal(ResultadoSenal.Fallida, reintento.Resultado);
        Assert.Contains("DesafioNoVigente", reintento.DetalleJson);
    }

    [Fact]
    public async Task UnDesafioDeEnrolamientoNoSirveParaFichar()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        await EnrolarAsync(cliente, llavero, "Llave");

        // Se pide un reto con la excusa de dar de alta otro dispositivo y se intenta
        // canjear al fichar. Los propósitos están separados justo para impedir esto.
        var desafio = await DesafioDeEnrolamientoAsync(cliente, "Otro aparato");

        var checada = await FicharAsync(cliente, (await CodigoAsync()).Token, llavero.Firmar(desafio));

        var senal = Assert.Single(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn);

        Assert.Equal(ResultadoSenal.Fallida, senal.Resultado);
        Assert.Contains("DesafioNoVigente", senal.DetalleJson);
    }

    [Fact]
    public async Task UnaCredencialRevocadaDejaDeServir()
    {
        var cliente = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        var credencial = await EnrolarAsync(cliente, llavero, "Llave perdida");

        // Se firma antes de revocar: la firma es criptográficamente impecable y aun así no
        // debe valer, porque la credencial ya no está vigente.
        var firma = llavero.Firmar(await DesafioDeAutenticacionAsync(cliente));

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await cliente.DeleteAsync($"/api/v1/webauthn/credenciales/{credencial.Id}")).StatusCode);

        var checada = await FicharAsync(cliente, (await CodigoAsync()).Token, firma);

        var senal = Assert.Single(checada.Senales, s => s.Tipo == TipoSenal.WebAuthn);

        Assert.Equal(ResultadoSenal.Fallida, senal.Resultado);
        Assert.Contains("CredencialDesconocida", senal.DetalleJson);

        Assert.Empty(await (await cliente.GetAsync("/api/v1/webauthn/credenciales")).LeerAsync<List<CredencialDto>>());
    }

    [Fact]
    public async Task NadiePuedeRevocarLaCredencialDeOtroEmpleado()
    {
        var duena = await NuevoEmpleadoAsync();
        using var llavero = NuevoAutenticador();

        var credencial = await EnrolarAsync(duena, llavero, "Llave ajena");

        var ajeno = await NuevoEmpleadoAsync();
        var intento = await ajeno.DeleteAsync($"/api/v1/webauthn/credenciales/{credencial.Id}");

        Assert.Equal(HttpStatusCode.NotFound, intento.StatusCode);

        var sigueViva = await (await duena.GetAsync("/api/v1/webauthn/credenciales"))
            .LeerAsync<List<CredencialDto>>();

        Assert.Contains(sigueViva, c => c.Id == credencial.Id);
    }

    [Fact]
    public async Task SinCredencialesRegistradasNoSeEmiteDesafio()
    {
        var cliente = await NuevoEmpleadoAsync();

        var respuesta = await cliente.PostAsync("/api/v1/webauthn/autenticacion/opciones", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    // ---------- Ayudantes ----------

    private static AutenticadorDeSoftware NuevoAutenticador() =>
        new(FabricaApiPruebas.RpId, FabricaApiPruebas.Origen);

    /// <summary>
    /// Da de alta un empleado en Monterrey, le quita el candado de contraseña temporal y
    /// devuelve un cliente ya autenticado como él.
    /// </summary>
    private async Task<HttpClient> NuevoEmpleadoAsync()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var correo = $"webauthn.{sufijo}@chronos.mx";

        var alta = await (await admin.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = $"W-{sufijo}",
            Nombres = "Prueba",
            ApellidoPaterno = "WebAuthn",
            CorreoCorporativo = correo,
            FechaIngreso = new DateOnly(2026, 1, 1),
            DepartamentoId = SembradorDatos.Ids.DeptoSistemas,
            SedeId = Monterrey,
            Rol = RolChronos.Empleado
        }, ClienteAutenticado.Json)).LeerAsync<RespuestaAltaEmpleado>();

        var cliente = fabrica.CreateClient();
        var temporal = await ClienteAutenticado.EntrarAsync(cliente, correo, alta.ContrasenaTemporal);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", temporal.AccessToken);

        var cambio = await cliente.PostAsJsonAsync("/api/v1/perfil/contrasena", new SolicitudCambioContrasena
        {
            ContrasenaActual = alta.ContrasenaTemporal,
            ContrasenaNueva = FabricaApiPruebas.Contrasena,
            Confirmacion = FabricaApiPruebas.Contrasena
        }, ClienteAutenticado.Json);

        cambio.EnsureSuccessStatusCode();

        var sesion = await ClienteAutenticado.EntrarAsync(cliente, correo, FabricaApiPruebas.Contrasena);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", sesion.AccessToken);

        return cliente;
    }

    private static async Task<CredencialDto> EnrolarAsync(
        HttpClient cliente,
        AutenticadorDeSoftware llavero,
        string nombre)
    {
        var desafio = await DesafioDeEnrolamientoAsync(cliente, nombre);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/webauthn/enrolamiento",
            new { respuesta = llavero.Registrar(desafio) },
            ClienteAutenticado.Json);

        await respuesta.AsegurarExitoAsync();

        return await respuesta.LeerAsync<CredencialDto>();
    }

    private static async Task<string> DesafioDeEnrolamientoAsync(HttpClient cliente, string nombre)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/webauthn/enrolamiento/opciones",
            new { nombreAmigable = nombre },
            ClienteAutenticado.Json);

        respuesta.EnsureSuccessStatusCode();

        return await DesafioAsync(respuesta);
    }

    private static async Task<string> DesafioDeAutenticacionAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsync("/api/v1/webauthn/autenticacion/opciones", null);

        respuesta.EnsureSuccessStatusCode();

        return await DesafioAsync(respuesta);
    }

    /// <summary>
    /// El reto viaja en base64url dentro de las opciones y es lo único que el autenticador
    /// necesita del servidor para poder firmar.
    /// </summary>
    private static async Task<string> DesafioAsync(HttpResponseMessage respuesta)
    {
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return documento.RootElement.GetProperty("opciones").GetProperty("challenge").GetString()!;
    }

    private async Task<ChecadaDto> FicharConFirmaAsync(
        HttpClient cliente,
        AutenticadorDeSoftware llavero,
        TipoChecada? tipo = null,
        Func<string, object>? personalizar = null)
    {
        var desafio = await DesafioDeAutenticacionAsync(cliente);
        var firma = personalizar is null ? llavero.Firmar(desafio) : personalizar(desafio);

        return await FicharAsync(cliente, (await CodigoAsync()).Token, firma, tipo);
    }

    private static async Task<ChecadaDto> FicharAsync(
        HttpClient cliente,
        string token,
        object? asercion = null,
        TipoChecada? tipo = null)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/checadas/qr",
            new { token, tipo, asercion },
            ClienteAutenticado.Json);

        respuesta.EnsureSuccessStatusCode();

        return await respuesta.LeerAsync<ChecadaDto>();
    }

    private async Task<CodigoKioscoDto> CodigoAsync()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var respuesta = await admin.GetAsync($"/api/v1/kiosco/{Monterrey}/codigo");

        respuesta.EnsureSuccessStatusCode();

        return await respuesta.LeerAsync<CodigoKioscoDto>();
    }
}
