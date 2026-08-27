using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Domain.Entidades;
using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Persistencia;
using Chronos.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

public static class EndpointsAutenticacion
{
    public static IEndpointRouteBuilder MapearAutenticacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/v1/auth").WithTags("Autenticación");

        grupo.MapPost("/login", IniciarSesionAsync)
            .AllowAnonymous()
            .WithName("IniciarSesion")
            .WithSummary("Emite un token JWT a partir de credenciales corporativas.")
            .Produces<RespuestaLogin>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        grupo.MapGet("/yo", ObtenerPerfilAsync)
            .RequireAuthorization()
            .WithName("ObtenerPerfil")
            .WithSummary("Devuelve el perfil asociado al token vigente.")
            .Produces<PerfilUsuario>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return rutas;
    }

    private static async Task<IResult> IniciarSesionAsync(
        [FromBody] SolicitudLogin solicitud,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        IGeneradorTokens generador,
        ChronosDbContext contexto,
        ILoggerFactory registroFactory,
        CancellationToken ct)
    {
        var registro = registroFactory.CreateLogger("Chronos.Autenticacion");

        if (string.IsNullOrWhiteSpace(solicitud.Correo) || string.IsNullOrWhiteSpace(solicitud.Contrasena))
        {
            return TypedResults.Problem(
                title: "Datos incompletos",
                detail: "Se requieren correo y contraseña.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var usuario = await gestorUsuarios.FindByEmailAsync(solicitud.Correo);

        // Respuesta uniforme: distinguir "no existe" de "contraseña incorrecta"
        // permitiría enumerar cuentas válidas.
        if (usuario is null || !usuario.Activo)
        {
            registro.LogWarning("Intento de acceso fallido para {Correo}", solicitud.Correo);
            return CredencialesInvalidas();
        }

        if (await gestorUsuarios.IsLockedOutAsync(usuario))
        {
            return TypedResults.Problem(
                title: "Cuenta bloqueada",
                detail: "Demasiados intentos fallidos. Intenta de nuevo más tarde.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (!await gestorUsuarios.CheckPasswordAsync(usuario, solicitud.Contrasena))
        {
            await gestorUsuarios.AccessFailedAsync(usuario);
            registro.LogWarning("Contraseña incorrecta para {UsuarioId}", usuario.Id);
            return CredencialesInvalidas();
        }

        await gestorUsuarios.ResetAccessFailedCountAsync(usuario);

        var roles = await gestorUsuarios.GetRolesAsync(usuario);
        var empleado = await CargarEmpleadoAsync(contexto, usuario.Id, ct);

        var token = generador.Emitir(usuario, roles, empleado);

        usuario.UltimoAccesoUtc = DateTimeOffset.UtcNow;
        await gestorUsuarios.UpdateAsync(usuario);

        registro.LogInformation("Sesión iniciada por {UsuarioId} con roles {Roles}", usuario.Id, roles);

        return TypedResults.Ok(new RespuestaLogin(
            token.AccessToken,
            JwtBearerDefaults.AuthenticationScheme,
            token.ExpiraEnSegundos,
            token.ExpiraUtc,
            ConstruirPerfil(usuario, roles, empleado)));
    }

    private static async Task<IResult> ObtenerPerfilAsync(
        ClaimsPrincipal principal,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        ChronosDbContext contexto,
        CancellationToken ct)
    {
        var usuario = await gestorUsuarios.GetUserAsync(principal);

        if (usuario is null || !usuario.Activo)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await gestorUsuarios.GetRolesAsync(usuario);
        var empleado = await CargarEmpleadoAsync(contexto, usuario.Id, ct);

        return TypedResults.Ok(ConstruirPerfil(usuario, roles, empleado));
    }

    private static Task<Empleado?> CargarEmpleadoAsync(
        ChronosDbContext contexto,
        Guid usuarioId,
        CancellationToken ct) =>
        contexto.Empleados
            .AsNoTracking()
            .Include(e => e.Departamento)
            .Include(e => e.Sede)
            .FirstOrDefaultAsync(e => e.UsuarioId == usuarioId, ct);

    internal static PerfilUsuario ConstruirPerfil(
        UsuarioAplicacion usuario,
        IEnumerable<string> roles,
        Empleado? empleado)
    {
        var lista = roles.ToArray();

        return new PerfilUsuario(
            usuario.Id,
            usuario.Email ?? string.Empty,
            empleado?.NombreCompleto ?? usuario.NombreParaMostrar ?? usuario.UserName ?? string.Empty,
            lista,
            Roles.MayorPrivilegio(lista),
            empleado?.Id,
            empleado?.NumeroEmpleado,
            empleado?.Puesto,
            empleado?.DepartamentoId,
            empleado?.Departamento?.Nombre,
            empleado?.SedeId,
            empleado?.Sede?.Nombre,
            usuario.DebeCambiarContrasena);
    }

    private static IResult CredencialesInvalidas() =>
        TypedResults.Problem(
            title: "Credenciales inválidas",
            detail: "El correo o la contraseña no coinciden.",
            statusCode: StatusCodes.Status401Unauthorized);
}
