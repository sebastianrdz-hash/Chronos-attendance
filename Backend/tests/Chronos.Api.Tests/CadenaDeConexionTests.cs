using Chronos.Infrastructure;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Chronos.Api.Tests;

/// <summary>
/// Los proveedores gestionados entregan la conexión como URI y Npgsql solo entiende
/// pares clave-valor. Un fallo aquí no se nota en local: aparece al desplegar, como un
/// error de formato sin pistas sobre qué parte de la cadena está mal.
/// </summary>
public class CadenaDeConexionTests
{
    private static IConfiguration Configuracion(params (string Clave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(par =>
                new KeyValuePair<string, string?>(par.Clave, par.Valor)))
            .Build();

    [Fact]
    public void TraduceLaUriDeNeonAFormatoNpgsql()
    {
        var configuracion = Configuracion((
            "ConnectionStrings:Postgres",
            "postgresql://chronos:secreta@ep-fria-luna-123.us-east-2.aws.neon.tech/chronos?sslmode=require"));

        var resultado = new NpgsqlConnectionStringBuilder(
            InyeccionDependencias.CadenaDeConexion(configuracion));

        Assert.Equal("ep-fria-luna-123.us-east-2.aws.neon.tech", resultado.Host);
        Assert.Equal("chronos", resultado.Database);
        Assert.Equal("chronos", resultado.Username);
        Assert.Equal("secreta", resultado.Password);
        Assert.Equal(SslMode.Require, resultado.SslMode);
        Assert.Equal(5432, resultado.Port);
    }

    [Fact]
    public void DescifraLosCaracteresEscapadosDeLaContrasena()
    {
        // Los generadores de contraseñas producen símbolos que en una URI viajan
        // escapados; pasarlos tal cual a Npgsql provoca un fallo de autenticación.
        var configuracion = Configuracion((
            "ConnectionStrings:Postgres",
            "postgresql://usuario:p%40ss%3Aword%2F1@servidor.neon.tech:6543/base"));

        var resultado = new NpgsqlConnectionStringBuilder(
            InyeccionDependencias.CadenaDeConexion(configuracion));

        Assert.Equal("p@ss:word/1", resultado.Password);
        Assert.Equal(6543, resultado.Port);
    }

    [Fact]
    public void ExigeTlsCuandoLaUriNoLoDeclara()
    {
        var configuracion = Configuracion((
            "ConnectionStrings:Postgres",
            "postgres://usuario:clave@servidor.neon.tech/base"));

        var resultado = new NpgsqlConnectionStringBuilder(
            InyeccionDependencias.CadenaDeConexion(configuracion));

        Assert.Equal(SslMode.Require, resultado.SslMode);
    }

    [Fact]
    public void DejaIntactaLaCadenaEnFormatoClaveValor()
    {
        const string cadena = "Host=localhost;Port=5433;Database=chronos;Username=chronos;Password=local";
        var configuracion = Configuracion(("ConnectionStrings:Postgres", cadena));

        Assert.Equal(cadena, InyeccionDependencias.CadenaDeConexion(configuracion));
    }

    [Fact]
    public void AceptaDatabaseUrlCuandoNoHayCadenaConNombre()
    {
        var configuracion = Configuracion((
            "DATABASE_URL",
            "postgresql://usuario:clave@servidor.neon.tech/base?sslmode=require"));

        var resultado = new NpgsqlConnectionStringBuilder(
            InyeccionDependencias.CadenaDeConexion(configuracion));

        Assert.Equal("servidor.neon.tech", resultado.Host);
    }

    [Fact]
    public void ExplicaQueFaltaLaCadenaCuandoNoHayNinguna()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => InyeccionDependencias.CadenaDeConexion(Configuracion()));

        Assert.Contains("DATABASE_URL", excepcion.Message);
    }
}
