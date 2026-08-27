using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

/// <summary>
/// Lo que cualquier usuario puede hacer sobre sí mismo, sin importar su rol. Estos
/// endpoints no consultan PoliticaAcceso porque el identificador nunca llega por la URL:
/// se toma del token, así que no hay forma de apuntar al expediente de otra persona.
/// </summary>
public static class EndpointsPerfil
{
    public static IEndpointRouteBuilder MapearPerfil(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/perfil")
            .WithTags("Perfil")
            .RequireAuthorization();

        grupo.MapGet("/", Obtener)
            .WithSummary("Devuelve el expediente y el turno del usuario autenticado.")
            .Produces<MiPerfilDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/contrasena", CambiarContrasena)
            .WithSummary("Cambia la contraseña propia y levanta la marca de cambio obligatorio.")
            .ConValidacion<SolicitudCambioContrasena>();

        return rutas;
    }

    private static async Task<IResult> Obtener(
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);
        var cuenta = await gestorUsuarios.GetUserAsync(principal);

        if (cuenta is null)
        {
            return TypedResults.Unauthorized();
        }

        if (contexto.EmpleadoId is not { } empleadoId)
        {
            return TypedResults.Problem(
                title: "Sin expediente",
                detail: "Esta cuenta no tiene un expediente de empleado asociado.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var empleado = await EndpointsEmpleados.LeerUno(bd, empleadoId, ct);

        if (empleado is null)
        {
            return TypedResults.Problem(
                title: "Sin expediente",
                detail: "El expediente asociado a esta cuenta ya no existe.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var turno = empleado.TurnoId is not { } turnoId
            ? null
            : await bd.Turnos
                .AsNoTracking()
                .Where(t => t.Id == turnoId)
                .Select(t => new TurnoDto(
                    t.Id,
                    t.Nombre,
                    t.HoraEntrada,
                    t.HoraSalida,
                    t.ToleranciaMinutos,
                    t.MinutosDescanso,
                    DiasLaboralesMapeo.ANombres(t.DiasLaborales),
                    t.HoraSalida <= t.HoraEntrada,
                    0,
                    t.Activo,
                    0))
                .FirstOrDefaultAsync(ct);

        // Las horas programadas dependen de una propiedad calculada que EF no traduce,
        // así que se completan una vez que el turno ya está materializado.
        if (turno is not null)
        {
            var jornada = turno.HoraSalida.ToTimeSpan() - turno.HoraEntrada.ToTimeSpan();

            if (turno.CruzaMedianoche)
            {
                jornada += TimeSpan.FromDays(1);
            }

            turno = turno with
            {
                HorasProgramadas = Math.Round((jornada - TimeSpan.FromMinutes(turno.MinutosDescanso)).TotalHours, 2)
            };
        }

        return TypedResults.Ok(new MiPerfilDto(empleado, turno, cuenta.Email ?? string.Empty, cuenta.UltimoAccesoUtc));
    }

    private static async Task<IResult> CambiarContrasena(
        SolicitudCambioContrasena solicitud,
        ClaimsPrincipal principal,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        ILoggerFactory registroFactory)
    {
        var cuenta = await gestorUsuarios.GetUserAsync(principal);

        if (cuenta is null || !cuenta.Activo)
        {
            return TypedResults.Unauthorized();
        }

        if (solicitud.ContrasenaActual == solicitud.ContrasenaNueva)
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudCambioContrasena.ContrasenaNueva),
                "La contraseña nueva debe ser distinta de la actual.");
        }

        var resultado = await gestorUsuarios.ChangePasswordAsync(
            cuenta, solicitud.ContrasenaActual, solicitud.ContrasenaNueva);

        if (!resultado.Succeeded)
        {
            var campo = resultado.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? nameof(SolicitudCambioContrasena.ContrasenaActual)
                : nameof(SolicitudCambioContrasena.ContrasenaNueva);

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [campo] = resultado.Errors.Select(error => error.Description).ToArray()
            });
        }

        cuenta.DebeCambiarContrasena = false;
        await gestorUsuarios.UpdateAsync(cuenta);

        registroFactory.CreateLogger("Chronos.Perfil")
            .LogInformation("El usuario {UsuarioId} cambió su contraseña.", cuenta.Id);

        return TypedResults.NoContent();
    }
}
