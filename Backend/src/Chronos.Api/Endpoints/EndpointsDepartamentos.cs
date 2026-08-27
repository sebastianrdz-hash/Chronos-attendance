using System.Linq.Expressions;
using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

public static class EndpointsDepartamentos
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Departamento, object?>>> Ordenables =
        new Dictionary<string, Expression<Func<Departamento, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["nombre"] = depto => depto.Nombre,
            ["codigo"] = depto => depto.Codigo,
            ["sede"] = depto => depto.Sede!.Nombre,
            ["activo"] = depto => depto.Activo
        };

    public static IEndpointRouteBuilder MapearDepartamentos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/departamentos")
            .WithTags("Departamentos")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/", Listar)
            .WithSummary("Lista departamentos con búsqueda, orden y paginación.");

        grupo.MapGet("/{id:guid}", Obtener)
            .WithSummary("Consulta un departamento por identificador.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/", Crear)
            .WithSummary("Registra un departamento nuevo.")
            .ConValidacion<SolicitudDepartamento>();

        grupo.MapPut("/{id:guid}", Actualizar)
            .WithSummary("Actualiza un departamento. El supervisor solo puede tocar el suyo.")
            .ConValidacion<SolicitudDepartamento>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", Desactivar)
            .WithSummary("Da de baja lógica un departamento.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return rutas;
    }

    private static async Task<IResult> Listar(
        [AsParameters] ParametrosConsulta filtros,
        [FromQuery] Guid? sedeId,
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
        var consulta = bd.Departamentos.AsNoTracking().Include(depto => depto.Sede).AsQueryable();

        if (sedeId is { } sede)
        {
            consulta = consulta.Where(depto => depto.SedeId == sede);
        }

        if (parametros.Activo is { } activo)
        {
            consulta = consulta.Where(depto => depto.Activo == activo);
        }

        if (parametros.Buscar is { } texto)
        {
            var patron = $"%{texto}%";
            consulta = consulta.Where(depto =>
                EF.Functions.ILike(depto.Nombre, patron) ||
                EF.Functions.ILike(depto.Codigo, patron));
        }

        var pagina = await consulta
            .OrdenarPor(parametros.OrdenarPor, parametros.Descendente, Ordenables, depto => depto.Nombre)
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

        var depto = await bd.Departamentos
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(Proyeccion)
            .FirstOrDefaultAsync(ct);

        return depto is null ? NoEncontrado(id) : TypedResults.Ok(depto);
    }

    private static async Task<IResult> Crear(
        SolicitudDepartamento solicitud,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        // Crear departamentos es del admin: un supervisor solo manda dentro del suyo.
        if (PoliticaAcceso.PuedeAdministrarCatalogos(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        if (!await bd.Sedes.AnyAsync(sede => sede.Id == solicitud.SedeId, ct))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudDepartamento.SedeId), "La sede indicada no existe.");
        }

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();

        if (await bd.Departamentos.AnyAsync(d => d.SedeId == solicitud.SedeId && d.Codigo == codigo, ct))
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudDepartamento.Codigo),
                "Ya existe un departamento con ese código en la sede seleccionada.");
        }

        var nuevo = new Departamento
        {
            Nombre = solicitud.Nombre.Trim(),
            Codigo = codigo,
            SedeId = solicitud.SedeId,
            Activo = solicitud.Activo
        };

        bd.Departamentos.Add(nuevo);
        await bd.SaveChangesAsync(ct);

        var creado = await bd.Departamentos
            .AsNoTracking()
            .Where(d => d.Id == nuevo.Id)
            .Select(Proyeccion)
            .FirstAsync(ct);

        return TypedResults.Created($"/api/v1/departamentos/{nuevo.Id}", creado);
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        SolicitudDepartamento solicitud,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeEditarDepartamento(contexto, id).Rechazo() is { } alto)
        {
            return alto;
        }

        var depto = await bd.Departamentos.FirstOrDefaultAsync(d => d.Id == id, ct);

        if (depto is null)
        {
            return NoEncontrado(id);
        }

        // Mover un departamento de sede reacomoda a toda su plantilla: solo el admin.
        if (depto.SedeId != solicitud.SedeId && !contexto.EsAdmin)
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudDepartamento.SedeId),
                "Solo un administrador puede cambiar un departamento de sede.");
        }

        if (!await bd.Sedes.AnyAsync(sede => sede.Id == solicitud.SedeId, ct))
        {
            return Validacion.ProblemaDeCampo(nameof(SolicitudDepartamento.SedeId), "La sede indicada no existe.");
        }

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();

        if (await bd.Departamentos.AnyAsync(otro => otro.SedeId == solicitud.SedeId && otro.Codigo == codigo && otro.Id != id, ct))
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudDepartamento.Codigo),
                "Ya existe otro departamento con ese código en la sede seleccionada.");
        }

        depto.Nombre = solicitud.Nombre.Trim();
        depto.Codigo = codigo;
        depto.SedeId = solicitud.SedeId;
        depto.Activo = solicitud.Activo;

        await bd.SaveChangesAsync(ct);

        var actualizado = await bd.Departamentos
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(Proyeccion)
            .FirstAsync(ct);

        return TypedResults.Ok(actualizado);
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

        var depto = await bd.Departamentos.FirstOrDefaultAsync(d => d.Id == id, ct);

        if (depto is null)
        {
            return NoEncontrado(id);
        }

        if (await bd.Empleados.AnyAsync(empleado => empleado.DepartamentoId == id && empleado.Activo, ct))
        {
            return Validacion.ProblemaDeCampo(
                "departamento",
                "No se puede dar de baja un departamento con empleados activos. Reasígnalos primero.");
        }

        depto.Activo = false;
        await bd.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static IResult NoEncontrado(Guid id) => TypedResults.Problem(
        detail: $"No existe un departamento con el identificador {id}.",
        statusCode: StatusCodes.Status404NotFound,
        title: "Departamento no encontrado");

    private static readonly Expression<Func<Departamento, DepartamentoDto>> Proyeccion = depto => new DepartamentoDto(
        depto.Id,
        depto.Nombre,
        depto.Codigo,
        depto.SedeId,
        depto.Sede != null ? depto.Sede.Nombre : string.Empty,
        depto.Activo,
        depto.Empleados.Count);
}
