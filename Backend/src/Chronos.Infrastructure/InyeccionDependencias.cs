using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Persistencia;
using Chronos.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

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

    public static string CadenaDeConexion(IConfiguration configuracion) =>
        configuracion.GetConnectionString(NombreCadenaConexion)
        ?? throw new InvalidOperationException(
            $"Falta la cadena de conexión '{NombreCadenaConexion}'. " +
            "Defínela en appsettings o como ConnectionStrings__Postgres.");
}
