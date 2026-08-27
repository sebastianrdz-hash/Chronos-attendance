using System.Linq.Expressions;
using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

public static class EndpointsTurnos
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Turno, object?>>> Ordenables =
        new Dictionary<string, Expression<Func<Turno, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["nombre"] = turno => turno.Nombre,
            ["horaEntrada"] = turno => turno.HoraEntrada,
            ["horaSalida"] = turno => turno.HoraSalida,
            ["activo"] = turno => turno.Activo
        };

    public static IEndpointRouteBuilder MapearTurnos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/turnos")
            .WithTags("Turnos")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/", Listar)
            .WithSummary("Lista turnos con búsqueda, orden y paginación.");

        grupo.MapGet("/{id:guid}", Obtener)
            .WithSummary("Consulta un turno por identificador.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/", Crear)
            .WithSummary("Registra un turno nuevo.")
            .ConValidacion<SolicitudTurno>();

        grupo.MapPut("/{id:guid}", Actualizar)
            .WithSummary("Actualiza un turno.")
            .ConValidacion<SolicitudTurno>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", Desactivar)
            .WithSummary("Da de baja lógica un turno.")
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
        var consulta = bd.Turnos.AsNoTracking();

        if (parametros.Activo is { } activo)
        {
            consulta = consulta.Where(turno => turno.Activo == activo);
        }

        if (parametros.Buscar is { } texto)
        {
            consulta = consulta.Where(turno => EF.Functions.ILike(turno.Nombre, $"%{texto}%"));
        }

        var ordenada = consulta.OrdenarPor(
            parametros.OrdenarPor, parametros.Descendente, Ordenables, turno => turno.HoraEntrada);

        var total = await ordenada.CountAsync(ct);

        // CruzaMedianoche y DuracionProgramada son propiedades calculadas que EF ignora,
        // así que la proyección se hace en memoria sobre la página ya recortada.
        var elementos = await ordenada
            .Skip(parametros.Salto)
            .Take(parametros.Tamano)
            .Select(turno => new { Turno = turno, Empleados = turno.Empleados.Count })
            .ToListAsync(ct);

        var pagina = new ResultadoPaginado<TurnoDto>(
            elementos.Select(fila => Mapear(fila.Turno, fila.Empleados)).ToList(),
            parametros.Pagina,
            parametros.Tamano,
            total);

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

        var fila = await bd.Turnos
            .AsNoTracking()
            .Where(turno => turno.Id == id)
            .Select(turno => new { Turno = turno, Empleados = turno.Empleados.Count })
            .FirstOrDefaultAsync(ct);

        return fila is null ? NoEncontrado(id) : TypedResults.Ok(Mapear(fila.Turno, fila.Empleados));
    }

    private static async Task<IResult> Crear(
        SolicitudTurno solicitud,
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

        if (RevisarReglas(solicitud, out var dias) is { } problema)
        {
            return problema;
        }

        var nombre = solicitud.Nombre.Trim();

        if (await bd.Turnos.AnyAsync(turno => turno.Nombre == nombre, ct))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudTurno.Nombre), "Ya existe un turno con ese nombre.");
        }

        var nuevo = new Turno
        {
            Nombre = nombre,
            HoraEntrada = solicitud.HoraEntrada,
            HoraSalida = solicitud.HoraSalida,
            ToleranciaMinutos = solicitud.ToleranciaMinutos,
            MinutosDescanso = solicitud.MinutosDescanso,
            DiasLaborales = dias,
            Activo = solicitud.Activo
        };

        bd.Turnos.Add(nuevo);
        await bd.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/v1/turnos/{nuevo.Id}", Mapear(nuevo, 0));
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        SolicitudTurno solicitud,
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

        if (RevisarReglas(solicitud, out var dias) is { } problema)
        {
            return problema;
        }

        var turno = await bd.Turnos.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (turno is null)
        {
            return NoEncontrado(id);
        }

        var nombre = solicitud.Nombre.Trim();

        if (await bd.Turnos.AnyAsync(otro => otro.Nombre == nombre && otro.Id != id, ct))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudTurno.Nombre), "Ya existe otro turno con ese nombre.");
        }

        turno.Nombre = nombre;
        turno.HoraEntrada = solicitud.HoraEntrada;
        turno.HoraSalida = solicitud.HoraSalida;
        turno.ToleranciaMinutos = solicitud.ToleranciaMinutos;
        turno.MinutosDescanso = solicitud.MinutosDescanso;
        turno.DiasLaborales = dias;
        turno.Activo = solicitud.Activo;

        await bd.SaveChangesAsync(ct);

        var empleados = await bd.Empleados.CountAsync(empleado => empleado.TurnoId == id, ct);
        return TypedResults.Ok(Mapear(turno, empleados));
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

        var turno = await bd.Turnos.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (turno is null)
        {
            return NoEncontrado(id);
        }

        if (await bd.Empleados.AnyAsync(empleado => empleado.TurnoId == id && empleado.Activo, ct))
        {
            return Validacion.ProblemaDeCampo(
                "turno",
                "No se puede dar de baja un turno con empleados activos asignados.");
        }

        turno.Activo = false;
        await bd.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static IResult? RevisarReglas(SolicitudTurno solicitud, out Chronos.Domain.Enums.DiasSemana dias)
    {
        if (!DiasLaboralesMapeo.Intentar(solicitud.DiasLaborales, out dias, out var error))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudTurno.DiasLaborales), error!);
        }

        if (solicitud.HoraEntrada == solicitud.HoraSalida)
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudTurno.HoraSalida),
                "La hora de salida no puede ser igual a la de entrada.");
        }

        var candidato = new Turno
        {
            Nombre = solicitud.Nombre,
            HoraEntrada = solicitud.HoraEntrada,
            HoraSalida = solicitud.HoraSalida,
            MinutosDescanso = solicitud.MinutosDescanso
        };

        if (candidato.DuracionProgramada <= TimeSpan.Zero)
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudTurno.MinutosDescanso),
                "El descanso deja la jornada efectiva en cero o menos. Reduce el descanso o alarga el turno.");
        }

        return null;
    }

    private static IResult NoEncontrado(Guid id) => TypedResults.Problem(
        detail: $"No existe un turno con el identificador {id}.",
        statusCode: StatusCodes.Status404NotFound,
        title: "Turno no encontrado");

    private static TurnoDto Mapear(Turno turno, int empleados) => new(
        turno.Id,
        turno.Nombre,
        turno.HoraEntrada,
        turno.HoraSalida,
        turno.ToleranciaMinutos,
        turno.MinutosDescanso,
        DiasLaboralesMapeo.ANombres(turno.DiasLaborales),
        turno.CruzaMedianoche,
        Math.Round(turno.DuracionProgramada.TotalHours, 2),
        turno.Activo,
        empleados);
}
