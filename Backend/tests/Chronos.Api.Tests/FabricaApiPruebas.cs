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
                ["Semilla:Contrasena"] = Contrasena
            });
        });
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    public new Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(ColeccionApi))]
public sealed class ColeccionApi : ICollectionFixture<FabricaApiPruebas>;
