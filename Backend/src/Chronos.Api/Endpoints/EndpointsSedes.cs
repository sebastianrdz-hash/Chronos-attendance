using System.Linq.Expressions;
using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Seguridad;
using Chronos.Domain.ValueObjects;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

public static class EndpointsSedes
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Sede, object?>>> Ordenables =
        new Dictionary<string, Expression<Func<Sede, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["nombre"] = sede => sede.Nombre,
            ["codigo"] = sede => sede.Codigo,
            ["zonaHoraria"] = sede => sede.ZonaHoraria,
            ["activa"] = sede => sede.Activa
        };

    public static IEndpointRouteBuilder MapearSedes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/sedes")
            .WithTags("Sedes")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/", Listar)
            .WithSummary("Lista sedes con búsqueda, orden y paginación.");

        grupo.MapGet("/{id:guid}", Obtener)
            .WithSummary("Consulta una sede por identificador.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/", Crear)
            .WithSummary("Registra una sede nueva.")
            .ConValidacion<SolicitudSede>();

        grupo.MapPut("/{id:guid}", Actualizar)
            .WithSummary("Actualiza los datos de una sede.")
            .ConValidacion<SolicitudSede>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", Desactivar)
            .WithSummary("Da de baja lógica una sede.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return rutas;
    }

    private static async Task<IResult> Listar(
        [AsParameters] ParametrosConsulta filtros,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeConsultarCatalogos(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var parametros = filtros.Normalizar();
        var consulta = bd.Sedes.AsNoTracking();

        if (parametros.Activo is { } activa)
        {
            consulta = consulta.Where(sede => sede.Activa == activa);
        }

        if (parametros.Buscar is { } texto)
        {
            var patron = $"%{texto}%";
            consulta = consulta.Where(sede =>
                EF.Functions.ILike(sede.Nombre, patron) ||
                EF.Functions.ILike(sede.Codigo, patron) ||
                (sede.Direccion != null && EF.Functions.ILike(sede.Direccion, patron)));
        }

        var pagina = await consulta
            .OrdenarPor(parametros.OrdenarPor, parametros.Descendente, Ordenables, sede => sede.Nombre)
            .PaginarAsync(parametros, Proyeccion, ct);

        return TypedResults.Ok(pagina);
    }

    private static async Task<IResult> Obtener(
        Guid id,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeConsultarCatalogos(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var sede = await bd.Sedes.AsNoTracking().Where(s => s.Id == id).Select(Proyeccion).FirstOrDefaultAsync(ct);

        return sede is null ? NoEncontrada(id) : TypedResults.Ok(sede);
    }

    private static async Task<IResult> Crear(
        SolicitudSede solicitud,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeAdministrarCatalogos(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        if (RevisarReglas(solicitud) is { } problema)
        {
            return problema;
        }

        if (await bd.Sedes.AnyAsync(sede => sede.Codigo == solicitud.Codigo, ct))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudSede.Codigo), "Ya existe una sede con ese código.");
        }

        var nueva = new Sede
        {
            Nombre = solicitud.Nombre.Trim(),
            Codigo = solicitud.Codigo.Trim().ToUpperInvariant(),
            Direccion = solicitud.Direccion?.Trim(),
            ZonaHoraria = solicitud.ZonaHoraria,
            Activa = solicitud.Activa,
            Geocerca = ArmarGeocerca(solicitud)
        };

        bd.Sedes.Add(nueva);
        await bd.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/v1/sedes/{nueva.Id}", Mapear(nueva, 0, 0));
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        SolicitudSede solicitud,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeAdministrarCatalogos(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        if (RevisarReglas(solicitud) is { } problema)
        {
            return problema;
        }

        var sede = await bd.Sedes.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (sede is null)
        {
            return NoEncontrada(id);
        }

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();

        if (await bd.Sedes.AnyAsync(otra => otra.Codigo == codigo && otra.Id != id, ct))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudSede.Codigo), "Ya existe otra sede con ese código.");
        }

        sede.Nombre = solicitud.Nombre.Trim();
        sede.Codigo = codigo;
        sede.Direccion = solicitud.Direccion?.Trim();
        sede.ZonaHoraria = solicitud.ZonaHoraria;
        sede.Activa = solicitud.Activa;
        sede.Geocerca = ArmarGeocerca(solicitud);

        await bd.SaveChangesAsync(ct);

        var totales = await Totales(bd, id, ct);
        return TypedResults.Ok(Mapear(sede, totales.Departamentos, totales.Empleados));
    }

    private static async Task<IResult> Desactivar(
        Guid id,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeAdministrarCatalogos(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var sede = await bd.Sedes.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (sede is null)
        {
            return NoEncontrada(id);
        }

        // Baja lógica: hay checadas históricas colgando de la sede y borrarla las falsearía.
        sede.Activa = false;
        await bd.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static IResult? RevisarReglas(SolicitudSede solicitud)
    {
        if (!solicitud.GeocercaCompleta && !solicitud.GeocercaVacia)
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudSede.RadioMetros),
                "La geocerca necesita latitud, longitud y radio, o los tres vacíos.");
        }

        if (!EsZonaHorariaConocida(solicitud.ZonaHoraria))
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudSede.ZonaHoraria),
                $"'{solicitud.ZonaHoraria}' no es una zona horaria IANA reconocida por este servidor.");
        }

        return null;
    }

    private static bool EsZonaHorariaConocida(string zona)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(zona);
            return true;
        }
        catch (Exception excepcion) when (excepcion is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static Geocerca? ArmarGeocerca(SolicitudSede solicitud) =>
        solicitud.GeocercaCompleta
            ? new Geocerca
            {
                Latitud = solicitud.Latitud!.Value,
                Longitud = solicitud.Longitud!.Value,
                RadioMetros = solicitud.RadioMetros!.Value
            }
            : null;

    private static async Task<(int Departamentos, int Empleados)> Totales(ChronosDbContext bd, Guid id, CancellationToken ct) =>
    (
        await bd.Departamentos.CountAsync(d => d.SedeId == id, ct),
        await bd.Empleados.CountAsync(e => e.SedeId == id, ct)
    );

    private static IResult NoEncontrada(Guid id) => TypedResults.Problem(
        detail: $"No existe una sede con el identificador {id}.",
        statusCode: StatusCodes.Status404NotFound,
        title: "Sede no encontrada");

    private static readonly Expression<Func<Sede, SedeDto>> Proyeccion = sede => new SedeDto(
        sede.Id,
        sede.Nombre,
        sede.Codigo,
        sede.Direccion,
        sede.ZonaHoraria,
        sede.Geocerca != null ? sede.Geocerca.Latitud : null,
        sede.Geocerca != null ? sede.Geocerca.Longitud : null,
        sede.Geocerca != null ? sede.Geocerca.RadioMetros : null,
        sede.Activa,
        sede.Departamentos.Count,
        sede.Empleados.Count);

    private static SedeDto Mapear(Sede sede, int departamentos, int empleados) => new(
        sede.Id,
        sede.Nombre,
        sede.Codigo,
        sede.Direccion,
        sede.ZonaHoraria,
        sede.Geocerca?.Latitud,
        sede.Geocerca?.Longitud,
        sede.Geocerca?.RadioMetros,
        sede.Activa,
        departamentos,
        empleados);
}
