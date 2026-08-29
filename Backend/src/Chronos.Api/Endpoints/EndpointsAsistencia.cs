using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

/// <summary>
/// Asistencia del día resuelta con las funciones puras de <see cref="CalculadoraJornada"/>.
/// <para>
/// El cálculo no vive aquí: este archivo se limita a reunir lo que la calculadora necesita
/// —turno, zona horaria y checadas— y a formatear la salida. Así las reglas de retardo y
/// tiempo extra se prueban sin levantar una base de datos, y no hay una segunda versión de
/// ellas escondida en una consulta.
/// </para>
/// </summary>
public static class EndpointsAsistencia
{
    public static IEndpointRouteBuilder MapearAsistencia(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/asistencia")
            .WithTags("Asistencia")
            .RequireAuthorization();

        grupo.MapGet("/dia", AsistenciaDelDia)
            .WithSummary("Asistencia de la plantilla en una fecha, con faltas, retardos y horas.")
            .Produces<AsistenciaDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/mia", MiJornada)
            .WithSummary("Jornadas propias en un rango de fechas.")
            .Produces<IReadOnlyList<AsistenciaDelDiaDto>>();

        return rutas;
    }

    private static async Task<IResult> AsistenciaDelDia(
        [FromQuery] DateOnly? fecha,
        [FromQuery] Guid? sedeId,
        [FromQuery] Guid? departamentoId,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        TimeProvider reloj,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeVerAsistencia(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var dia = fecha ?? DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

        var empleados = await bd.Empleados
            .AsNoTracking()
            .Include(e => e.Departamento)
            .Include(e => e.Sede)
            .Include(e => e.Turno)
            .Where(e => e.Activo)
            .Where(e => sedeId == null || e.SedeId == sedeId)
            .Where(e => departamentoId == null || e.DepartamentoId == departamentoId)
            .OrderBy(e => e.ApellidoPaterno)
            .ThenBy(e => e.Nombres)
            .ToListAsync(ct);

        if (empleados.Count == 0)
        {
            return TypedResults.Ok(new AsistenciaDto(Resumir(dia, []), []));
        }

        // Todas las checadas del día en una consulta. Pedirlas por empleado convertiría una
        // pantalla de plantilla completa en decenas de viajes a la base.
        var identificadores = empleados.Select(e => e.Id).ToList();

        var checadas = await bd.Checadas
            .AsNoTracking()
            .Where(c => c.DiaLaboral == dia && identificadores.Contains(c.EmpleadoId))
            .ToListAsync(ct);

        var porEmpleado = checadas.ToLookup(c => c.EmpleadoId);

        var filas = empleados
            .Select(empleado => Calcular(empleado, dia, [.. porEmpleado[empleado.Id]]))
            .ToList();

        return TypedResults.Ok(new AsistenciaDto(Resumir(dia, filas), filas));
    }

    private static async Task<IResult> MiJornada(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        TimeProvider reloj,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var fin = hasta ?? hoy;
        var inicio = desde ?? fin.AddDays(-13);

        if (inicio > fin)
        {
            return Validacion.ProblemaDeCampo(nameof(desde), "La fecha inicial no puede ser posterior a la final.");
        }

        // Un rango abierto sobre una plantilla grande es una consulta que nadie quiso hacer.
        const int MaximoDias = 92;

        if (fin.DayNumber - inicio.DayNumber > MaximoDias)
        {
            return Validacion.ProblemaDeCampo(
                nameof(hasta),
                $"El rango no puede exceder {MaximoDias} días.");
        }

        var empleado = await bd.Empleados
            .AsNoTracking()
            .Include(e => e.Departamento)
            .Include(e => e.Sede)
            .Include(e => e.Turno)
            .FirstOrDefaultAsync(e => e.Id == contexto.EmpleadoId!.Value, ct);

        if (empleado is null)
        {
            return TypedResults.Problem(
                title: "Expediente no disponible",
                detail: "La cuenta no tiene un expediente asociado.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var checadas = await bd.Checadas
            .AsNoTracking()
            .Where(c => c.EmpleadoId == empleado.Id && c.DiaLaboral >= inicio && c.DiaLaboral <= fin)
            .ToListAsync(ct);

        var porDia = checadas.ToLookup(c => c.DiaLaboral);

        var jornadas = Enumerable
            .Range(0, fin.DayNumber - inicio.DayNumber + 1)
            .Select(desplazamiento => inicio.AddDays(desplazamiento))
            .Select(dia => Calcular(empleado, dia, [.. porDia[dia]]))
            .ToList();

        return TypedResults.Ok(jornadas);
    }

    private static AsistenciaDelDiaDto Calcular(Empleado empleado, DateOnly dia, IReadOnlyList<Checada> checadas)
    {
        var zona = ResolverZona(empleado.Sede?.ZonaHoraria);

        // Sin turno asignado no hay contra qué comparar la hora de llegada. Se informan las
        // horas presentes y el estado queda en blanco en vez de inventar un retardo contra
        // un horario que nadie pactó.
        var resumen = empleado.Turno is { } turno
            ? CalculadoraJornada.Calcular(dia, turno, zona, checadas)
            : SinTurno(dia, checadas);

        return new AsistenciaDelDiaDto(
            empleado.Id,
            empleado.NombreCompleto,
            empleado.NumeroEmpleado,
            empleado.Departamento?.Nombre ?? "—",
            empleado.Sede?.Nombre ?? "—",
            empleado.Turno?.Nombre,
            resumen.Estado,
            NombreDeEstado(resumen.Estado),
            resumen.Entrada,
            resumen.Salida,
            Math.Round(resumen.HorasTrabajadas.TotalHours, 2),
            Math.Round(resumen.HorasExtra.TotalHours, 2),
            resumen.MinutosRetardo,
            resumen.MinutosSalidaAnticipada,
            resumen.RequiereRevision,
            resumen.ConfianzaMinima);
    }

    private static ResumenJornada SinTurno(DateOnly dia, IReadOnlyList<Checada> checadas)
    {
        var validas = checadas.Where(c => c.CuentaParaJornada).OrderBy(c => c.MomentoUtc).ToList();

        var entrada = validas.FirstOrDefault(c => c.Tipo == TipoChecada.Entrada);
        var salida = validas.LastOrDefault(c => c.Tipo == TipoChecada.Salida);

        return new ResumenJornada
        {
            Dia = dia,
            Entrada = entrada?.MomentoUtc,
            Salida = salida?.MomentoUtc,
            HorasTrabajadas = entrada is not null && salida is not null
                ? salida.MomentoUtc - entrada.MomentoUtc
                : TimeSpan.Zero,
            Estado = entrada is null ? EstadoAsistencia.Falta : EstadoAsistencia.Completa,
            RequiereRevision = validas.Any(c => c.Estado == EstadoChecada.RequiereRevision),
            ConfianzaMinima = validas.Count == 0 ? NivelConfianza.Nula : validas.Min(c => c.NivelConfianza)
        };
    }

    /// <summary>
    /// Una zona horaria mal escrita en el catálogo no debe tumbar la pantalla de toda la
    /// plantilla; se cae a UTC y el resto de las filas sigue siendo útil.
    /// </summary>
    private static TimeZoneInfo ResolverZona(string? identificador)
    {
        if (string.IsNullOrWhiteSpace(identificador))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(identificador, out var zona) ? zona : TimeZoneInfo.Utc;
    }

    private static ResumenAsistenciaDto Resumir(DateOnly dia, IReadOnlyList<AsistenciaDelDiaDto> filas)
    {
        // El descanso no cuenta como plantilla esperada: nadie falta el día que libra.
        var esperados = filas.Where(f => f.Estado != EstadoAsistencia.Descanso).ToList();

        return new ResumenAsistenciaDto(
            dia,
            esperados.Count,
            esperados.Count(f => f.Entrada is not null),
            esperados.Count(f => f.Estado == EstadoAsistencia.Falta),
            esperados.Count(f => f.MinutosRetardo > 0),
            esperados.Count(f => f.Estado == EstadoAsistencia.JornadaIncompleta),
            esperados.Count(f => f.RequiereRevision),
            Math.Round(esperados.Sum(f => f.HorasTrabajadas), 2),
            Math.Round(esperados.Sum(f => f.HorasExtra), 2));
    }

    private static string NombreDeEstado(EstadoAsistencia estado) => estado switch
    {
        EstadoAsistencia.Descanso => "Descanso",
        EstadoAsistencia.Completa => "Completa",
        EstadoAsistencia.Retardo => "Retardo",
        EstadoAsistencia.SalidaAnticipada => "Salida anticipada",
        EstadoAsistencia.JornadaIncompleta => "Jornada incompleta",
        EstadoAsistencia.Falta => "Falta",
        _ => estado.ToString()
    };
}
