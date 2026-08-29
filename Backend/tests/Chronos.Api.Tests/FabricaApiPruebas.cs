using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Chronos.Api.Tests;

/// <summary>
/// Levanta la API contra un PostgreSQL real y efímero. Se usa la misma imagen que
/// docker-compose para que las pruebas ejerciten las migraciones tal cual se aplicarán.
/// </summary>
public sealed class FabricaApiPruebas : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Contrasena = "Chronos#2026";

    /// <summary>
    /// Se fija aquí para que las pruebas puedan fabricar tokens a mano: firmar uno con
    /// fecha pasada es la única forma de comprobar el rechazo por caducidad sin depender
    /// de esperas reales ni de manipular el reloj del proceso.
    /// </summary>
    public const string LlaveQr = "llave-de-pruebas-qr-con-largo-mas-que-suficiente";

    /// <summary>
    /// El autenticador de software de las pruebas firma contra este dominio y este origen,
    /// así que tienen que ser exactamente los que la API espera: buena parte de lo que se
    /// verifica en WebAuthn es justamente que ambos coincidan.
    /// </summary>
    public const string RpId = "localhost";

    public const string Origen = "https://localhost:5173";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("chronos_pruebas")
        .WithUsername("chronos")
        .WithPassword("chronos_pruebas")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:Llave"] = "llave-de-pruebas-de-integracion-con-largo-suficiente",
                ["Jwt:MinutosVigencia"] = "15",
                ["Semilla:EjecutarAlIniciar"] = "true",
                ["Semilla:Contrasena"] = Contrasena,
                ["Qr:Llave"] = LlaveQr,
                ["Qr:SegundosVigencia"] = "30",
                ["Qr:SegundosGracia"] = "5",
                ["WebAuthn:RpId"] = RpId,
                ["WebAuthn:NombreRp"] = "Chronos Pruebas",
                ["WebAuthn:OrigenesPermitidos:0"] = Origen,
                ["WebAuthn:SegundosVigenciaDesafio"] = "120"
            });
        });
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    public new Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(ColeccionApi))]
public sealed class ColeccionApi : ICollectionFixture<FabricaApiPruebas>;
