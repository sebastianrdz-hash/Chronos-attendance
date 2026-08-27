using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Persistencia;
using Chronos.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;

namespace Chronos.Infrastructure;

public static class InyeccionDependencias
{
    public const string NombreCadenaConexion = "Postgres";

    public static IServiceCollection AgregarInfraestructura(this IServiceCollection servicios)
    {
        // La cadena se resuelve desde el contenedor de servicios, no al registrar: así
        // valen las fuentes de configuración que se agreguen después (variables de
        // entorno del contenedor, overrides de las pruebas de integración).
        servicios.AddDbContext<ChronosDbContext>((proveedor, opciones) => opciones
            .UseNpgsql(
                CadenaDeConexion(proveedor.GetRequiredService<IConfiguration>()),
                npgsql => npgsql.MigrationsAssembly(typeof(ChronosDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention());

        servicios
            .AddIdentityCore<UsuarioAplicacion>(opciones =>
            {
                opciones.User.RequireUniqueEmail = true;
                opciones.Password.RequiredLength = 10;
                opciones.Password.RequireDigit = true;
                opciones.Password.RequireUppercase = true;
                opciones.Password.RequireNonAlphanumeric = true;
                opciones.Lockout.MaxFailedAccessAttempts = 5;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // Alinea a Identity con los claims que emite el JWT; de lo contrario
                // UserManager busca ClaimTypes.NameIdentifier y no encuentra al usuario.
                opciones.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
                opciones.ClaimsIdentity.EmailClaimType = JwtRegisteredClaimNames.Email;
                opciones.ClaimsIdentity.RoleClaimType = ClaimsChronos.Rol;
            })
            .AddRoles<RolAplicacion>()
            .AddEntityFrameworkStores<ChronosDbContext>()
            .AddDefaultTokenProviders();

        servicios.AddOptions<OpcionesJwt>()
            .BindConfiguration(OpcionesJwt.Seccion)
            .ValidateDataAnnotations();

        servicios.AddScoped<IGeneradorTokens, GeneradorTokens>();

        return servicios;
    }

    public static string CadenaDeConexion(IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString(NombreCadenaConexion);

        if (string.IsNullOrWhiteSpace(cadena))
        {
            // DATABASE_URL es la convención que usan Neon, Render, Supabase y Heroku;
            // aceptarla evita tener que reescribir a mano lo que el proveedor ya dio hecho.
            cadena = configuracion["DATABASE_URL"];
        }

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{NombreCadenaConexion}'. " +
                "Defínela en appsettings, como ConnectionStrings__Postgres o como DATABASE_URL.");
        }

        return EsUri(cadena) ? TraducirUri(cadena) : cadena;
    }

    private static bool EsUri(string cadena) =>
        cadena.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || cadena.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Npgsql solo entiende pares clave-valor, mientras que los proveedores gestionados
    /// entregan la conexión como URI. Sin esta traducción hay que reescribirla a mano en
    /// cada despliegue, que es justo donde se cuelan las erratas difíciles de diagnosticar.
    /// </summary>
    private static string TraducirUri(string uri)
    {
        Uri origen;
        try
        {
            origen = new Uri(uri);
        }
        catch (UriFormatException excepcion)
        {
            throw new InvalidOperationException(
                "La cadena de conexión parece una URI pero no se pudo interpretar. " +
                "Se espera el formato postgresql://usuario:contrasena@servidor/base?sslmode=require",
                excepcion);
        }

        var credenciales = origen.UserInfo.Split(':', 2);

        var constructor = new NpgsqlConnectionStringBuilder
        {
            Host = origen.Host,
            Port = origen.IsDefaultPort ? 5432 : origen.Port,
            Database = origen.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credenciales[0]),
            Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : null,
        };

        var parametros = QueryHelpers.ParseQuery(origen.Query);

        if (parametros.TryGetValue("sslmode", out var modoSsl)
            && Enum.TryParse<SslMode>(modoSsl.ToString(), ignoreCase: true, out var valorSsl))
        {
            constructor.SslMode = valorSsl;
        }
        else
        {
            // Todo proveedor gestionado exige TLS y ninguno acepta texto plano.
            constructor.SslMode = SslMode.Require;
        }

        if (parametros.TryGetValue("channel_binding", out var enlace))
        {
            constructor.ChannelBinding = Enum.TryParse<ChannelBinding>(
                enlace.ToString(), ignoreCase: true, out var valorEnlace)
                ? valorEnlace
                : constructor.ChannelBinding;
        }

        return constructor.ConnectionString;
    }
}
