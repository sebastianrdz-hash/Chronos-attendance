using System.Security.Claims;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Fichaje;
using Chronos.Infrastructure.Persistencia;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

/// <summary>
/// Alta y baja de credenciales FIDO2, y emisión de los desafíos que se firman al fichar.
/// <para>
/// De la biometría no se guarda nada: el sensor del dispositivo desbloquea una llave
/// privada que jamás sale de su enclave seguro, y aquí solo queda la pública y un contador
/// de firmas. Una filtración de esta base no expone ningún rasgo de nadie.
/// </para>
/// </summary>
public static class EndpointsWebAuthn
{
    public static IEndpointRouteBuilder MapearWebAuthn(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/v1/webauthn")
            .WithTags("WebAuthn")
            .RequireAuthorization();

        grupo.MapPost("/enrolamiento/opciones", OpcionesEnrolamiento)
            .WithSummary("Inicia el alta de una credencial y devuelve el desafío a firmar.")
            .Produces<RespuestaOpcionesEnrolamiento>()
            .ConValidacion<SolicitudOpcionesEnrolamiento>();

        grupo.MapPost("/enrolamiento", CompletarEnrolamiento)
            .WithSummary("Verifica la respuesta del autenticador y guarda la clave pública.")
            .Produces<CredencialDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/credenciales", ListarCredenciales)
            .WithSummary("Credenciales registradas por el usuario autenticado.")
            .Produces<IReadOnlyList<CredencialDto>>();

        grupo.MapDelete("/credenciales/{id:guid}", RevocarCredencial)
            .WithSummary("Revoca una credencial propia.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/autenticacion/opciones", OpcionesAutenticacion)
            .WithSummary("Emite el desafío que se firmará al fichar.")
            .Produces<RespuestaOpcionesAutenticacion>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return rutas;
    }

    private static async Task<IResult> OpcionesEnrolamiento(
        SolicitudOpcionesEnrolamiento solicitud,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioWebAuthn webAuthn,
        ChronosDbContext bd,
        TimeProvider reloj,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var empleadoId = contexto.EmpleadoId!.Value;

        var expediente = await bd.Empleados
            .AsNoTracking()
            .Where(e => e.Id == empleadoId && e.Activo)
            .Select(e => new
            {
                e.CorreoCorporativo,
                Nombre = e.Nombres + " " + e.ApellidoPaterno
            })
            .FirstOrDefaultAsync(ct);

        if (expediente is null)
        {
            return TypedResults.Problem(
                title: "Expediente no disponible",
                detail: "La cuenta no tiene un expediente activo.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var existentes = await bd.CredencialesWebAuthn
            .AsNoTracking()
            .Where(c => c.EmpleadoId == empleadoId && c.Activa)
            .ToListAsync(ct);

        if (existentes.Count >= webAuthn.Opciones.MaximoCredenciales)
        {
            return TypedResults.Problem(
                title: "Demasiadas credenciales",
                detail: $"Ya tienes {existentes.Count} dispositivos registrados. Revoca alguno antes de añadir otro.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var opciones = webAuthn.OpcionesDeEnrolamiento(
            empleadoId,
            expediente.CorreoCorporativo,
            expediente.Nombre,
            existentes);

        await GuardarDesafio(
            bd,
            empleadoId,
            PropositoDesafio.Enrolamiento,
            opciones.ToJson(),
            webAuthn,
            reloj,
            ct,
            solicitud.NombreAmigable.Trim());

        return TypedResults.Ok(new RespuestaOpcionesEnrolamiento(opciones));
    }

    private static async Task<IResult> CompletarEnrolamiento(
        SolicitudEnrolamiento solicitud,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioWebAuthn webAuthn,
        ChronosDbContext bd,
        TimeProvider reloj,
        ILoggerFactory registros,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var empleadoId = contexto.EmpleadoId!.Value;
        var ahora = reloj.GetUtcNow();

        var desafio = await LeerDesafio(bd, empleadoId, PropositoDesafio.Enrolamiento, ahora, ct);

        if (desafio is null)
        {
            return TypedResults.UnprocessableEntity(new RechazoWebAuthnDto(
                "DesafioNoVigente",
                "El desafío venció o no existe. Vuelve a empezar el registro."));
        }

        var original = CredentialCreateOptions.FromJson(desafio.OpcionesJson);

        RegisteredPublicKeyCredential credencial;

        try
        {
            credencial = await webAuthn.VerificarEnrolamientoAsync(
                solicitud.Respuesta,
                original,
                async (parametros, cancelacion) => !await bd.CredencialesWebAuthn
                    .AsNoTracking()
                    .AnyAsync(c => c.CredentialId == parametros.CredentialId, cancelacion),
                ct);
        }
        catch (Fido2VerificationException excepcion)
        {
            registros.CreateLogger("Chronos.WebAuthn")
                .LogWarning(excepcion, "Enrolamiento rechazado para el empleado {EmpleadoId}.", empleadoId);

            return TypedResults.UnprocessableEntity(new RechazoWebAuthnDto(
                "VerificacionFallida",
                "El dispositivo no superó la verificación. Inténtalo de nuevo."));
        }
        finally
        {
            // El desafío se consume pase lo que pase: si se dejara vivo tras un fallo,
            // quedaría disponible para reintentos indefinidos con el mismo reto.
            bd.DesafiosWebAuthn.Remove(desafio);
            await bd.SaveChangesAsync(ct);
        }

        var nueva = new CredencialWebAuthn
        {
            EmpleadoId = empleadoId,
            CredentialId = credencial.Id,
            ClavePublica = credencial.PublicKey,
            IdUsuario = credencial.User.Id,
            ContadorFirmas = credencial.SignCount,
            AaGuid = credencial.AaGuid,
            NombreAmigable = desafio.NombreDispositivo,
            TipoDispositivo = credencial.Type.ToString()
        };

        bd.CredencialesWebAuthn.Add(nueva);
        await bd.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/v1/webauthn/credenciales/{nueva.Id}", Mapear(nueva));
    }

    private static async Task<IResult> ListarCredenciales(
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var credenciales = await bd.CredencialesWebAuthn
            .AsNoTracking()
            .Where(c => c.EmpleadoId == contexto.EmpleadoId!.Value && c.Activa)
            .OrderBy(c => c.CreadoUtc)
            .ToListAsync(ct);

        return TypedResults.Ok(credenciales.Select(Mapear).ToList());
    }

    private static async Task<IResult> RevocarCredencial(
        Guid id,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        ChronosDbContext bd,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        // El filtro por empleado va en la consulta, no en una comprobación posterior: así
        // no hay forma de revocar la credencial de otra persona conociendo su identificador.
        var credencial = await bd.CredencialesWebAuthn
            .FirstOrDefaultAsync(c => c.Id == id && c.EmpleadoId == contexto.EmpleadoId!.Value, ct);

        if (credencial is null)
        {
            return TypedResults.Problem(
                title: "Credencial no encontrada",
                detail: "No existe una credencial tuya con ese identificador.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Baja lógica, como todo lo demás en Chronos: las checadas ya firmadas por esta
        // credencial deben poder seguir explicándose después de revocarla.
        credencial.Activa = false;
        await bd.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> OpcionesAutenticacion(
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioWebAuthn webAuthn,
        ChronosDbContext bd,
        TimeProvider reloj,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var empleadoId = contexto.EmpleadoId!.Value;

        var credenciales = await bd.CredencialesWebAuthn
            .AsNoTracking()
            .Where(c => c.EmpleadoId == empleadoId && c.Activa)
            .ToListAsync(ct);

        if (credenciales.Count == 0)
        {
            return TypedResults.Problem(
                title: "Sin credenciales",
                detail: "Todavía no registras ningún dispositivo. Hazlo desde tu perfil.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var opciones = webAuthn.OpcionesDeAutenticacion(credenciales);

        await GuardarDesafio(bd, empleadoId, PropositoDesafio.Autenticacion, opciones.ToJson(), webAuthn, reloj, ct);

        return TypedResults.Ok(new RespuestaOpcionesAutenticacion(opciones));
    }

    // ---------- Compartido con el fichaje ----------

    internal static async Task GuardarDesafio(
        ChronosDbContext bd,
        Guid empleadoId,
        PropositoDesafio proposito,
        string opcionesJson,
        IServicioWebAuthn webAuthn,
        TimeProvider reloj,
        CancellationToken ct,
        string? nombreDispositivo = null)
    {
        var previo = await bd.DesafiosWebAuthn
            .FirstOrDefaultAsync(d => d.EmpleadoId == empleadoId && d.Proposito == proposito, ct);

        if (previo is not null)
        {
            bd.DesafiosWebAuthn.Remove(previo);
        }

        bd.DesafiosWebAuthn.Add(new DesafioWebAuthn
        {
            EmpleadoId = empleadoId,
            Proposito = proposito,
            OpcionesJson = opcionesJson,
            NombreDispositivo = nombreDispositivo,
            CreadoUtc = reloj.GetUtcNow(),
            ExpiraUtc = reloj.GetUtcNow().AddSeconds(webAuthn.Opciones.SegundosVigenciaDesafio)
        });

        await bd.SaveChangesAsync(ct);
    }

    internal static async Task<DesafioWebAuthn?> LeerDesafio(
        ChronosDbContext bd,
        Guid empleadoId,
        PropositoDesafio proposito,
        DateTimeOffset ahora,
        CancellationToken ct)
    {
        var desafio = await bd.DesafiosWebAuthn
            .FirstOrDefaultAsync(d => d.EmpleadoId == empleadoId && d.Proposito == proposito, ct);

        return desafio?.Vigente(ahora) == true ? desafio : null;
    }

    internal static CredencialDto Mapear(CredencialWebAuthn credencial) => new(
        credencial.Id,
        credencial.NombreAmigable,
        credencial.TipoDispositivo,
        credencial.CreadoUtc,
        credencial.UltimoUsoUtc,
        credencial.ContadorFirmas,
        credencial.Activa);
}

public sealed record RechazoWebAuthnDto(string Motivo, string Mensaje);
