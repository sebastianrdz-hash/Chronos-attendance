using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Auditoria;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chronos.Api.Endpoints;

/// <summary>
/// Bandeja de Recursos Humanos para las checadas que no reunieron evidencia suficiente.
/// <para>
/// El sistema nunca decide solo sobre un caso dudoso: lo acepta, lo deja anotado y lo pone
/// aquí. Quien dictamina tiene que escribir por qué, y eso queda en la bitácora junto a su
/// nombre. Es la pieza que convierte el puntaje de confianza en algo accionable en vez de
/// un número decorativo.
/// </para>
/// </summary>
public static class EndpointsRevision
{
    public static IEndpointRouteBuilder MapearRevision(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/revision")
            .WithTags("Revisión")
            .RequireAuthorization();

        grupo.MapGet("/pendientes", ListarPendientes)
            .WithSummary("Checadas que esperan dictamen, acotadas al alcance de quien pregunta.")
            .Produces<ResultadoPaginado<ChecadaPorRevisarDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapPost("/{id:guid}/aprobar", (Guid id, SolicitudDictamen solicitud, [AsParameters] ContextoDictamen ctx) =>
                Dictaminar(id, solicitud, ctx, aprobar: true))
            .WithSummary("Da por buena una checada dudosa dejando constancia de quién y por qué.")
            .Produces<ChecadaPorRevisarDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ConValidacion<SolicitudDictamen>();

        grupo.MapPost("/{id:guid}/rechazar", (Guid id, SolicitudDictamen solicitud, [AsParameters] ContextoDictamen ctx) =>
                Dictaminar(id, solicitud, ctx, aprobar: false))
            .WithSummary("Descarta una checada dudosa; deja de contar para la jornada.")
            .Produces<ChecadaPorRevisarDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ConValidacion<SolicitudDictamen>();

        grupo.MapGet("/bitacora", ListarBitacora)
            .WithSummary("Asientos de auditoría, del más reciente al más antiguo.")
            .Produces<ResultadoPaginado<AsientoBitacoraDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return rutas;
    }

    private static async Task<IResult> ListarPendientes(
        [AsParameters] ParametrosConsulta parametros,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeVerAsistencia(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var consulta = parametros.Normalizar();

        var origen = bd.Checadas
            .AsNoTracking()
            .Include(c => c.Senales)
            .Include(c => c.Sede)
            .Include(c => c.Empleado!).ThenInclude(e => e.Departamento)
            .Where(c => c.Estado == EstadoChecada.RequiereRevision);

        // Un supervisor solo ve lo que puede dictaminar. Mostrarle una bandeja llena de
        // casos que al pulsar devolverían 403 sería una interfaz que miente.
        if (!contexto.EsAdmin)
        {
            origen = origen.Where(c => c.Empleado!.DepartamentoId == contexto.DepartamentoId);
        }

        // Nadie dictamina la propia, así que tampoco tiene sentido ofrecérsela.
        origen = origen.Where(c => c.EmpleadoId != contexto.EmpleadoId);

        if (consulta.Buscar is { } texto)
        {
            var patron = $"%{texto}%";
            origen = origen.Where(c =>
                EF.Functions.ILike(c.Empleado!.NumeroEmpleado, patron) ||
                EF.Functions.ILike(c.Empleado!.Nombres, patron) ||
                EF.Functions.ILike(c.Empleado!.ApellidoPaterno, patron));
        }

        var total = await origen.CountAsync(ct);

        // Las más antiguas primero: son las que llevan más tiempo sin resolver.
        var pagina = await origen
            .OrderBy(c => c.MomentoUtc)
            .Skip(consulta.Salto)
            .Take(consulta.Tamano)
            .ToListAsync(ct);

        return TypedResults.Ok(new ResultadoPaginado<ChecadaPorRevisarDto>(
            [.. pagina.Select(Mapear)],
            consulta.Pagina,
            consulta.Tamano,
            total));
    }

    private static async Task<IResult> Dictaminar(
        Guid id,
        SolicitudDictamen solicitud,
        ContextoDictamen ctx,
        bool aprobar)
    {
        var contexto = await ctx.Acceso.ResolverAsync(ctx.Principal, ctx.Ct);

        var checada = await ctx.Bd.Checadas
            .Include(c => c.Senales)
            .Include(c => c.Sede)
            .Include(c => c.Empleado!).ThenInclude(e => e.Departamento)
            .FirstOrDefaultAsync(c => c.Id == id, ctx.Ct);

        if (checada?.Empleado is null)
        {
            return TypedResults.Problem(
                title: "Checada no encontrada",
                detail: "No existe una checada con ese identificador.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var permiso = PoliticaAcceso.PuedeRevisarChecada(
            contexto,
            checada.EmpleadoId,
            checada.Empleado.DepartamentoId);

        if (permiso.Rechazo() is { } alto)
        {
            return alto;
        }

        if (!checada.EsperaRevision)
        {
            return TypedResults.Problem(
                title: "Ya está dictaminada",
                detail: $"Esta checada ya se resolvió como «{checada.Estado}». Consulta la bitácora para ver quién lo decidió.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var motivo = solicitud.Motivo.Trim();
        var usuarioId = LeerUsuarioId(ctx.Principal);
        var estadoPrevio = checada.Estado;

        if (aprobar)
        {
            checada.AjustarPorSupervisor(usuarioId ?? Guid.Empty, motivo);
        }
        else
        {
            checada.RechazarPorSupervisor(usuarioId ?? Guid.Empty, motivo);
        }

        ctx.Bitacora.Registrar(
            aprobar ? AccionAuditada.ChecadaAprobada : AccionAuditada.ChecadaRechazada,
            nameof(Checada),
            checada.Id,
            usuarioId,
            ctx.Principal.FindFirstValue(JwtRegisteredClaimNames.Email),
            motivo,
            new
            {
                empleadoId = checada.EmpleadoId,
                diaLaboral = checada.DiaLaboral,
                estadoPrevio = estadoPrevio.ToString(),
                estadoNuevo = checada.Estado.ToString(),
                puntajeConfianza = checada.PuntajeConfianza
            },
            ctx.Http.Connection.RemoteIpAddress?.ToString());

        // El asiento y el cambio de estado se confirman juntos. Separarlos abriría la
        // puerta a una checada dictaminada sin rastro, o a un rastro de algo que no pasó.
        await ctx.Bd.SaveChangesAsync(ctx.Ct);

        return TypedResults.Ok(Mapear(checada));
    }

    private static async Task<IResult> ListarBitacora(
        [AsParameters] ParametrosConsulta parametros,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeVerAsistencia(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var consulta = parametros.Normalizar();

        var origen = bd.Bitacora.AsNoTracking();

        if (consulta.Buscar is { } texto)
        {
            var patron = $"%{texto}%";
            origen = origen.Where(a =>
                (a.UsuarioCorreo != null && EF.Functions.ILike(a.UsuarioCorreo, patron)) ||
                (a.Motivo != null && EF.Functions.ILike(a.Motivo, patron)) ||
                EF.Functions.ILike(a.Entidad, patron));
        }

        var total = await origen.CountAsync(ct);

        var pagina = await origen
            .OrderByDescending(a => a.OcurridoUtc)
            .Skip(consulta.Salto)
            .Take(consulta.Tamano)
            .ToListAsync(ct);

        return TypedResults.Ok(new ResultadoPaginado<AsientoBitacoraDto>(
            [.. pagina.Select(a => new AsientoBitacoraDto(
                a.Id,
                a.OcurridoUtc,
                a.Accion,
                NombreDeAccion(a.Accion),
                a.Entidad,
                a.EntidadId,
                a.UsuarioCorreo,
                a.Motivo))],
            consulta.Pagina,
            consulta.Tamano,
            total));
    }

    private static Guid? LeerUsuarioId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    private static string NombreDeAccion(AccionAuditada accion) => accion switch
    {
        AccionAuditada.ChecadaAprobada => "Checada aprobada",
        AccionAuditada.ChecadaRechazada => "Checada rechazada",
        AccionAuditada.CredencialRevocada => "Credencial revocada",
        AccionAuditada.EmpleadoDadoDeAlta => "Empleado dado de alta",
        AccionAuditada.EmpleadoDadoDeBaja => "Empleado dado de baja",
        AccionAuditada.AccesoReiniciado => "Acceso reiniciado",
        _ => accion.ToString()
    };

    private static ChecadaPorRevisarDto Mapear(Checada checada) => new(
        checada.Id,
        checada.EmpleadoId,
        checada.Empleado?.NombreCompleto ?? "—",
        checada.Empleado?.NumeroEmpleado ?? "—",
        checada.Empleado?.Departamento?.Nombre ?? "—",
        checada.Tipo,
        checada.MomentoUtc,
        checada.DiaLaboral,
        checada.Estado,
        checada.PuntajeConfianza,
        checada.NivelConfianza,
        checada.Sede?.Nombre,
        [.. checada.Senales
            .OrderBy(s => s.CapturadaUtc)
            .Select(s => new SenalDto(
                s.Tipo,
                EndpointsFichaje.NombreDeSenal(s.Tipo),
                s.Resultado,
                s.PesoAplicado,
                s.CapturadaUtc,
                s.DetalleJson))]);
}

/// <summary>
/// Las dependencias del dictamen agrupadas. Aprobar y rechazar comparten todo salvo una
/// bandera, y repetir ocho parámetros en cada firma solo servía para que se desincronizaran.
/// </summary>
public sealed record ContextoDictamen(
    ClaimsPrincipal Principal,
    IResolutorAcceso Acceso,
    ChronosDbContext Bd,
    IBitacora Bitacora,
    HttpContext Http,
    CancellationToken Ct);
