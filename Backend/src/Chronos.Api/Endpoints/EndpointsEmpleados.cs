using System.Linq.Expressions;
using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Persistencia;
using Chronos.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

public static class EndpointsEmpleados
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Empleado, object?>>> Ordenables =
        new Dictionary<string, Expression<Func<Empleado, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["numero"] = empleado => empleado.NumeroEmpleado,
            ["nombre"] = empleado => empleado.ApellidoPaterno,
            ["correo"] = empleado => empleado.CorreoCorporativo,
            ["puesto"] = empleado => empleado.Puesto,
            ["departamento"] = empleado => empleado.Departamento!.Nombre,
            ["sede"] = empleado => empleado.Sede!.Nombre,
            ["fechaIngreso"] = empleado => empleado.FechaIngreso,
            ["activo"] = empleado => empleado.Activo
        };

    public static IEndpointRouteBuilder MapearEmpleados(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/empleados")
            .WithTags("Empleados")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/", Listar)
            .WithSummary("Lista empleados con búsqueda, filtros, orden y paginación.");

        grupo.MapGet("/{id:guid}", Obtener)
            .WithSummary("Consulta un empleado. Un empleado raso solo puede pedir el suyo.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/", Crear)
            .WithSummary("Da de alta un empleado junto con su cuenta de acceso.")
            .ConValidacion<SolicitudCrearEmpleado>()
            .Produces<RespuestaAltaEmpleado>(StatusCodes.Status201Created);

        grupo.MapPut("/{id:guid}", Actualizar)
            .WithSummary("Actualiza un empleado. El supervisor solo dentro de su departamento.")
            .ConValidacion<SolicitudActualizarEmpleado>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", DarDeBaja)
            .WithSummary("Baja lógica: desactiva al empleado y bloquea su acceso.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/reactivar", Reactivar)
            .WithSummary("Reactiva a un empleado dado de baja.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/reiniciar-acceso", ReiniciarAcceso)
            .WithSummary("Genera una contraseña temporal nueva y fuerza su cambio.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return rutas;
    }

    private static async Task<IResult> Listar(
        [AsParameters] ParametrosConsulta filtros,
        [FromQuery] Guid? departamentoId,
        [FromQuery] Guid? sedeId,
        [FromQuery] Guid? turnoId,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeListarEmpleados(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var parametros = filtros.Normalizar();
        var consulta = bd.Empleados.AsNoTracking().AsQueryable();

        if (departamentoId is { } depto)
        {
            consulta = consulta.Where(empleado => empleado.DepartamentoId == depto);
        }

        if (sedeId is { } sede)
        {
            consulta = consulta.Where(empleado => empleado.SedeId == sede);
        }

        if (turnoId is { } turno)
        {
            consulta = consulta.Where(empleado => empleado.TurnoId == turno);
        }

        if (parametros.Activo is { } activo)
        {
            consulta = consulta.Where(empleado => empleado.Activo == activo);
        }

        if (parametros.Buscar is { } texto)
        {
            var patron = $"%{texto}%";
            consulta = consulta.Where(empleado =>
                EF.Functions.ILike(empleado.NumeroEmpleado, patron) ||
                EF.Functions.ILike(empleado.Nombres, patron) ||
                EF.Functions.ILike(empleado.ApellidoPaterno, patron) ||
                (empleado.ApellidoMaterno != null && EF.Functions.ILike(empleado.ApellidoMaterno, patron)) ||
                EF.Functions.ILike(empleado.CorreoCorporativo, patron) ||
                (empleado.Puesto != null && EF.Functions.ILike(empleado.Puesto, patron)));
        }

        var ordenada = consulta.OrdenarPor(
            parametros.OrdenarPor, parametros.Descendente, Ordenables, empleado => empleado.ApellidoPaterno);

        var total = await ordenada.CountAsync(ct);

        var filas = await ordenada
            .Skip(parametros.Salto)
            .Take(parametros.Tamano)
            .Select(Proyeccion(bd))
            .ToListAsync(ct);

        var elementos = await Completar(bd, filas, ct);

        return TypedResults.Ok(new ResultadoPaginado<EmpleadoDto>(
            elementos, parametros.Pagina, parametros.Tamano, total));
    }

    private static async Task<IResult> Obtener(
        Guid id,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeVerEmpleado(contexto, id).Rechazo() is { } alto)
        {
            return alto;
        }

        var fila = await bd.Empleados
            .AsNoTracking()
            .Where(empleado => empleado.Id == id)
            .Select(Proyeccion(bd))
            .FirstOrDefaultAsync(ct);

        if (fila is null)
        {
            return NoEncontrado(id);
        }

        var completos = await Completar(bd, [fila], ct);
        return TypedResults.Ok(completos[0]);
    }

    private static async Task<IResult> Crear(
        SolicitudCrearEmpleado solicitud,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        ILoggerFactory registroFactory,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);

        if (PoliticaAcceso.PuedeCrearEmpleado(contexto, solicitud.DepartamentoId).Rechazo() is { } alto)
        {
            return alto;
        }

        if (PoliticaAcceso.PuedeAsignarRol(contexto, solicitud.Rol).Rechazo() is { } altoRol)
        {
            return altoRol;
        }

        var correo = solicitud.CorreoCorporativo.Trim().ToLowerInvariant();
        var numero = solicitud.NumeroEmpleado.Trim().ToUpperInvariant();

        if (await RevisarCatalogos(bd, solicitud.DepartamentoId, solicitud.SedeId, solicitud.TurnoId, ct) is { } catalogo)
        {
            return catalogo;
        }

        if (await bd.Empleados.AnyAsync(e => e.NumeroEmpleado == numero, ct))
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudCrearEmpleado.NumeroEmpleado), "Ya existe un empleado con ese número.");
        }

        if (await bd.Empleados.AnyAsync(e => e.CorreoCorporativo == correo, ct) ||
            await gestorUsuarios.FindByEmailAsync(correo) is not null)
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudCrearEmpleado.CorreoCorporativo), "Ese correo ya está en uso.");
        }

        var registro = registroFactory.CreateLogger("Chronos.Empleados");
        var contrasenaTemporal = GeneradorContrasenas.Temporal();

        // La cuenta y el expediente se crean juntos o no se crea ninguno: un empleado sin
        // acceso, o un acceso sin expediente, dejarían el sistema en un estado inconsistente.
        await using var transaccion = await bd.Database.BeginTransactionAsync(ct);

        var cuenta = new UsuarioAplicacion
        {
            UserName = correo,
            Email = correo,
            EmailConfirmed = true,
            NombreParaMostrar = $"{solicitud.Nombres.Trim()} {solicitud.ApellidoPaterno.Trim()}",
            DebeCambiarContrasena = true
        };

        var creacion = await gestorUsuarios.CreateAsync(cuenta, contrasenaTemporal);

        if (!creacion.Succeeded)
        {
            await transaccion.RollbackAsync(ct);
            return ErroresDeIdentity(creacion, nameof(SolicitudCrearEmpleado.CorreoCorporativo));
        }

        var asignacion = await gestorUsuarios.AddToRoleAsync(cuenta, Roles.Nombre(solicitud.Rol));

        if (!asignacion.Succeeded)
        {
            await transaccion.RollbackAsync(ct);
            return ErroresDeIdentity(asignacion, nameof(SolicitudCrearEmpleado.Rol));
        }

        var empleado = new Empleado
        {
            NumeroEmpleado = numero,
            Nombres = solicitud.Nombres.Trim(),
            ApellidoPaterno = solicitud.ApellidoPaterno.Trim(),
            ApellidoMaterno = Vacio(solicitud.ApellidoMaterno),
            CorreoCorporativo = correo,
            Puesto = Vacio(solicitud.Puesto),
            FechaIngreso = solicitud.FechaIngreso,
            DepartamentoId = solicitud.DepartamentoId,
            SedeId = solicitud.SedeId,
            TurnoId = solicitud.TurnoId,
            UsuarioId = cuenta.Id,
            Activo = true
        };

        bd.Empleados.Add(empleado);
        await bd.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        registro.LogInformation(
            "Alta de empleado {Numero} ({EmpleadoId}) con rol {Rol} por {Autor}",
            numero, empleado.Id, solicitud.Rol, contexto.EmpleadoId);

        var dto = await LeerUno(bd, empleado.Id, ct);

        return TypedResults.Created(
            $"/api/v1/empleados/{empleado.Id}",
            new RespuestaAltaEmpleado(dto!, contrasenaTemporal));
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        SolicitudActualizarEmpleado solicitud,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);
        var empleado = await bd.Empleados.FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
        {
            // El 404 va después de comprobar el rol para no filtrar qué identificadores
            // existen a quien ni siquiera tiene permiso de listar.
            return PoliticaAcceso.PuedeListarEmpleados(contexto).Rechazo() ?? NoEncontrado(id);
        }

        var decision = PoliticaAcceso.PuedeEditarEmpleado(contexto, empleado.DepartamentoId, solicitud.DepartamentoId);

        if (decision.Rechazo() is { } alto)
        {
            return alto;
        }

        if (await RevisarCatalogos(bd, solicitud.DepartamentoId, solicitud.SedeId, solicitud.TurnoId, ct) is { } catalogo)
        {
            return catalogo;
        }

        var correo = solicitud.CorreoCorporativo.Trim().ToLowerInvariant();
        var numero = solicitud.NumeroEmpleado.Trim().ToUpperInvariant();

        if (await bd.Empleados.AnyAsync(otro => otro.NumeroEmpleado == numero && otro.Id != id, ct))
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudActualizarEmpleado.NumeroEmpleado), "Ya existe otro empleado con ese número.");
        }

        if (await bd.Empleados.AnyAsync(otro => otro.CorreoCorporativo == correo && otro.Id != id, ct))
        {
            return Validacion.ProblemaDeCampo(
                nameof(SolicitudActualizarEmpleado.CorreoCorporativo), "Ese correo ya está en uso.");
        }

        var cuenta = empleado.UsuarioId is { } cuentaId
            ? await gestorUsuarios.FindByIdAsync(cuentaId.ToString())
            : null;

        if (cuenta is not null)
        {
            var rolActual = Roles.MayorPrivilegio(await gestorUsuarios.GetRolesAsync(cuenta));

            if (rolActual != solicitud.Rol)
            {
                // Se comprueban ambos extremos: quitar un rol alto también es un cambio
                // de privilegio y no debe quedar en manos de un supervisor.
                if (PoliticaAcceso.PuedeAsignarRol(contexto, solicitud.Rol).Rechazo() is { } altoDestino)
                {
                    return altoDestino;
                }

                if (PoliticaAcceso.PuedeAsignarRol(contexto, rolActual).Rechazo() is { } altoOrigen)
                {
                    return altoOrigen;
                }

                var resultado = await CambiarRol(gestorUsuarios, cuenta, solicitud.Rol);

                if (!resultado.Succeeded)
                {
                    return ErroresDeIdentity(resultado, nameof(SolicitudActualizarEmpleado.Rol));
                }
            }
        }

        empleado.NumeroEmpleado = numero;
        empleado.Nombres = solicitud.Nombres.Trim();
        empleado.ApellidoPaterno = solicitud.ApellidoPaterno.Trim();
        empleado.ApellidoMaterno = Vacio(solicitud.ApellidoMaterno);
        empleado.CorreoCorporativo = correo;
        empleado.Puesto = Vacio(solicitud.Puesto);
        empleado.FechaIngreso = solicitud.FechaIngreso;
        empleado.DepartamentoId = solicitud.DepartamentoId;
        empleado.SedeId = solicitud.SedeId;
        empleado.TurnoId = solicitud.TurnoId;

        AplicarEstado(empleado, solicitud.Activo, solicitud.FechaBaja);

        if (cuenta is not null)
        {
            cuenta.Email = correo;
            cuenta.UserName = correo;
            cuenta.NombreParaMostrar = empleado.NombreCompleto;
            cuenta.Activo = empleado.Activo;
            await gestorUsuarios.UpdateAsync(cuenta);
        }

        await bd.SaveChangesAsync(ct);

        return TypedResults.Ok(await LeerUno(bd, id, ct));
    }

    private static async Task<IResult> DarDeBaja(
        Guid id,
        [FromQuery] DateOnly? fechaBaja,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        CancellationToken ct) =>
        await CambiarEstado(id, activo: false, fechaBaja, usuario, acceso, bd, gestorUsuarios, ct);

    private static async Task<IResult> Reactivar(
        Guid id,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        CancellationToken ct) =>
        await CambiarEstado(id, activo: true, null, usuario, acceso, bd, gestorUsuarios, ct);

    private static async Task<IResult> CambiarEstado(
        Guid id,
        bool activo,
        DateOnly? fechaBaja,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);
        var empleado = await bd.Empleados.FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
        {
            return PoliticaAcceso.PuedeListarEmpleados(contexto).Rechazo() ?? NoEncontrado(id);
        }

        if (PoliticaAcceso.PuedeCambiarEstadoEmpleado(contexto, empleado.DepartamentoId).Rechazo() is { } alto)
        {
            return alto;
        }

        if (!activo && contexto.EmpleadoId == id)
        {
            return Validacion.ProblemaDeCampo("empleado", "No puedes darte de baja a ti mismo.");
        }

        AplicarEstado(empleado, activo, fechaBaja);

        if (empleado.UsuarioId is { } cuentaId &&
            await gestorUsuarios.FindByIdAsync(cuentaId.ToString()) is { } cuenta)
        {
            cuenta.Activo = activo;
            await gestorUsuarios.UpdateAsync(cuenta);
        }

        await bd.SaveChangesAsync(ct);

        return TypedResults.Ok(await LeerUno(bd, id, ct));
    }

    private static async Task<IResult> ReiniciarAcceso(
        Guid id,
        ClaimsPrincipal usuario,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        UserManager<UsuarioAplicacion> gestorUsuarios,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(usuario, ct);
        var empleado = await bd.Empleados.FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
        {
            return PoliticaAcceso.PuedeListarEmpleados(contexto).Rechazo() ?? NoEncontrado(id);
        }

        if (PoliticaAcceso.PuedeCambiarEstadoEmpleado(contexto, empleado.DepartamentoId).Rechazo() is { } alto)
        {
            return alto;
        }

        if (empleado.UsuarioId is not { } cuentaId ||
            await gestorUsuarios.FindByIdAsync(cuentaId.ToString()) is not { } cuenta)
        {
            return Validacion.ProblemaDeCampo("empleado", "Este empleado no tiene una cuenta de acceso asociada.");
        }

        var contrasenaTemporal = GeneradorContrasenas.Temporal();
        var ficha = await gestorUsuarios.GeneratePasswordResetTokenAsync(cuenta);
        var resultado = await gestorUsuarios.ResetPasswordAsync(cuenta, ficha, contrasenaTemporal);

        if (!resultado.Succeeded)
        {
            return ErroresDeIdentity(resultado, "contrasena");
        }

        cuenta.DebeCambiarContrasena = true;
        await gestorUsuarios.UpdateAsync(cuenta);

        var dto = await LeerUno(bd, id, ct);
        return TypedResults.Ok(new RespuestaAltaEmpleado(dto!, contrasenaTemporal));
    }

    private static void AplicarEstado(Empleado empleado, bool activo, DateOnly? fechaBaja)
    {
        empleado.Activo = activo;
        empleado.FechaBaja = activo
            ? null
            : fechaBaja ?? empleado.FechaBaja ?? DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static async Task<IdentityResult> CambiarRol(
        UserManager<UsuarioAplicacion> gestorUsuarios,
        UsuarioAplicacion cuenta,
        RolChronos destino)
    {
        var actuales = await gestorUsuarios.GetRolesAsync(cuenta);

        if (actuales.Count > 0)
        {
            var quitar = await gestorUsuarios.RemoveFromRolesAsync(cuenta, actuales);

            if (!quitar.Succeeded)
            {
                return quitar;
            }
        }

        return await gestorUsuarios.AddToRoleAsync(cuenta, Roles.Nombre(destino));
    }

    private static async Task<IResult?> RevisarCatalogos(
        ChronosDbContext bd,
        Guid departamentoId,
        Guid sedeId,
        Guid? turnoId,
        CancellationToken ct)
    {
        var departamento = await bd.Departamentos
            .AsNoTracking()
            .Where(depto => depto.Id == departamentoId)
            .Select(depto => new { depto.SedeId, depto.Activo })
            .FirstOrDefaultAsync(ct);

        if (departamento is null)
        {
            return Validacion.ProblemaDeCampo("departamentoId", "El departamento indicado no existe.");
        }

        if (!departamento.Activo)
        {
            return Validacion.ProblemaDeCampo("departamentoId", "El departamento está dado de baja.");
        }

        if (!await bd.Sedes.AnyAsync(sede => sede.Id == sedeId, ct))
        {
            return Validacion.ProblemaDeCampo("sedeId", "La sede indicada no existe.");
        }

        // El departamento pertenece a una sede; permitir la combinación cruzada dejaría
        // al empleado en una sede donde su departamento no opera.
        if (departamento.SedeId != sedeId)
        {
            return Validacion.ProblemaDeCampo(
                "sedeId", "El departamento seleccionado no pertenece a esa sede.");
        }

        if (turnoId is { } turno && !await bd.Turnos.AnyAsync(t => t.Id == turno && t.Activo, ct))
        {
            return Validacion.ProblemaDeCampo("turnoId", "El turno indicado no existe o está dado de baja.");
        }

        return null;
    }

    internal static async Task<EmpleadoDto?> LeerUno(ChronosDbContext bd, Guid id, CancellationToken ct)
    {
        var fila = await bd.Empleados
            .AsNoTracking()
            .Where(empleado => empleado.Id == id)
            .Select(Proyeccion(bd))
            .FirstOrDefaultAsync(ct);

        if (fila is null)
        {
            return null;
        }

        var completos = await Completar(bd, [fila], ct);
        return completos[0];
    }

    /// <summary>
    /// El rol vive en las tablas de Identity, no en el expediente. Se resuelve en una
    /// consulta aparte sobre los usuarios de la página en curso, en vez de una subconsulta
    /// por fila, y la precedencia entre roles se decide en memoria.
    /// </summary>
    private static async Task<List<EmpleadoDto>> Completar(
        ChronosDbContext bd,
        IReadOnlyList<FilaEmpleado> filas,
        CancellationToken ct)
    {
        var cuentas = filas.Where(fila => fila.UsuarioId is not null)
            .Select(fila => fila.UsuarioId!.Value)
            .Distinct()
            .ToArray();

        var roles = cuentas.Length == 0
            ? []
            : (await bd.UserRoles
                    .Where(vinculo => cuentas.Contains(vinculo.UserId))
                    .Join(bd.Roles, vinculo => vinculo.RoleId, rol => rol.Id,
                        (vinculo, rol) => new { vinculo.UserId, rol.Name })
                    .ToListAsync(ct))
                .GroupBy(fila => fila.UserId)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => Roles.MayorPrivilegio(grupo.Select(fila => fila.Name ?? string.Empty)));

        return filas.Select(fila => fila.AEmpleadoDto(
            fila.UsuarioId is { } cuenta && roles.TryGetValue(cuenta, out var rol) ? rol : RolChronos.Empleado)).ToList();
    }

    private static Expression<Func<Empleado, FilaEmpleado>> Proyeccion(ChronosDbContext bd) => empleado => new FilaEmpleado(
        empleado.Id,
        empleado.NumeroEmpleado,
        empleado.Nombres,
        empleado.ApellidoPaterno,
        empleado.ApellidoMaterno,
        empleado.CorreoCorporativo,
        empleado.Puesto,
        empleado.FechaIngreso,
        empleado.FechaBaja,
        empleado.Activo,
        empleado.DepartamentoId,
        empleado.Departamento != null ? empleado.Departamento.Nombre : string.Empty,
        empleado.SedeId,
        empleado.Sede != null ? empleado.Sede.Nombre : string.Empty,
        empleado.TurnoId,
        empleado.Turno != null ? empleado.Turno.Nombre : null,
        empleado.UsuarioId,
        bd.Users.Where(cuenta => cuenta.Id == empleado.UsuarioId)
            .Select(cuenta => cuenta.DebeCambiarContrasena)
            .FirstOrDefault());

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static IResult NoEncontrado(Guid id) => TypedResults.Problem(
        detail: $"No existe un empleado con el identificador {id}.",
        statusCode: StatusCodes.Status404NotFound,
        title: "Empleado no encontrado");

    /// <summary>Traduce los errores de Identity al mismo formato por campo que el resto.</summary>
    private static IResult ErroresDeIdentity(IdentityResult resultado, string campo) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [campo] = resultado.Errors.Select(error => error.Description).ToArray()
        });
}

internal sealed record FilaEmpleado(
    Guid Id,
    string NumeroEmpleado,
    string Nombres,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string CorreoCorporativo,
    string? Puesto,
    DateOnly FechaIngreso,
    DateOnly? FechaBaja,
    bool Activo,
    Guid DepartamentoId,
    string DepartamentoNombre,
    Guid SedeId,
    string SedeNombre,
    Guid? TurnoId,
    string? TurnoNombre,
    Guid? UsuarioId,
    bool DebeCambiarContrasena)
{
    public EmpleadoDto AEmpleadoDto(RolChronos rol) => new(
        Id,
        NumeroEmpleado,
        Nombres,
        ApellidoPaterno,
        ApellidoMaterno,
        string.Join(' ', new[] { Nombres, ApellidoPaterno, ApellidoMaterno }
            .Where(parte => !string.IsNullOrWhiteSpace(parte))),
        CorreoCorporativo,
        Puesto,
        FechaIngreso,
        FechaBaja,
        Activo,
        DepartamentoId,
        DepartamentoNombre,
        SedeId,
        SedeNombre,
        TurnoId,
        TurnoNombre,
        rol,
        DebeCambiarContrasena);
}
