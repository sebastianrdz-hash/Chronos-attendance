using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Api.Tests;

[Collection(nameof(ColeccionApi))]
public class RevisionTests(FabricaApiPruebas fabrica)
{
    private static readonly Guid Monterrey = SembradorDatos.Ids.SedeMonterrey;
    private static readonly Guid Sistemas = SembradorDatos.Ids.DeptoSistemas;
    private static readonly Guid RecursosHumanos = SembradorDatos.Ids.DeptoRecursosHumanos;

    // ---------- La bandeja ----------

    [Fact]
    public async Task UnaChecadaDebilAparecePendienteConElDetalleDeSusSenales()
    {
        var (checada, _) = await ChecadaDudosaAsync();

        var pendiente = await BuscarPendienteAsync(await Admin(), checada.Id);

        Assert.NotNull(pendiente);
        Assert.Equal(25, pendiente.PuntajeConfianza);
        Assert.Equal(NivelConfianza.Baja, pendiente.NivelConfianza);
        Assert.Equal("Corporativo Monterrey", pendiente.SedeNombre);

        // Quien dictamina necesita ver qué faltó, no solo que el número quedó corto.
        var senal = Assert.Single(pendiente.Senales);
        Assert.Equal(TipoSenal.CodigoQr, senal.Tipo);
        Assert.Equal("Código QR", senal.TipoNombre);
    }

    [Fact]
    public async Task LaBandejaNoLeMuestraANadieSusPropiasChecadas()
    {
        // El supervisor de RH ficha solo con QR: su checada queda débil como cualquier otra.
        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var suya = await FicharAsync(supervisor);

        var pendiente = await BuscarPendienteAsync(supervisor, suya.Id);

        Assert.Null(pendiente);

        // Y no es que se haya perdido: el administrador sí la ve.
        Assert.NotNull(await BuscarPendienteAsync(await Admin(), suya.Id));
    }

    [Fact]
    public async Task ElSupervisorSoloVeLaBandejaDeSuDepartamento()
    {
        var (deSistemas, _) = await ChecadaDudosaAsync(Sistemas);
        var (deRecursosHumanos, _) = await ChecadaDudosaAsync(RecursosHumanos);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        Assert.Null(await BuscarPendienteAsync(supervisor, deSistemas.Id));
        Assert.NotNull(await BuscarPendienteAsync(supervisor, deRecursosHumanos.Id));
    }

    [Fact]
    public async Task UnEmpleadoNoEntraALaBandeja()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await cliente.GetAsync("/api/v1/revision/pendientes");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ---------- El dictamen ----------

    [Fact]
    public async Task AprobarUnaChecadaLaSacaDeLaBandejaYLaDejaContandoParaLaJornada()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var admin = await Admin();

        var dictaminada = await (await Dictaminar(admin, checada.Id, "aprobar", "Verificado con el jefe de piso."))
            .LeerAsync<ChecadaPorRevisarDto>();

        Assert.Equal(EstadoChecada.AjustadaPorSupervisor, dictaminada.Estado);
        Assert.Null(await BuscarPendienteAsync(admin, checada.Id));

        using var alcance = fabrica.Services.CreateScope();
        var bd = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();
        var guardada = await bd.Checadas.AsNoTracking().FirstAsync(c => c.Id == checada.Id);

        Assert.True(guardada.CuentaParaJornada);
        Assert.Equal("Verificado con el jefe de piso.", guardada.MotivoAjuste);
        Assert.NotNull(guardada.AjustadaPorUsuarioId);
    }

    [Fact]
    public async Task RechazarUnaChecadaLaDejaFueraDeLaJornadaPeroNoLaBorra()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var admin = await Admin();

        var dictaminada = await (await Dictaminar(admin, checada.Id, "rechazar", "No hubo asistencia ese día."))
            .LeerAsync<ChecadaPorRevisarDto>();

        Assert.Equal(EstadoChecada.Rechazada, dictaminada.Estado);

        using var alcance = fabrica.Services.CreateScope();
        var bd = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();
        var guardada = await bd.Checadas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == checada.Id);

        // Sigue ahí, con su motivo. Borrarla dejaría a RH sin poder explicar la ausencia.
        Assert.NotNull(guardada);
        Assert.False(guardada.CuentaParaJornada);
        Assert.Equal("No hubo asistencia ese día.", guardada.MotivoAjuste);
    }

    [Fact]
    public async Task UnaChecadaYaDictaminadaNoSePuedeVolverADictaminar()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var admin = await Admin();

        await Dictaminar(admin, checada.Id, "aprobar", "Primer dictamen.");

        // Dos revisores abriendo la misma bandeja: el segundo se entera en vez de pisar.
        var segundo = await Dictaminar(admin, checada.Id, "rechazar", "Segundo dictamen.");

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Contains("bitácora", await segundo.DetalleAsync());
    }

    [Fact]
    public async Task ElDictamenExigeUnMotivoEscrito()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var admin = await Admin();

        var respuesta = await Dictaminar(admin, checada.Id, "aprobar", "   ");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Motivo", (await respuesta.ErroresAsync()).Keys);
    }

    [Fact]
    public async Task NadieApruebaSuPropiaChecadaNiSiendoAdministrador()
    {
        // Es la regla que sostiene el umbral: si el interesado pudiera firmar su propio
        // visto bueno, la revisión sería un trámite en vez de un control.
        var admin = await Admin();
        var suya = await FicharAsync(admin);

        var respuesta = await Dictaminar(admin, suya.Id, "aprobar", "Me consta que estuve.");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Contains("su propia checada", await respuesta.DetalleAsync());
    }

    [Fact]
    public async Task UnSupervisorNoDictaminaFueraDeSuDepartamento()
    {
        var (deSistemas, _) = await ChecadaDudosaAsync(Sistemas);
        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        var respuesta = await Dictaminar(supervisor, deSistemas.Id, "aprobar", "Intento fuera de alcance.");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnEmpleadoNoDictaminaNada()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await Dictaminar(cliente, checada.Id, "aprobar", "No me corresponde.");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task DictaminarUnaChecadaInexistenteDevuelveNoEncontrado()
    {
        var admin = await Admin();

        var respuesta = await Dictaminar(admin, Guid.CreateVersion7(), "aprobar", "Fantasma.");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------- La bitácora ----------

    [Fact]
    public async Task CadaDictamenDejaUnAsientoConQuienYPorQue()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var admin = await Admin();

        await Dictaminar(admin, checada.Id, "aprobar", "Corte de energía en el lector.");

        var asiento = await BuscarAsientoAsync(admin, checada.Id);

        Assert.NotNull(asiento);
        Assert.Equal(AccionAuditada.ChecadaAprobada, asiento.Accion);
        Assert.Equal("Checada aprobada", asiento.AccionNombre);
        Assert.Equal(ClienteAutenticado.Admin, asiento.UsuarioCorreo);
        Assert.Equal("Corte de energía en el lector.", asiento.Motivo);
    }

    [Fact]
    public async Task ElAsientoConservaElEstadoAnteriorYElPuntajeDelMomento()
    {
        var (checada, sesion) = await ChecadaDudosaAsync();
        var admin = await Admin();

        await Dictaminar(admin, checada.Id, "rechazar", "Fichó desde fuera de la sede.");

        using var alcance = fabrica.Services.CreateScope();
        var bd = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

        var asiento = await bd.Bitacora.AsNoTracking().FirstAsync(a => a.EntidadId == checada.Id);

        Assert.NotNull(asiento.DatosJson);

        using var datos = JsonDocument.Parse(asiento.DatosJson);
        var raiz = datos.RootElement;

        // El estado del que se venía es la mitad de la historia: sin él, un asiento de
        // rechazo no distingue entre descartar una checada dudosa y tumbar una verificada.
        Assert.Equal("RequiereRevision", raiz.GetProperty("estadoPrevio").GetString());
        Assert.Equal("Rechazada", raiz.GetProperty("estadoNuevo").GetString());
        Assert.Equal(25, raiz.GetProperty("puntajeConfianza").GetInt32());
        Assert.Equal(sesion.Empleado.Id, raiz.GetProperty("empleadoId").GetGuid());
    }

    [Fact]
    public async Task LaBitacoraNoSePuedeAlterarNiSiquieraDesdeLaBaseDeDatos()
    {
        var (checada, _) = await ChecadaDudosaAsync();
        var admin = await Admin();

        await Dictaminar(admin, checada.Id, "aprobar", "Motivo original.");

        using var alcance = fabrica.Services.CreateScope();
        var bd = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

        // La inmutabilidad vive en un disparador de PostgreSQL y no en la buena conducta de
        // la aplicación: una bitácora que el propio código puede reescribir no prueba nada.
        var alterar = await Assert.ThrowsAnyAsync<Exception>(() =>
            bd.Database.ExecuteSqlRawAsync(
                "UPDATE bitacora SET motivo = 'reescrito' WHERE entidad_id = {0}", checada.Id));

        var borrar = await Assert.ThrowsAnyAsync<Exception>(() =>
            bd.Database.ExecuteSqlRawAsync("DELETE FROM bitacora WHERE entidad_id = {0}", checada.Id));

        Assert.Contains("solo inserción", alterar.ToString());
        Assert.Contains("solo inserción", borrar.ToString());

        var asiento = await bd.Bitacora.AsNoTracking().FirstAsync(a => a.EntidadId == checada.Id);
        Assert.Equal("Motivo original.", asiento.Motivo);
    }

    [Fact]
    public async Task UnEmpleadoNoLeeLaBitacora()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await cliente.GetAsync("/api/v1/revision/bitacora");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ---------- Ayudantes ----------

    private Task<HttpClient> Admin() => fabrica.ComoAsync(ClienteAutenticado.Admin);

    /// <summary>Ficha solo con QR, que es justo lo que deja la checada por debajo del umbral.</summary>
    private async Task<(ChecadaDto Checada, SesionDePrueba Sesion)> ChecadaDudosaAsync(Guid? departamentoId = null)
    {
        var sesion = await fabrica.NuevoEmpleadoAsync(departamentoId, Monterrey, prefijo: "revision");
        var checada = await FicharAsync(sesion.Cliente);

        Assert.Equal(EstadoChecada.RequiereRevision, checada.Estado);

        return (checada, sesion);
    }

    private async Task<ChecadaDto> FicharAsync(HttpClient cliente)
    {
        var admin = await Admin();
        var codigo = await (await admin.GetAsync($"/api/v1/kiosco/{Monterrey}/codigo"))
            .LeerAsync<CodigoKioscoDto>();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/checadas/qr",
            new SolicitudChecadaQr { Token = codigo.Token },
            ClienteAutenticado.Json);

        return await (await respuesta.AsegurarExitoAsync()).LeerAsync<ChecadaDto>();
    }

    private static Task<HttpResponseMessage> Dictaminar(
        HttpClient cliente,
        Guid checadaId,
        string accion,
        string motivo) =>
        cliente.PostAsJsonAsync(
            $"/api/v1/revision/{checadaId}/{accion}",
            new SolicitudDictamen { Motivo = motivo },
            ClienteAutenticado.Json);

    /// <summary>
    /// Recorre la bandeja hasta dar con la checada: las pruebas comparten base y otra clase
    /// puede haber dejado pendientes por delante.
    /// </summary>
    private static async Task<ChecadaPorRevisarDto?> BuscarPendienteAsync(HttpClient cliente, Guid checadaId) =>
        await BuscarAsync(
            cliente,
            "/api/v1/revision/pendientes",
            (ChecadaPorRevisarDto c) => c.Id == checadaId);

    private static async Task<AsientoBitacoraDto?> BuscarAsientoAsync(HttpClient cliente, Guid entidadId) =>
        await BuscarAsync(
            cliente,
            "/api/v1/revision/bitacora",
            (AsientoBitacoraDto a) => a.EntidadId == entidadId);

    private static async Task<T?> BuscarAsync<T>(HttpClient cliente, string ruta, Func<T, bool> coincide)
        where T : class
    {
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var lote = await (await cliente.GetAsync($"{ruta}?pagina={pagina}&tamano=100"))
                .LeerAsync<ResultadoPaginado<T>>();

            if (lote.Elementos.FirstOrDefault(coincide) is { } encontrado)
            {
                return encontrado;
            }

            if (lote.Elementos.Count == 0 || pagina * 100 >= lote.Total)
            {
                return null;
            }
        }

        return null;
    }
}
