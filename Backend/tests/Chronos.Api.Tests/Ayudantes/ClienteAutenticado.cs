using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Api.Contratos;

namespace Chronos.Api.Tests.Ayudantes;

/// <summary>
/// Atajos para hablar con la API ya autenticado. Cada prueba pide el cliente del rol que
/// necesita en vez de repetir el login y el armado de la cabecera.
/// </summary>
public static class ClienteAutenticado
{
    public const string Admin = "admin@chronos.mx";
    public const string SupervisorRh = "supervisor@chronos.mx";
    public const string SupervisorOperaciones = "diego.fuentes@chronos.mx";
    public const string Empleada = "empleado@chronos.mx";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<HttpClient> ComoAsync(this FabricaApiPruebas fabrica, string correo)
    {
        var cliente = fabrica.CreateClient();
        var sesion = await EntrarAsync(cliente, correo, FabricaApiPruebas.Contrasena);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.AccessToken);
        return cliente;
    }

    public static async Task<RespuestaLogin> EntrarAsync(HttpClient cliente, string correo, string contrasena)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/auth/login", new SolicitudLogin(correo, contrasena), Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaLogin>(Json))!;
    }

    public static async Task<T> LeerAsync<T>(this HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Errores por campo de un ValidationProblemDetails.</summary>
    public static async Task<Dictionary<string, string[]>> ErroresAsync(this HttpResponseMessage respuesta)
    {
        var documento = await respuesta.Content.ReadFromJsonAsync<JsonElement>(Json);

        return documento.TryGetProperty("errors", out var errores)
            ? errores.Deserialize<Dictionary<string, string[]>>(Json) ?? []
            : [];
    }

    public static async Task<string> DetalleAsync(this HttpResponseMessage respuesta)
    {
        var documento = await respuesta.Content.ReadFromJsonAsync<JsonElement>(Json);

        return documento.TryGetProperty("detail", out var detalle)
            ? detalle.GetString() ?? string.Empty
            : string.Empty;
    }
}
