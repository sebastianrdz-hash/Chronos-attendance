using System.Security.Claims;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Identidad;
using Chronos.Infrastructure.Persistencia;
using Chronos.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Seguridad;

public interface IResolutorAcceso
{
    ValueTask<ContextoAcceso> ResolverAsync(ClaimsPrincipal usuario, CancellationToken ct = default);
}

/// <summary>
/// Traduce el <see cref="ClaimsPrincipal"/> al <see cref="ContextoAcceso"/> del dominio.
///
/// El rol y el identificador de empleado salen del token, pero el departamento se lee de
/// la base: es la frontera que decide qué puede escribir un supervisor, y si viniera del
/// token seguiría mandando sobre su departamento anterior hasta que este expirara. El
/// resultado se memoriza porque el servicio vive lo que dura la petición.
/// </summary>
internal sealed class ResolutorAcceso(ChronosDbContext contexto) : IResolutorAcceso
{
    private ContextoAcceso? _resuelto;

    public async ValueTask<ContextoAcceso> ResolverAsync(ClaimsPrincipal usuario, CancellationToken ct = default)
    {
        if (_resuelto is not null)
        {
            return _resuelto;
        }

        var rol = Roles.MayorPrivilegio(usuario.FindAll(ClaimsChronos.Rol).Select(claim => claim.Value));
        var empleadoId = LeerGuid(usuario, ClaimsChronos.EmpleadoId);

        var expediente = empleadoId is null
            ? null
            : await contexto.Empleados
                .AsNoTracking()
                .Where(empleado => empleado.Id == empleadoId)
                .Select(empleado => new { empleado.DepartamentoId, empleado.SedeId })
                .FirstOrDefaultAsync(ct);

        _resuelto = new ContextoAcceso
        {
            Rol = rol,
            EmpleadoId = empleadoId,
            DepartamentoId = expediente?.DepartamentoId,
            SedeId = expediente?.SedeId
        };

        return _resuelto;
    }

    private static Guid? LeerGuid(ClaimsPrincipal usuario, string tipo) =>
        Guid.TryParse(usuario.FindFirstValue(tipo), out var valor) ? valor : null;
}
