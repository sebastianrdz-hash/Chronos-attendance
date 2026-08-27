using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chronos.Infrastructure.Persistencia;

/// <summary>
/// Permite que "dotnet ef" cree el contexto sin levantar la API completa.
/// Toma la cadena de ConnectionStrings__Postgres si existe y si no usa la de docker-compose.
/// </summary>
public sealed class ChronosDbContextFactory : IDesignTimeDbContextFactory<ChronosDbContext>
{
    private const string CadenaLocal =
        "Host=localhost;Port=5433;Database=chronos;Username=chronos;Password=chronos_dev";

    public ChronosDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? CadenaLocal;

        var opciones = new DbContextOptionsBuilder<ChronosDbContext>()
            .UseNpgsql(cadena, npgsql => npgsql
                .MigrationsAssembly(typeof(ChronosDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ChronosDbContext(opciones);
    }
}
