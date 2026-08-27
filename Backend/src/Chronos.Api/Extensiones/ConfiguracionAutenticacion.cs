using System.Text;
using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chronos.Api.Extensiones;

public static class ConfiguracionAutenticacion
{
    public static IServiceCollection AgregarAutenticacionJwt(this IServiceCollection servicios)
    {
        servicios.AddSingleton<IValidateOptions<OpcionesJwt>, ValidadorOpcionesJwt>();

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Se configura a partir de IOptions<OpcionesJwt> y no leyendo IConfiguration aquí
        // mismo: leerla de forma anticipada dejaría fuera las fuentes que se registran
        // después (variables de entorno de un contenedor, overrides de las pruebas) y la
        // API terminaría validando con una llave distinta a la que usó para firmar.
        servicios
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OpcionesJwt>>((bearer, envoltura) =>
            {
                var jwt = envoltura.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimsChronos.NombreCompleto,
                    RoleClaimType = ClaimsChronos.Rol,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Emisor,
                    ValidAudience = jwt.Audiencia,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Llave)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        servicios.AddAuthorization(auth =>
        {
            auth.AddPolicy("SoloAdmin", p => p.RequireRole(Roles.Admin));
            auth.AddPolicy("AdminOSupervisor", p => p.RequireRole(Roles.Admin, Roles.Supervisor));
        });

        return servicios;
    }
}

internal sealed class ValidadorOpcionesJwt(IHostEnvironment entorno) : IValidateOptions<OpcionesJwt>
{
    /// <summary>Llave incluida en appsettings.Development.json; fuera de desarrollo es un error usarla.</summary>
    private const string LlaveDesarrollo = "llave-solo-para-desarrollo-local-no-usar-en-produccion-2026";

    public ValidateOptionsResult Validate(string? name, OpcionesJwt opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones.Llave) || opciones.Llave.Length < 32)
        {
            return ValidateOptionsResult.Fail(
                "Jwt:Llave debe existir y tener al menos 32 caracteres. " +
                "Defínela como variable de entorno Jwt__Llave.");
        }

        if (!entorno.IsDevelopment() && opciones.Llave == LlaveDesarrollo)
        {
            return ValidateOptionsResult.Fail(
                $"El entorno '{entorno.EnvironmentName}' está usando la llave JWT de desarrollo. " +
                "Sobrescribe Jwt__Llave con un secreto propio antes de desplegar.");
        }

        return ValidateOptionsResult.Success;
    }
}
