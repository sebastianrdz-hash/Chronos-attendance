using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Chronos.Api.Tests;

[Collection(nameof(ColeccionApi))]
public class AutenticacionTests(FabricaApiPruebas fabrica)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record RespuestaLogin(string AccessToken, string TipoToken, JsonElement Usuario);

    private async Task<RespuestaLogin> IniciarSesionAsync(HttpClient cliente, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new
        {
            correo,
            contrasena = FabricaApiPruebas.Contrasena
        });

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaLogin>(Json))!;
    }

    [Fact]
    public async Task LasCredencialesSembradasEmitenUnToken()
    {
        var cliente = fabrica.CreateClient();

        var sesion = await IniciarSesionAsync(cliente, "admin@chronos.mx");

        Assert.False(string.IsNullOrWhiteSpace(sesion.AccessToken));
        Assert.Equal("Bearer", sesion.TipoToken);
        Assert.Equal("EMP-0001", sesion.Usuario.GetProperty("numeroEmpleado").GetString());
    }

    [Theory]
    [InlineData("admin@chronos.mx", "Admin")]
    [InlineData("supervisor@chronos.mx", "Supervisor")]
    [InlineData("empleado@chronos.mx", "Empleado")]
    public async Task CadaCuentaSembradaLlegaConSuRol(string correo, string rolEsperado)
    {
        var cliente = fabrica.CreateClient();

        var sesion = await IniciarSesionAsync(cliente, correo);
        var roles = sesion.Usuario.GetProperty("roles").EnumerateArray().Select(r => r.GetString());

        Assert.Contains(rolEsperado, roles);
    }

    [Fact]
    public async Task UnaContrasenaIncorrectaDevuelve401()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new
        {
            correo = "admin@chronos.mx",
            contrasena = "esta-no-es-la-buena"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnCorreoInexistenteDevuelveLoMismoQueUnaContrasenaMala()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new
        {
            correo = "fantasma@chronos.mx",
            contrasena = FabricaApiPruebas.Contrasena
        });

        // Misma respuesta en ambos casos: distinguirlas permitiría enumerar cuentas.
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElPerfilExigeUnTokenValido()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/auth/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConTokenElPerfilDevuelveLosDatosDelEmpleado()
    {
        var cliente = fabrica.CreateClient();
        var sesion = await IniciarSesionAsync(cliente, "empleado@chronos.mx");

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sesion.AccessToken);

        var perfil = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/auth/yo", Json);

        Assert.Equal("EMP-0003", perfil.GetProperty("numeroEmpleado").GetString());
        Assert.Equal("Corporativo Monterrey", perfil.GetProperty("sede").GetString());
    }

    [Fact]
    public async Task UnTokenAlteradoSeRechaza()
    {
        var cliente = fabrica.CreateClient();
        var sesion = await IniciarSesionAsync(cliente, "admin@chronos.mx");

        var alterado = sesion.AccessToken[..^4] + "aaaa";
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alterado);

        var respuesta = await cliente.GetAsync("/api/v1/auth/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElChequeoDeSaludConfirmaLaConexionAPostgres()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
    }
}
