using System.Security.Claims;
using System.Text.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;
using Chronos.Domain.Seguridad;
using Chronos.Infrastructure.Fichaje;
using Chronos.Infrastructure.Persistencia;
using Fido2NetLib;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Endpoints;

/// <summary>
/// Fichaje por código QR.
/// <para>
/// El sentido del flujo importa: el kiosco de la sede <em>muestra</em> el código y el
/// empleado lo <em>escanea</em> desde su teléfono ya autenticado. Al revés —que el
/// empleado mostrara su propio código— bastaría con mandarlo por mensajería para que otro
/// fichara por él. Así, el código sin la sesión no vale, y la sesión sin haber estado
/// frente a la pantalla tampoco.
/// </para>
/// </summary>
public static class EndpointsFichaje
{
    public static IEndpointRouteBuilder MapearFichaje(this IEndpointRouteBuilder rutas)
    {
        var kiosco = rutas
            .MapGroup("/api/v1/kiosco")
            .WithTags("Kiosco")
            .RequireAuthorization();

        kiosco.MapGet("/{sedeId:guid}/codigo", EmitirCodigo)
            .WithSummary("Emite un código firmado y de vida corta para la pantalla de una sede.")
            .Produces<CodigoKioscoDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var checadas = rutas
            .MapGroup("/api/v1/checadas")
            .WithTags("Checadas")
            .RequireAuthorization();

        checadas.MapPost("/qr", FicharConTexto)
            .WithSummary("Registra un fichaje a partir del código ya decodificado por la cámara.")
            .Produces<ChecadaDto>(StatusCodes.Status201Created)
            .Produces<RechazoChecadaDto>(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ConValidacion<SolicitudChecadaQr>();

        checadas.MapPost("/qr/imagen", FicharConImagen)
            .WithSummary("Registra un fichaje a partir de una foto del código.")
            .Produces<ChecadaDto>(StatusCodes.Status201Created)
            .Produces<RechazoChecadaDto>(StatusCodes.Status422UnprocessableEntity)
            .DisableAntiforgery();

        checadas.MapGet("/mias", ListarPropias)
            .WithSummary("Historial de fichajes del usuario autenticado, con sus señales.")
            .Produces<ResultadoPaginado<ChecadaDto>>();

        return rutas;
    }

    private static async Task<IResult> EmitirCodigo(
        Guid sedeId,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioQr servicioQr,
        ChronosDbContext bd,
        TimeProvider reloj,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeMostrarKiosco(contexto, sedeId).Rechazo() is { } alto)
        {
            return alto;
        }

        var sede = await bd.Sedes
            .AsNoTracking()
            .Where(s => s.Id == sedeId && s.Activa)
            .Select(s => new { s.Id, s.Nombre })
            .FirstOrDefaultAsync(ct);

        if (sede is null)
        {
            return TypedResults.Problem(
                title: "Sede no encontrada",
                detail: "La sede no existe o está dada de baja.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var codigo = servicioQr.Emitir(sedeId, reloj.GetUtcNow());

        return TypedResults.Ok(new CodigoKioscoDto(
            codigo.Texto,
            Convert.ToBase64String(codigo.ImagenPng),
            codigo.EmitidoUtc,
            codigo.ExpiraUtc,
            codigo.SegundosRefresco,
            sede.Id,
            sede.Nombre));
    }

    private static Task<IResult> FicharConTexto(
        SolicitudChecadaQr solicitud,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioQr servicioQr,
        IServicioWebAuthn webAuthn,
        ChronosDbContext bd,
        TimeProvider reloj,
        HttpContext http,
        ILoggerFactory registros,
        CancellationToken ct) =>
        Fichar(solicitud.Token, solicitud.Tipo, solicitud.Asercion, principal, acceso, servicioQr, webAuthn, bd, reloj, http, registros, ct);

    /// <summary>
    /// Alternativa para cuando la cámara en vivo no se puede usar. La foto se decodifica
    /// en el servidor con ZXing y a partir de ahí el camino es idéntico.
    /// </summary>
    private static async Task<IResult> FicharConImagen(
        [FromForm] IFormFile imagen,
        [FromForm] TipoChecada? tipo,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioQr servicioQr,
        IServicioWebAuthn webAuthn,
        ILectorImagenQr lector,
        ChronosDbContext bd,
        TimeProvider reloj,
        HttpContext http,
        ILoggerFactory registros,
        CancellationToken ct)
    {
        const int MaximoBytes = 8 * 1024 * 1024;

        if (imagen.Length is 0 or > MaximoBytes)
        {
            return Validacion.ProblemaDeCampo(
                nameof(imagen),
                $"La imagen debe pesar entre 1 byte y {MaximoBytes / (1024 * 1024)} MB.");
        }

        using var memoria = new MemoryStream((int)imagen.Length);
        await imagen.CopyToAsync(memoria, ct);

        var texto = lector.Decodificar(memoria.ToArray());

        if (texto is null)
        {
            return Rechazo(MotivoRechazoQr.FormatoInvalido, "No se encontró ningún código QR en la imagen.");
        }

        // La ruta de la foto no lleva firma biométrica: es el plan B de quien ya tiene
        // problemas con la cámara, y encadenarle una ceremonia WebAuthn en el mismo viaje
        // sería exigirle justo lo que no le está funcionando.
        return await Fichar(texto, tipo, null, principal, acceso, servicioQr, webAuthn, bd, reloj, http, registros, ct);
    }

    private static async Task<IResult> Fichar(
        string token,
        TipoChecada? tipoPedido,
        AuthenticatorAssertionRawResponse? asercion,
        ClaimsPrincipal principal,
        IResolutorAcceso acceso,
        IServicioQr servicioQr,
        IServicioWebAuthn webAuthn,
        ChronosDbContext bd,
        TimeProvider reloj,
        HttpContext http,
        ILoggerFactory registros,
        CancellationToken ct)
    {
        var contexto = await acceso.ResolverAsync(principal, ct);

        if (PoliticaAcceso.PuedeFichar(contexto).Rechazo() is { } alto)
        {
            return alto;
        }

        var ahora = reloj.GetUtcNow();
        var lectura = servicioQr.Leer(token, ahora);

        if (!lectura.Valido)
        {
            return Rechazo(lectura.Motivo);
        }

        var empleadoId = contexto.EmpleadoId!.Value;

        var expediente = await bd.Empleados
            .AsNoTracking()
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Activo,
                e.SedeId,
                SedeNombre = e.Sede!.Nombre,
                e.Sede.ZonaHoraria
            })
            .FirstOrDefaultAsync(ct);

        if (expediente is null || !expediente.Activo)
        {
            return TypedResults.Problem(
                title: "Expediente no disponible",
                detail: "La cuenta no tiene un expediente activo con el cual fichar.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // El código es de una sede y el empleado pertenece a otra. Es el caso de alguien
        // que consiguió un código válido de una sede donde no trabaja.
        if (expediente.SedeId != lectura.Token.SedeId)
        {
            return Rechazo(MotivoRechazoQr.SedeNoCorresponde);
        }

        var diaLaboral = DiaLaboralDe(ahora, expediente.ZonaHoraria);

        var delDia = await bd.Checadas
            .AsNoTracking()
            .Where(c => c.EmpleadoId == empleadoId && c.DiaLaboral == diaLaboral)
            .OrderByDescending(c => c.MomentoUtc)
            .Select(c => new { c.Tipo, c.MomentoUtc })
            .ToListAsync(ct);

        var tipo = tipoPedido ?? PoliticaFichaje.SiguienteTipo(delDia.Count > 0 ? delDia[0].Tipo : null);

        var ultimaDelTipo = delDia
            .Where(c => c.Tipo == tipo)
            .Select(c => (DateTimeOffset?)c.MomentoUtc)
            .FirstOrDefault();

        if (PoliticaFichaje.Predeterminada.EsDuplicada(ultimaDelTipo, ahora))
        {
            return TypedResults.Problem(
                title: "Fichaje duplicado",
                detail: $"Ya se registró una {tipo.ToString().ToLowerInvariant()} hace menos de "
                        + $"{PoliticaFichaje.Predeterminada.VentanaAntiduplicados.TotalMinutes:0} minutos.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var checada = new Checada
        {
            EmpleadoId = empleadoId,
            Tipo = tipo,
            MomentoUtc = ahora,
            DiaLaboral = diaLaboral,
            SedeId = lectura.Token.SedeId,
            DireccionIp = http.Connection.RemoteIpAddress?.ToString()
        };

        checada.AgregarSenal(
            TipoSenal.CodigoQr,
            ResultadoSenal.Confirmada,
            JsonSerializer.Serialize(new
            {
                nonce = lectura.Token.Nonce,
                emitidoUtc = lectura.Token.EmitidoUtc,
                segundosParaEscanear = (int)(ahora - lectura.Token.EmitidoUtc).TotalSeconds
            }),
            ahora);

        if (asercion is not null)
        {
            await AgregarSenalWebAuthn(checada, asercion, empleadoId, webAuthn, bd, ahora, registros, ct);
        }

        bd.Checadas.Add(checada);

        // El asiento del nonce va en el mismo SaveChanges que la checada. Si se guardaran
        // por separado, una caída entre ambos dejaría un código gastado sin fichaje, o un
        // fichaje con el código todavía disponible para repetirse.
        bd.NoncesQrConsumidos.Add(new NonceQrConsumido
        {
            Nonce = lectura.Token.Nonce,
            SedeId = lectura.Token.SedeId,
            EmpleadoId = empleadoId,
            ChecadaId = checada.Id,
            ConsumidoUtc = ahora,
            ExpiraUtc = lectura.Token.ExpiraUtc
        });

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (ServicioQr.EsNonceDuplicado(excepcion))
        {
            return Rechazo(MotivoRechazoQr.NonceReusado);
        }

        var dto = Mapear(checada, expediente.SedeNombre);

        return TypedResults.Created($"/api/v1/checadas/{checada.Id}", dto);
    }

    /// <summary>
    /// Comprueba la firma del autenticador y la anota como señal.
    /// <para>
    /// Una firma que no cuadra no tumba el fichaje: lo deja registrado como intento
    /// fallido. Rechazar el viaje entero borraría el rastro justo en el caso más
    /// interesante para RH, que es el de alguien presentando una firma que no verifica.
    /// </para>
    /// </summary>
    private static async Task AgregarSenalWebAuthn(
        Checada checada,
        AuthenticatorAssertionRawResponse asercion,
        Guid empleadoId,
        IServicioWebAuthn webAuthn,
        ChronosDbContext bd,
        DateTimeOffset ahora,
        ILoggerFactory registros,
        CancellationToken ct)
    {
        var registro = registros.CreateLogger("Chronos.WebAuthn");

        var desafio = await EndpointsWebAuthn.LeerDesafio(
            bd, empleadoId, PropositoDesafio.Autenticacion, ahora, ct);

        if (desafio is null)
        {
            checada.AgregarSenal(
                TipoSenal.WebAuthn,
                ResultadoSenal.Fallida,
                JsonSerializer.Serialize(new { motivo = "DesafioNoVigente" }),
                ahora);

            return;
        }

        bd.DesafiosWebAuthn.Remove(desafio);

        // La credencial se busca acotada al empleado de la sesión: sin ese filtro, una
        // firma válida de otra persona serviría para sumar puntos en esta checada.
        var credencial = await bd.CredencialesWebAuthn
            .FirstOrDefaultAsync(
                c => c.CredentialId == asercion.RawId && c.EmpleadoId == empleadoId && c.Activa,
                ct);

        if (credencial is null)
        {
            checada.AgregarSenal(
                TipoSenal.WebAuthn,
                ResultadoSenal.Fallida,
                JsonSerializer.Serialize(new { motivo = "CredencialDesconocida" }),
                ahora);

            return;
        }

        try
        {
            var resultado = await webAuthn.VerificarAutenticacionAsync(
                asercion,
                AssertionOptions.FromJson(desafio.OpcionesJson),
                credencial,
                ct);

            // Si el contador no avanza, el autenticador pudo haber sido clonado. Fido2 ya
            // lo comprueba y lanza; guardar el nuevo valor es lo que mantiene viva esa
            // defensa para el siguiente fichaje.
            credencial.ContadorFirmas = resultado.SignCount;
            credencial.UltimoUsoUtc = ahora;

            checada.AgregarSenal(
                TipoSenal.WebAuthn,
                ResultadoSenal.Confirmada,
                JsonSerializer.Serialize(new
                {
                    credencialId = credencial.Id,
                    dispositivo = credencial.NombreAmigable,
                    contadorFirmas = resultado.SignCount
                }),
                ahora);
        }
        catch (Fido2VerificationException excepcion)
        {
            registro.LogWarning(
                excepcion,
                "Aserción WebAuthn rechazada al fichar. Empleado {EmpleadoId}, credencial {CredencialId}.",
                empleadoId,
                credencial.Id);

            checada.AgregarSenal(
                TipoSenal.WebAuthn,
                ResultadoSenal.Fallida,
                JsonSerializer.Serialize(new { motivo = "FirmaInvalida" }),
                ahora);
        }
    }

    private static async Task<IResult> ListarPropias(
        [AsParameters] ParametrosConsulta parametros,
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

        var consulta = parametros.Normalizar();

        var origen = bd.Checadas
            .AsNoTracking()
            .Include(c => c.Senales)
            .Include(c => c.Sede)
            .Where(c => c.EmpleadoId == contexto.EmpleadoId!.Value)
            .OrderByDescending(c => c.MomentoUtc);

        var total = await origen.CountAsync(ct);

        var pagina = await origen
            .Skip(consulta.Salto)
            .Take(consulta.Tamano)
            .ToListAsync(ct);

        var elementos = pagina
            .Select(c => Mapear(c, c.Sede?.Nombre))
            .ToList();

        return TypedResults.Ok(
            new ResultadoPaginado<ChecadaDto>(elementos, consulta.Pagina, consulta.Tamano, total));
    }

    private static ChecadaDto Mapear(Checada checada, string? sedeNombre) => new(
        checada.Id,
        checada.Tipo,
        checada.MomentoUtc,
        checada.DiaLaboral,
        checada.Estado,
        checada.PuntajeConfianza,
        checada.NivelConfianza,
        checada.SedeId,
        sedeNombre,
        checada.Observaciones,
        [.. checada.Senales
            .OrderBy(s => s.CapturadaUtc)
            .Select(s => new SenalDto(
                s.Tipo,
                NombreDeSenal(s.Tipo),
                s.Resultado,
                s.PesoAplicado,
                s.CapturadaUtc,
                s.DetalleJson))]);

    internal static string NombreDeSenal(TipoSenal tipo) => tipo switch
    {
        TipoSenal.CodigoQr => "Código QR",
        TipoSenal.WebAuthn => "Biometría del dispositivo",
        TipoSenal.BeaconBle => "Beacon BLE",
        TipoSenal.Geocerca => "Geocerca",
        TipoSenal.RedWifi => "Red WiFi",
        TipoSenal.RegistroManual => "Registro manual",
        _ => tipo.ToString()
    };

    /// <summary>
    /// El día laboral se resuelve en la zona de la sede y no en UTC: un fichaje a las
    /// 19:00 del centro de México son las 01:00 UTC del día siguiente, y sin convertir
    /// quedaría imputado a mañana.
    /// </summary>
    private static DateOnly DiaLaboralDe(DateTimeOffset momento, string zonaHoraria)
    {
        TimeZoneInfo zona;

        try
        {
            zona = TimeZoneInfo.FindSystemTimeZoneById(zonaHoraria);
        }
        catch (Exception excepcion) when (excepcion is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Una sede mal configurada no debe impedir fichar; se imputa en UTC y la
            // incoherencia saldrá en el reporte, que es donde alguien puede corregirla.
            zona = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(momento, zona).DateTime);
    }

    private static IResult Rechazo(MotivoRechazoQr motivo, string? mensaje = null) =>
        TypedResults.UnprocessableEntity(new RechazoChecadaDto(motivo, mensaje ?? MensajeDe(motivo)));

    private static string MensajeDe(MotivoRechazoQr motivo) => motivo switch
    {
        MotivoRechazoQr.FormatoInvalido => "El código escaneado no es un código de Chronos.",
        MotivoRechazoQr.FirmaInvalida => "El código no fue emitido por este sistema.",
        MotivoRechazoQr.Caducado => "El código ya venció. Vuelve a escanear el de la pantalla.",
        MotivoRechazoQr.NonceReusado => "Este código ya se usó. Escanea el que muestra la pantalla ahora.",
        MotivoRechazoQr.SedeNoCorresponde => "Ese código pertenece a otra sede.",
        _ => "No se pudo validar el código."
    };
}
